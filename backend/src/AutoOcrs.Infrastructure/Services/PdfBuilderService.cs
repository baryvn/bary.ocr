using System.Text.Json;
using AutoOcrs.Core.Entities;
using AutoOcrs.Core.Models;
using Microsoft.Extensions.Logging;
using PdfSharpCore.Drawing;
using PdfSharpCore.Pdf;

namespace AutoOcrs.Infrastructure.Services;

public class PdfBuilderService(MinioStorageService minioService, ILogger<PdfBuilderService> logger)
{
    public async Task<Stream> BuildSearchablePdfAsync(Document document, List<DocumentPage> pages, List<Stream> imageStreams)
    {
        var pdfDocument = new PdfDocument();
        pdfDocument.Info.Title = document.OriginalFilename;
        pdfDocument.Info.Author = "AutoOcrs System";

        // Font cấu hình, PdfSharpCore cần cài đặt font resolver hoặc dùng font chuẩn
        // Tạm dùng font chuẩn "Arial"
        var font = new XFont("Arial", 10, XFontStyle.Regular);
        
        // Brush trong suốt để giấu text
        var transparentBrush = new XSolidBrush(XColor.FromArgb(0, 0, 0, 0));

        var orderedPages = pages.OrderBy(p => p.PageNumber).ToList();
        for (int i = 0; i < orderedPages.Count; i++)
        {
            var page = orderedPages[i];
            var imageStream = imageStreams[i];
            
            try
            {
                logger.LogInformation($"Xây dựng trang PDF {page.PageNumber} cho Document {document.Id}");
                
                imageStream.Position = 0;
                using var xImage = XImage.FromStream(() => imageStream);

                // Tạo trang PDF mới với kích thước chuẩn (Point = Pixel * 72 / DPI)
                // Giả định DPI = 300, PdfSharp mặc định 72 DPI
                var pdfPage = pdfDocument.AddPage();
                
                // XImage tự tính Point width/height dựa trên DPI của ảnh, hoặc ta set cứng
                pdfPage.Width = xImage.PointWidth;
                pdfPage.Height = xImage.PointHeight;

                using var gfx = XGraphics.FromPdfPage(pdfPage);
                
                // 1. Vẽ ảnh (Lớp hiển thị)
                gfx.DrawImage(xImage, 0, 0, pdfPage.Width, pdfPage.Height);

                // 2. Vẽ Text (Lớp tìm kiếm)
                if (!string.IsNullOrEmpty(page.OcrResult))
                {
                    var textOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    var textBoxes = JsonSerializer.Deserialize<List<OcrTextBox>>(page.OcrResult, textOptions);

                    if (textBoxes != null)
                    {
                        // Tính tỉ lệ chuyển đổi Pixel -> Point
                        double scaleX = pdfPage.Width / xImage.PixelWidth;
                        double scaleY = pdfPage.Height / xImage.PixelHeight;

                        foreach (var box in textBoxes)
                        {
                            if (box.Box == null || box.Box.Length < 4) continue;

                            double xMin = box.Box[0] * scaleX;
                            double yMin = box.Box[1] * scaleY;
                            double xMax = box.Box[2] * scaleX;
                            double yMax = box.Box[3] * scaleY;

                            double boxWidth = xMax - xMin;
                            double boxHeight = yMax - yMin;

                            // Chỉnh font size vừa vặn box height
                            var boxFont = new XFont("Arial", boxHeight * 0.8, XFontStyle.Regular);
                            
                            // Vẽ text trong suốt
                            var layoutRect = new XRect(xMin, yMin, boxWidth, boxHeight);
                            gfx.DrawString(box.Text, boxFont, transparentBrush, layoutRect, XStringFormats.TopLeft);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, $"Lỗi khi build trang {page.PageNumber} của Document {document.Id}");
            }
        }

        var resultStream = new MemoryStream();
        pdfDocument.Save(resultStream, false);
        resultStream.Position = 0;

        return resultStream;
    }
}
