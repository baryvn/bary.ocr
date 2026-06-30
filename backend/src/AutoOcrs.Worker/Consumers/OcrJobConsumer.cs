using System.Text;
using AutoOcrs.Core.Entities;
using AutoOcrs.Core.Enums;
using AutoOcrs.Core.Messages;
using AutoOcrs.Infrastructure.Data;
using AutoOcrs.Infrastructure.Services;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AutoOcrs.Worker.Consumers;

public class OcrJobConsumer(
    ILogger<OcrJobConsumer> logger,
    AppDbContext db,
    MinioStorageService minioService,
    PdfRendererService pdfRenderer,
    OcrService ocrService,
    PdfBuilderService pdfBuilderService,
    ClassificationService classificationService,
    NamingRuleService namingService,
    ExtractionService extractionService,
    TextCleanupService textCleanupService) : IConsumer<OcrJobMessage>
{
    public async Task Consume(ConsumeContext<OcrJobMessage> context)
    {
        var msg = context.Message;
        logger.LogInformation($"Bắt đầu xử lý OCR cho Document: {msg.DocumentId}");

        var doc = await db.Documents.FindAsync(msg.DocumentId);
        if (doc == null)
        {
            logger.LogWarning($"Không tìm thấy Document {msg.DocumentId}");
            return;
        }

        doc.Status = DocumentStatus.OcrProcessing;
        await db.SaveChangesAsync();

        List<Stream>? imageStreams = null;
        try
        {
            // 1. Tải PDF từ MinIO
            logger.LogInformation($"Tải file {msg.StorageKey} từ MinIO...");
            using var pdfStream = await minioService.DownloadFileAsync(msg.StorageKey);

            // 2. Render PDF thành ảnh
            logger.LogInformation($"Render PDF thành ảnh...");
            imageStreams = pdfRenderer.RenderPdfToImages(pdfStream, 300);
            doc.PageCount = imageStreams.Count;

            // 3. Gọi OCR API cho từng trang
            var localPages = new List<DocumentPage>();
            for (int i = 0; i < imageStreams.Count; i++)
            {
                logger.LogInformation($"OCR trang {i + 1}/{imageStreams.Count}...");
                var imgStream = imageStreams[i];
                doc.ProcessingMessage = $"OCR {i + 1}/{imageStreams.Count} trang";
                await db.SaveChangesAsync();

                string ocrResultJson = await ocrService.ProcessImageAsync(imgStream, $"page_{i+1}.png");

                // Lưu kết quả trang
                var docPage = new DocumentPage
                {
                    DocumentId = doc.Id,
                    PageNumber = i + 1,
                    OcrResult = ocrResultJson,
                    ImageKey = null // Yêu cầu: Không đẩy ảnh lên MinIO
                };
                
                db.DocumentPages.Add(docPage);
                localPages.Add(docPage);
            }

            // 4. Gọi PdfBuilderService để tạo PDF 2 lớp
            logger.LogInformation($"Đang tạo PDF 2 lớp cho Document: {doc.Id}...");
            using var builtPdfStream = await pdfBuilderService.BuildSearchablePdfAsync(doc, localPages, imageStreams);
            
            // 5. Upload PDF 2 lớp lên MinIO
            await db.Entry(doc).Reference(d => d.Batch).Query().Include(b => b.Project).LoadAsync();
            string folderPath = doc.Batch?.Project != null ? namingService.GenerateFolderPath(doc, doc.Batch.Project) : doc.BatchId.ToString();
            string fileName = doc.Batch?.Project != null ? namingService.GenerateFilename(doc, doc.Batch.Project) : (doc.OriginalFilename ?? $"doc_{doc.Id}_ocr.pdf");
            string outputPrefix = doc.Batch?.Project?.OutputPath ?? "exports";
            string ocrPdfKey = await minioService.UploadFileAsync(builtPdfStream, $"{outputPrefix.Trim('/')}/{folderPath}/{fileName}", "application/pdf", true);
            doc.OcrPdfKey = ocrPdfKey;

            // 5b. Tạo file Markdown text sạch cho từng trang và upload lên MinIO
            logger.LogInformation($"Đang tạo file markdown text sạch cho Document: {doc.Id}...");
            doc.ProcessingMessage = "Tạo file text sạch...";
            await db.SaveChangesAsync();

            string baseFileName = Path.GetFileNameWithoutExtension(fileName);
            string textPrefix = "text";

            for (int i = 0; i < localPages.Count; i++)
            {
                var page = localPages[i];
                if (string.IsNullOrEmpty(page.OcrResult)) continue;

                string cleanText = textCleanupService.ConvertToCleanMarkdown(page.OcrResult);
                if (string.IsNullOrWhiteSpace(cleanText)) continue;

                string mdFileName = $"{baseFileName}_page_{page.PageNumber}.md";
                string mdKey = $"{textPrefix}/{folderPath}/{mdFileName}";

                using var mdStream = new MemoryStream(Encoding.UTF8.GetBytes(cleanText));
                await minioService.UploadFileAsync(mdStream, mdKey, "text/markdown", true);

                // Lưu MinIO key vào cột FullText để Classification/Extraction có thể truy cập
                page.FullText = mdKey;
                logger.LogInformation($"Đã lưu text sạch trang {page.PageNumber} → {mdKey}");
            }

            await db.SaveChangesAsync();

            doc.Status = DocumentStatus.OcrDone;
            doc.OcrCompletedAt = DateTime.UtcNow;

            // 6. Thực hiện phân loại (Classification)
            logger.LogInformation($"Đang phân loại Document: {doc.Id}...");
            doc.Status = DocumentStatus.Classifying;
            doc.ProcessingMessage = "Phân loại...";
            await db.SaveChangesAsync();

            var classificationResult = await classificationService.ClassifyDocumentAsync(doc, localPages);
            if (classificationResult.LabelId.HasValue)
            {
                doc.LabelId = classificationResult.LabelId.Value;
                doc.ClassificationConfidence = classificationResult.Confidence;
                doc.Status = DocumentStatus.Classified;
                doc.ClassifiedAt = DateTime.UtcNow;
                logger.LogInformation($"Phân loại thành công: LabelId = {classificationResult.LabelId.Value}, Confidence = {classificationResult.Confidence}");

                // 7. Thực hiện bóc tách (Extraction)
                logger.LogInformation($"Đang bóc tách Metadata cho Document: {doc.Id}...");
                doc.Status = DocumentStatus.Extracting;
                doc.ProcessingMessage = "Bóc tách...";
                await db.SaveChangesAsync();

                string? extractedData = await extractionService.ExtractMetadataAsync(doc, localPages);
                if (extractedData != null)
                {
                    doc.ExtractedMetadata = extractedData;
                    doc.Status = DocumentStatus.Extracted;
                    doc.ProcessingMessage = null;
                    doc.ExtractedAt = DateTime.UtcNow;
                    logger.LogInformation($"Bóc tách thành công cho Document: {doc.Id}");
                }
                else
                {
                    logger.LogWarning($"Bóc tách thất bại hoặc không có config cho Document: {doc.Id}");
                    doc.Status = DocumentStatus.Classified; // Giữ lại state cũ hoặc chuyển state tùy yêu cầu
                }
            }
            else
            {
                logger.LogWarning($"Không thể phân loại Document: {doc.Id}");
                doc.Status = DocumentStatus.Classified; // Vẫn update state để đi tiếp, user sẽ review thủ công
            }
            
            // Xong pipeline tự động, chờ Review
            if (doc.Status == DocumentStatus.Extracted || doc.Status == DocumentStatus.Classified)
            {
                doc.Status = DocumentStatus.ReadyForReview;
            }

            logger.LogInformation($"Toàn bộ pipeline tự động hoàn tất cho Document: {msg.DocumentId}");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"Lỗi OCR cho Document: {msg.DocumentId}");
            doc.Status = DocumentStatus.Error;
            doc.ErrorMessage = ex.Message;
        }
        finally
        {
            if (imageStreams != null)
            {
                foreach (var stream in imageStreams)
                {
                    stream?.Dispose();
                }
            }

            // Cập nhật Database
            await db.SaveChangesAsync();
            
            // Có thể publish 1 message báo OcrResultMessage để trigger bước tiếp theo (Classifying)
            await context.Publish(new OcrResultMessage(doc.Id, doc.Status.ToString(), doc.ErrorMessage));
        }
    }
}
