using System.Text.Json;
using AutoOcrs.Core.Entities;
using AutoOcrs.Core.Models;
using Microsoft.Extensions.Logging;

namespace AutoOcrs.Infrastructure.Services;

public class ClassificationService(LlmService llmService, LabelService labelService, MinioStorageService minioService, ILogger<ClassificationService> logger)
{
    public async Task<(Guid? LabelId, float Confidence)> ClassifyDocumentAsync(Document document, List<DocumentPage> pages)
    {
        try
        {
            // 1. Lấy tất cả nhãn của dự án
            var labels = await labelService.GetFlatListAsync(document.Batch?.ProjectId ?? Guid.Empty);
            if (labels == null || labels.Count == 0) return (null, 0f);

            var labelOptions = labels.Select(l => new { id = l.Id, name = l.Name, code = l.Code, description = l.Description, prompt = l.LlmPrompt }).ToList();
            string labelTreeJson = JsonSerializer.Serialize(labelOptions);

            // Gộp text sạch của 3 trang đầu (đọc từ MinIO nếu có, fallback sang OcrResult)
            var textToClassify = "";
            var topPages = pages.OrderBy(p => p.PageNumber).Take(3).ToList();
            
            foreach (var page in topPages)
            {
                string pageText = await GetCleanTextAsync(page);
                if (!string.IsNullOrWhiteSpace(pageText))
                {
                    textToClassify += pageText + "\n\n";
                }
            }

            if (string.IsNullOrWhiteSpace(textToClassify)) return (null, 0f);

            string basePrompt = @"Bạn là chuyên gia phân loại tài liệu văn bản.
Dưới đây là danh sách các nhãn (Labels) có sẵn trong hệ thống (định dạng JSON). Mỗi nhãn chứa thuộc tính 'prompt' (hướng dẫn chi tiết cách nhận diện nhãn này).
" + labelTreeJson + @"

NHIỆM VỤ CỦA BẠN:
1. Đọc kỹ văn bản được cung cấp.
2. Đọc kỹ thuộc tính 'prompt' và 'name' của từng nhãn để hiểu rõ tiêu chí phân loại.
3. So sánh nội dung văn bản với tiêu chí của từng nhãn và chọn ra nhãn (labelId) ĐÚNG NHẤT. Hãy đặc biệt chú ý đến tiêu đề hoặc thể loại của văn bản.

QUY TẮC TRẢ VỀ:
Chỉ trả về ĐÚNG MỘT JSON OBJECT duy nhất. TUYỆT ĐỐI KHÔNG thêm bất kỳ giải thích, chú thích hoặc ký tự markdown nào khác.
Cấu trúc JSON bắt buộc:
{
  ""labelId"": ""guid_của_nhãn"",
  ""confidence"": 0.95
}
Nếu không thể phân loại hoặc văn bản không khớp với bất kỳ tiêu chí nào, trả về:
{
  ""labelId"": null,
  ""confidence"": 0.0
}";

            string systemPrompt = !string.IsNullOrWhiteSpace(document.Batch?.Project?.ClassificationPrompt)
                ? document.Batch.Project.ClassificationPrompt + "\n\n" + basePrompt
                : basePrompt;

            string userPrompt = "Văn bản cần phân loại:\n" + textToClassify;

            logger.LogInformation($"[Classification] Gửi yêu cầu phân loại LLM cho Document {document.Id}. Text length: {textToClassify.Length}. Labels count: {labelOptions.Count}");
            
            string rawResponseJson = await llmService.CallLlmAsync(systemPrompt, userPrompt);
            logger.LogInformation($"[Classification] Document {document.Id} - Raw LLM Response:\n{rawResponseJson}");
            
            // Clean markdown formatting if present
            string responseJson = CleanLlmResponse(rawResponseJson);
            logger.LogInformation($"[Classification] Document {document.Id} - Cleaned JSON:\n{responseJson}");

            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var result = JsonSerializer.Deserialize<ClassificationResult>(responseJson, options);

            return (result?.LabelId, result?.Confidence ?? 0f);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"Lỗi Classification Document {document.Id}");
            return (null, 0f);
        }
    }

    /// <summary>
    /// Lấy text sạch cho 1 trang: ưu tiên đọc file .md từ MinIO (qua FullText key), fallback sang parse OcrResult JSON.
    /// </summary>
    private async Task<string> GetCleanTextAsync(DocumentPage page)
    {
        // Ưu tiên đọc file .md từ MinIO
        if (!string.IsNullOrEmpty(page.FullText))
        {
            try
            {
                using var stream = await minioService.DownloadFileAsync(page.FullText);
                using var reader = new StreamReader(stream);
                return await reader.ReadToEndAsync();
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, $"Không thể đọc file text từ MinIO ({page.FullText}), fallback sang OcrResult.");
            }
        }

        // Fallback: parse OcrResult JSON
        if (!string.IsNullOrEmpty(page.OcrResult))
        {
            var textOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var textBoxes = JsonSerializer.Deserialize<List<OcrTextBox>>(page.OcrResult, textOptions);
            if (textBoxes != null)
            {
                return string.Join(" ", textBoxes.Select(t => t.Text));
            }
        }

        return string.Empty;
    }

    private static string CleanLlmResponse(string responseJson)
    {
        if (string.IsNullOrWhiteSpace(responseJson)) return "{}";
        
        int startObj = responseJson.IndexOf('{');
        int endObj = responseJson.LastIndexOf('}');
        
        int startArr = responseJson.IndexOf('[');
        int endArr = responseJson.LastIndexOf(']');

        // Nếu là JSON Object (có {} và bọc ngoài cùng hoặc hợp lý nhất)
        if (startObj >= 0 && endObj > startObj)
        {
            // Kiểm tra xem nó có phải là array không
            if (startArr >= 0 && endArr > startArr && startArr < startObj && endArr > endObj)
            {
                return responseJson.Substring(startArr, endArr - startArr + 1);
            }
            return responseJson.Substring(startObj, endObj - startObj + 1);
        }
        else if (startArr >= 0 && endArr > startArr)
        {
             return responseJson.Substring(startArr, endArr - startArr + 1);
        }

        return "{}";
    }

    private class ClassificationResult
    {
        public Guid? LabelId { get; set; }
        public float Confidence { get; set; }
    }
}
