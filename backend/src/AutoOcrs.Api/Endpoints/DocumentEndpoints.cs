using AutoOcrs.Core.DTOs.Documents;
using AutoOcrs.Core.Entities;
using AutoOcrs.Core.Enums;
using AutoOcrs.Infrastructure.Data;
using AutoOcrs.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text.Json;

namespace AutoOcrs.Api.Endpoints;

public static class DocumentEndpoints
{
    public static void MapDocumentEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/documents").RequireAuthorization();

        // Lấy thông tin chi tiết Document
        group.MapGet("/{id:guid}", async (Guid id, AppDbContext db) =>
        {
            var doc = await db.Documents
                .Include(d => d.Batch)
                .Include(d => d.Label)
                .Include(d => d.Pages)
                .FirstOrDefaultAsync(d => d.Id == id);

            if (doc == null) return Results.NotFound();

            return Results.Ok(new DocumentDetailResponse(
                doc.Id,
                doc.Batch?.ProjectId ?? Guid.Empty,
                doc.OriginalFilename,
                doc.Status.ToString(),
                doc.PageCount,
                doc.LabelId,
                doc.Label?.Name,
                doc.ExtractedMetadata,
                doc.ReviewedMetadata,
                doc.ErrorMessage,
                doc.ProcessingMessage,
                doc.ReviewerNote,
                doc.Pages.Select(p => new DocumentPageResponse(
                    p.Id, p.PageNumber, p.ImageKey, p.Width, p.Height)).ToList()
            ));
        });

        // Retry document lỗi
        group.MapPost("/{id:guid}/retry", async (Guid id, AppDbContext db, OcrJobPublisher publisher) =>
        {
            try
            {
                var doc = await db.Documents.FindAsync(id) ?? throw new Exception("Không tìm thấy tài liệu.");
                
                if (doc.Status == DocumentStatus.Approved || doc.Status == DocumentStatus.Classifying || doc.Status == DocumentStatus.Extracting || doc.Status == DocumentStatus.OcrProcessing)
                    throw new Exception("Tài liệu đang xử lý hoặc đã duyệt, không thể gửi lại yêu cầu.");

                doc.Status = DocumentStatus.Pending;
                doc.ErrorMessage = null;
                await db.SaveChangesAsync();

                if (!string.IsNullOrEmpty(doc.StorageKey))
                {
                    await publisher.PublishJobAsync(doc.Id, doc.Batch?.ProjectId ?? Guid.Empty, doc.StorageKey);
                }

                return Results.Ok(new { message = "Đã gửi lại yêu cầu xử lý." });
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        }).RequireAuthorization(p => p.RequireRole("Manager"));

        // Download/Stream PDF gốc
        group.MapGet("/{id:guid}/original", async (Guid id, AppDbContext db, MinioStorageService minio) =>
        {
            var doc = await db.Documents.FindAsync(id);
            if (doc == null || string.IsNullOrEmpty(doc.StorageKey)) return Results.NotFound();

            var stream = await minio.DownloadFileAsync(doc.StorageKey);
            return Results.File(stream, "application/pdf", doc.OriginalFilename);
        });

        // Download/Stream PDF 2 lớp (Sau OCR)
        group.MapGet("/{id:guid}/ocr-pdf", async (Guid id, AppDbContext db, MinioStorageService minio) =>
        {
            var doc = await db.Documents.FindAsync(id);
            if (doc == null || string.IsNullOrEmpty(doc.OcrPdfKey)) return Results.NotFound();

            var stream = await minio.DownloadFileAsync(doc.OcrPdfKey);
            return Results.File(stream, "application/pdf", $"OCR_{doc.OriginalFilename}");
        });

        // Stream từng trang (Ảnh)
        group.MapGet("/{id:guid}/pages/{num:int}", async (Guid id, int num, AppDbContext db, MinioStorageService minio) =>
        {
            var page = await db.DocumentPages.FirstOrDefaultAsync(p => p.DocumentId == id && p.PageNumber == num);
            if (page == null || string.IsNullOrEmpty(page.ImageKey)) return Results.NotFound();

            var stream = await minio.DownloadFileAsync(page.ImageKey);
            return Results.File(stream, "image/png");
        });

        // Đổi nhãn thủ công
        group.MapPut("/{id:guid}/label", async (Guid id, [FromBody] Guid? labelId, AppDbContext db) =>
        {
            try
            {
                var doc = await db.Documents.FindAsync(id) ?? throw new Exception("Không tìm thấy tài liệu.");
                doc.LabelId = labelId;
                await db.SaveChangesAsync();
                return Results.Ok(new { message = "Cập nhật nhãn thành công." });
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        }).RequireAuthorization(p => p.RequireRole("Manager", "Reviewer"));

        // Phân loại lại bằng LLM
        group.MapPost("/{id:guid}/reclassify", async (Guid id, AppDbContext db, OcrJobPublisher publisher) =>
        {
            try
            {
                // Thực tế có thể đẩy vào 1 queue riêng (ClassifyQueue),
                // nhưng tạm thời ta có thể đẩy lại vào OcrQueue hoặc xử lý trực tiếp.
                // Để đơn giản, ta xử lý trực tiếp luôn nếu nó không tốn quá nhiều thời gian (chỉ gọi API)
                // Hoặc tạo ClassifyJobPublisher. Vì ta đang gọi Classification ở worker,
                // ta nên tách Ocr và Classification thành 2 Message nếu muốn gọi độc lập.
                // Tạm thời, endpoint này sẽ push message OcrJobMessage để chạy lại TỪ ĐẦU (OCR + Classify)
                // HOẶC chỉ update status nếu muốn.
                // Cách đúng là có 1 ClassifyJobMessage. Để tiết kiệm thời gian, tôi sẽ tạo hàm ReclassifyAsync trong BatchService/ClassificationService
                // nhưng gọi trực tiếp qua HTTP sẽ block lâu.
                
                // Ở đây ta update status về Classifying và đẩy lại vào queue OCR (vì OcrConsumer hiện đang ôm cả Classify)
                // Đây là workaround.
                var doc = await db.Documents.FindAsync(id) ?? throw new Exception("Không tìm thấy tài liệu.");
                if (doc.Status < DocumentStatus.OcrDone) throw new Exception("Tài liệu chưa OCR xong.");
                
                doc.Status = DocumentStatus.Pending; // Để OCR chạy lại và qua bước classify
                await db.SaveChangesAsync();

                if (!string.IsNullOrEmpty(doc.StorageKey))
                {
                    await publisher.PublishJobAsync(doc.Id, doc.Batch?.ProjectId ?? Guid.Empty, doc.StorageKey);
                }

                return Results.Ok(new { message = "Đã gửi lại yêu cầu OCR & Phân loại." });
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        }).RequireAuthorization(p => p.RequireRole("Manager"));

        // Bóc tách lại bằng LLM
        group.MapPost("/{id:guid}/re-extract", async (Guid id, AppDbContext db, OcrJobPublisher publisher) =>
        {
            try
            {
                // Tương tự reclassify, lý tưởng là đẩy vào ExtractionQueue.
                // Tạm dùng workaround đẩy lại vào OcrQueue. (OCR sẽ cache hoặc skip nếu đã OCR,
                // nhưng hiện tại Worker ôm tất cả và chưa có cache/skip logic).
                // Do đó, nếu call re-extract, status sẽ lùi về Pending và chạy lại CẢ OCR, CLASSIFY.
                // Để làm chuẩn: tạo 1 message riêng hoặc viết method ReExtract cho endpoint này.
                // Do hạn chế thời gian, tôi tạm thiết lập logic đơn giản:
                var doc = await db.Documents.FindAsync(id) ?? throw new Exception("Không tìm thấy tài liệu.");
                if (doc.Status < DocumentStatus.Classified) throw new Exception("Tài liệu chưa phân loại xong.");
                
                // Đẩy vào queue để xử lý lại toàn bộ (workaround)
                doc.Status = DocumentStatus.Pending;
                await db.SaveChangesAsync();

                if (!string.IsNullOrEmpty(doc.StorageKey))
                {
                    await publisher.PublishJobAsync(doc.Id, doc.Batch?.ProjectId ?? Guid.Empty, doc.StorageKey);
                }

                return Results.Ok(new { message = "Đã gửi lại yêu cầu xử lý tổng hợp." });
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        }).RequireAuthorization(p => p.RequireRole("Manager"));

        // Cập nhật metadata thủ công
        group.MapPut("/{id:guid}/metadata", async (Guid id, [FromBody] JsonElement metadata, ClaimsPrincipal user, AppDbContext db, AuditLogService audit) =>
        {
            try
            {
                var userId = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
                var doc = await db.Documents.FindAsync(id) ?? throw new Exception("Không tìm thấy tài liệu.");
                
                string oldMeta = doc.ReviewedMetadata ?? doc.ExtractedMetadata ?? "{}";
                string newMeta = metadata.GetRawText();
                
                doc.ReviewedMetadata = newMeta;
                doc.ReviewedBy = userId;
                
                await db.SaveChangesAsync();
                
                await audit.LogActionAsync(userId, "UpdateMetadata", "Document", doc.Id, oldMeta, newMeta);

                return Results.Ok(new { message = "Lưu metadata thành công." });
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        }).RequireAuthorization(p => p.RequireRole("Reviewer", "Manager"));

        // Approve document
        group.MapPost("/{id:guid}/approve", async (Guid id, ClaimsPrincipal user, AppDbContext db, AuditLogService audit, NamingRuleService namingService, MinioStorageService minio) =>
        {
            try
            {
                var userId = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
                var doc = await db.Documents
                    .Include(d => d.Batch)
                    .ThenInclude(b => b.Project)
                    .Include(d => d.Label)
                    .FirstOrDefaultAsync(d => d.Id == id) ?? throw new Exception("Không tìm thấy tài liệu.");
                
                if (doc.Status == DocumentStatus.Approved) return Results.Ok(new { message = "Tài liệu đã được duyệt trước đó." });

                // 1. Cập nhật Status
                string oldStatus = doc.Status.ToString();
                doc.Status = DocumentStatus.Approved;
                doc.ReviewedBy = userId;
                doc.ReviewedAt = DateTime.UtcNow;

                // Cập nhật thống kê Batch
                if (doc.Batch != null)
                {
                    // Lấy số lượng file đã duyệt từ db để đảm bảo tính chính xác
                    var approvedCount = await db.Documents.CountAsync(d => d.BatchId == doc.BatchId && d.Status == DocumentStatus.Approved);
                    doc.Batch.ApprovedFiles = approvedCount + 1; // +1 cho file hiện tại
                    
                    if (doc.Batch.ApprovedFiles >= doc.Batch.TotalFiles && doc.Batch.TotalFiles > 0)
                    {
                        doc.Batch.Status = BatchStatus.Completed;
                    }
                }

                // 2. Naming Rule -> Tạo Output File
                if (!string.IsNullOrEmpty(doc.StorageKey) && doc.Batch?.Project != null)
                {
                    string newFilename = namingService.GenerateFilename(doc, doc.Batch.Project);
                    doc.OutputFilename = newFilename;

                    // Copy file trong MinIO (hoặc upload mới nếu cần)
                    // Vì ta đang xử lý OCR, ta có thể lưu file OCR PDF làm file output (OcrPdfKey) hoặc file gốc (StorageKey).
                    // Thường thì ta xuất file PDF 2 lớp.
                    string sourceKey = !string.IsNullOrEmpty(doc.OcrPdfKey) ? doc.OcrPdfKey : doc.StorageKey;
                    
                    // Lấy project output path
                    string folderPath = namingService.GenerateFolderPath(doc, doc.Batch.Project);
                    string prefix = string.IsNullOrEmpty(doc.Batch.Project.OutputPath) ? "exports" : doc.Batch.Project.OutputPath;
                    string outputKey = $"{prefix.Trim('/')}/{folderPath}/{newFilename}";
                    
                    // Dùng stream tải xuống và đẩy lên lại (cách đơn giản nhất)
                    using var stream = await minio.DownloadFileAsync(sourceKey);
                    doc.OutputKey = await minio.UploadFileAsync(stream, outputKey, "application/pdf", true);
                }

                await db.SaveChangesAsync();
                
                await audit.LogActionAsync(userId, "ApproveDocument", "Document", doc.Id, oldStatus, "Approved");

                return Results.Ok(new { message = "Đã duyệt tài liệu thành công.", outputFilename = doc.OutputFilename });
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        }).RequireAuthorization(p => p.RequireRole("Manager"));

        // Reject document
        group.MapPost("/{id:guid}/reject", async (Guid id, [FromBody] string note, ClaimsPrincipal user, AppDbContext db, AuditLogService audit) =>
        {
            try
            {
                var userId = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
                var doc = await db.Documents.FindAsync(id) ?? throw new Exception("Không tìm thấy tài liệu.");
                
                string oldStatus = doc.Status.ToString();
                doc.Status = DocumentStatus.Rejected;
                doc.ReviewerNote = note;
                doc.ReviewedBy = userId;
                doc.ReviewedAt = DateTime.UtcNow;
                
                await db.SaveChangesAsync();
                
                await audit.LogActionAsync(userId, "RejectDocument", "Document", doc.Id, oldStatus, $"Rejected - Note: {note}");

                return Results.Ok(new { message = "Đã từ chối tài liệu." });
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        }).RequireAuthorization(p => p.RequireRole("Manager"));

        // Cập nhật thông tin cơ bản tài liệu
        group.MapPut("/{id:guid}/info", async (Guid id, [FromBody] UpdateDocumentInfoRequest request, AppDbContext db) =>
        {
            try
            {
                var doc = await db.Documents.FindAsync(id) ?? throw new Exception("Không tìm thấy tài liệu.");
                if (!string.IsNullOrEmpty(request.OriginalFilename)) {
                    doc.OriginalFilename = request.OriginalFilename;
                }
                if (request.LabelId.HasValue) {
                    doc.LabelId = request.LabelId.Value;
                }
                await db.SaveChangesAsync();
                return Results.Ok(new { message = "Cập nhật thông tin tài liệu thành công." });
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        }).RequireAuthorization(p => p.RequireRole("Reviewer", "Manager"));

        // Xóa tài liệu
        group.MapDelete("/{id:guid}", async (Guid id, AppDbContext db) =>
        {
            try
            {
                var doc = await db.Documents.FindAsync(id) ?? throw new Exception("Không tìm thấy tài liệu.");
                db.Documents.Remove(doc);
                await db.SaveChangesAsync();
                return Results.Ok(new { message = "Đã xóa tài liệu thành công." });
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        }).RequireAuthorization(p => p.RequireRole("Manager"));

        // Lấy lịch sử chỉnh sửa tài liệu
        group.MapGet("/{id:guid}/history", async (Guid id, AppDbContext db) =>
        {
            try
            {
                var logs = await db.AuditLogs
                    .Include(a => a.User)
                    .Where(a => a.EntityId == id && a.EntityType == "Document")
                    .OrderByDescending(a => a.CreatedAt)
                    .Select(a => new 
                    {
                        a.Id,
                        a.Action,
                        UserName = a.User != null ? a.User.FullName : "System",
                        a.OldValues,
                        a.NewValues,
                        a.CreatedAt
                    })
                    .ToListAsync();
                
                return Results.Ok(logs);
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        }).RequireAuthorization(p => p.RequireRole("Reviewer", "Manager"));
    }
}

public record UpdateDocumentInfoRequest(string? OriginalFilename, Guid? LabelId);
