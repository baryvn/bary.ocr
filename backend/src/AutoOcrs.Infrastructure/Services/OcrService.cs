using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AutoOcrs.Infrastructure.Services;

public class OcrService(HttpClient httpClient, IConfiguration configuration, ILogger<OcrService> logger)
{
    public async Task<string> ProcessImageAsync(Stream imageStream, string fileName)
    {
        string ocrUrl = configuration["OcrWorker:Url"] ?? "http://localhost:8000";
        string endpoint = $"{ocrUrl.TrimEnd('/')}/ocr_page";

        using var content = new MultipartFormDataContent();
        
        // Đọc stream vào byte array để tránh HttpClient dispose mất stream gốc
        imageStream.Position = 0;
        using var ms = new MemoryStream();
        await imageStream.CopyToAsync(ms);
        var byteContent = new ByteArrayContent(ms.ToArray());
        byteContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        
        content.Add(byteContent, "file", fileName);

        try
        {
            logger.LogInformation($"Gửi ảnh {fileName} tới OCR Worker...");
            var response = await httpClient.PostAsync(endpoint, content);
            response.EnsureSuccessStatusCode();

            string jsonResponse = await response.Content.ReadAsStringAsync();
            return jsonResponse;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"Lỗi khi gọi OCR API cho ảnh {fileName}");
            throw;
        }
    }
}
