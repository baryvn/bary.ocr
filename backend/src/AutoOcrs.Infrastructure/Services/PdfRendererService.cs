using Docnet.Core;
using Docnet.Core.Models;
using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace AutoOcrs.Infrastructure.Services;

public class PdfRendererService(ILogger<PdfRendererService> logger)
{
    // Dùng Docnet.Core render PDF stream thành danh sách PNG streams (300 DPI mặc định)
    // Docnet.Core.IDocLib docLib = DocLib.Instance;
    public List<Stream> RenderPdfToImages(Stream pdfStream, int dpi = 300)
    {
        var result = new List<Stream>();
        try
        {
            using var ms = new MemoryStream();
            pdfStream.CopyTo(ms);
            byte[] pdfBytes = ms.ToArray();

            using var docReader = DocLib.Instance.GetDocReader(pdfBytes, new PageDimensions(dpi / 72.0));
            int pageCount = docReader.GetPageCount();
            
            logger.LogInformation($"Bắt đầu render PDF, số trang: {pageCount}");

            for (int i = 0; i < pageCount; i++)
            {
                using var pageReader = docReader.GetPageReader(i);
                var rawBytes = pageReader.GetImage();
                var width = pageReader.GetPageWidth();
                var height = pageReader.GetPageHeight();

                // Convert B-G-R-A to R-G-B-A ImageSharp
                using var image = Image.LoadPixelData<Bgra32>(rawBytes, width, height);
                var outStream = new MemoryStream();
                image.SaveAsPng(outStream);
                outStream.Position = 0;
                result.Add(outStream);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Lỗi khi render PDF thành ảnh");
            throw;
        }

        return result;
    }
}
