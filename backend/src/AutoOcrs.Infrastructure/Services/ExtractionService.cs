using System.Text.Json;
using AutoOcrs.Core.Entities;
using AutoOcrs.Core.Enums;
using AutoOcrs.Core.Models;
using AutoOcrs.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AutoOcrs.Infrastructure.Services;

public class ExtractionService(LlmService llmService, AppDbContext db, MinioStorageService minioService, ILogger<ExtractionService> logger)
{
    public async Task<string?> ExtractMetadataAsync(Document document, List<DocumentPage> pages)
    {
        try
        {
            if (!document.LabelId.HasValue)
            {
                logger.LogWarning($"Document {document.Id} chưa có Label, không thể bóc tách metadata.");
                return null;
            }

            // 1. Lấy cây nhãn (từ node hiện tại ngược lên gốc)
            var ancestorIds = new List<Guid>();
            var currentLabelId = document.LabelId;
            while (currentLabelId.HasValue)
            {
                ancestorIds.Add(currentLabelId.Value);
                currentLabelId = await db.Labels
                    .Where(l => l.Id == currentLabelId.Value)
                    .Select(l => l.ParentId)
                    .FirstOrDefaultAsync();
            }

            // 2. Lấy danh sách MetadataFields cấu hình cho tất cả các Label này
            var fields = await db.MetadataFields
                .Where(f => ancestorIds.Contains(f.LabelId))
                .OrderBy(f => f.SortOrder)
                .ToListAsync();

            if (fields.Count == 0)
            {
                logger.LogInformation($"Label {document.LabelId} không có cấu hình MetadataField nào.");
                return "{}";
            }

            // 2. Chuẩn bị Field Definition
            var fieldDefinitions = fields.Select(f => new
            {
                key = f.FieldKey,
                name = f.FieldName,
                type = f.FieldType.ToString(),
                required = f.IsRequired,
                instruction = f.LlmExtractionPrompt,
                options = !string.IsNullOrEmpty(f.Options) ? JsonSerializer.Deserialize<List<string>>(f.Options) : null
            }).ToList();

            string fieldsJson = JsonSerializer.Serialize(fieldDefinitions, new JsonSerializerOptions { WriteIndented = true });

            // 3. Gộp toàn bộ text sạch (đọc từ MinIO .md nếu có, fallback sang OcrResult)
            var fullText = "";
            foreach (var page in pages.OrderBy(p => p.PageNumber))
            {
                string pageText = await GetCleanTextAsync(page);
                if (!string.IsNullOrWhiteSpace(pageText))
                {
                    fullText += pageText + "\n\n";
                }
            }

            if (string.IsNullOrWhiteSpace(fullText)) return "{}";

            // 4. Gọi LLM
            string basePrompt = @"Bạn là chuyên gia bóc tách thông tin từ văn bản OCR.
Bạn sẽ nhận được định nghĩa các trường dữ liệu cần bóc tách (định dạng JSON) và văn bản OCR gốc.
Nhiệm vụ của bạn là tìm thông tin tương ứng cho mỗi trường dữ liệu.

Định nghĩa các trường (Fields Definition):
" + fieldsJson + @"

Yêu cầu KẾT QUẢ ĐẦU RA CHỈ ĐƯỢC PHÉP LÀ MỘT JSON OBJECT duy nhất, trong đó mỗi key là `key` của trường dữ liệu, value là giá trị bóc tách được.
TUYỆT ĐỐI KHÔNG thêm bất kỳ giải thích, chú thích hoặc ký tự markdown nào khác.
- Nếu trường không có thông tin, hãy gán giá trị null.
- Định dạng Date: trả về chuỗi yyyy-MM-dd (nếu có thể nhận diện).
- Định dạng Number: trả về SỐ thực hoặc số nguyên (không phải chuỗi).
- Định dạng MultiSelect (nhiều lựa chọn): trả về MẢNG các chuỗi (ví dụ: [""A"", ""B""]).
- Chú ý các chỉ dẫn đặc biệt (instruction) của từng trường nếu có.";

            string systemPrompt = !string.IsNullOrWhiteSpace(document.Batch?.Project?.ExtractionPrompt)
                ? document.Batch.Project.ExtractionPrompt + "\n\n" + basePrompt
                : basePrompt;

            string userPrompt = "Văn bản OCR:\n" + fullText;

            logger.LogInformation($"[Extraction] Gửi yêu cầu LLM Extract Metadata cho Document {document.Id}. Fields: {fields.Count}. Text length: {fullText.Length}");
            
            string rawResponseJson = await llmService.CallLlmAsync(systemPrompt, userPrompt);
            logger.LogInformation($"[Extraction] Document {document.Id} - Raw LLM Response:\n{rawResponseJson}");
            
            // Clean markdown formatting if present
            string responseJson = rawResponseJson;
            int startObj = responseJson.IndexOf('{');
            int endObj = responseJson.LastIndexOf('}');
            int startArr = responseJson.IndexOf('[');
            int endArr = responseJson.LastIndexOf(']');

            if (startObj >= 0 && endObj > startObj)
            {
                if (startArr >= 0 && endArr > startArr && startArr < startObj && endArr > endObj)
                    responseJson = responseJson.Substring(startArr, endArr - startArr + 1);
                else
                    responseJson = responseJson.Substring(startObj, endObj - startObj + 1);
            }
            else if (startArr >= 0 && endArr > startArr)
            {
                 responseJson = responseJson.Substring(startArr, endArr - startArr + 1);
            }
            else
            {
                responseJson = "{}";
            }
            logger.LogInformation($"[Extraction] Document {document.Id} - Cleaned JSON:\n{responseJson}");

            // Validate JSON
            try
            {
                using var jsonDoc = JsonDocument.Parse(responseJson);
                // Đảm bảo là Object
                if (jsonDoc.RootElement.ValueKind != JsonValueKind.Object)
                {
                    return "{}";
                }
                
                var resultDict = new Dictionary<string, object?>();
                foreach (var prop in jsonDoc.RootElement.EnumerateObject())
                {
                    var fieldDef = fields.FirstOrDefault(f => f.FieldKey == prop.Name);
                    if (fieldDef == null)
                    {
                        resultDict[prop.Name] = prop.Value.ValueKind == JsonValueKind.Null ? null : prop.Value.ToString();
                        continue;
                    }

                    if (prop.Value.ValueKind == JsonValueKind.Null)
                    {
                        resultDict[prop.Name] = null;
                        continue;
                    }

                    switch (fieldDef.FieldType)
                    {
                        case FieldType.Number:
                            if (prop.Value.ValueKind == JsonValueKind.Number)
                            {
                                resultDict[prop.Name] = prop.Value.GetDouble();
                            }
                            else
                            {
                                string strVal = prop.Value.ToString() ?? "";
                                if (double.TryParse(strVal.Replace(",", "."), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double num))
                                    resultDict[prop.Name] = num;
                                else
                                    resultDict[prop.Name] = strVal; // Fallback
                            }
                            break;
                        case FieldType.MultiSelect:
                            if (prop.Value.ValueKind == JsonValueKind.Array)
                            {
                                var arr = new List<string>();
                                foreach (var item in prop.Value.EnumerateArray())
                                    arr.Add(item.ToString());
                                resultDict[prop.Name] = arr;
                            }
                            else
                            {
                                string strVal = prop.Value.ToString() ?? "";
                                resultDict[prop.Name] = strVal.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                                                             .Select(s => s.Trim()).ToList();
                            }
                            break;
                        case FieldType.Date:
                            string dateStr = prop.Value.ToString() ?? "";
                            if (DateTime.TryParse(dateStr, out DateTime parsedDate))
                            {
                                resultDict[prop.Name] = parsedDate.ToString("yyyy-MM-dd");
                            }
                            else
                            {
                                resultDict[prop.Name] = dateStr; // Fallback
                            }
                            break;
                        case FieldType.Select:
                            string selectStr = prop.Value.ToString() ?? "";
                            if (!string.IsNullOrEmpty(fieldDef.Options))
                            {
                                try
                                {
                                    var opts = JsonSerializer.Deserialize<List<string>>(fieldDef.Options);
                                    if (opts != null)
                                    {
                                        var matched = opts.FirstOrDefault(o => string.Equals(o, selectStr, StringComparison.OrdinalIgnoreCase));
                                        resultDict[prop.Name] = matched ?? selectStr; // Fallback to matched exact case, or raw string
                                    }
                                    else
                                    {
                                        resultDict[prop.Name] = selectStr;
                                    }
                                }
                                catch
                                {
                                    resultDict[prop.Name] = selectStr;
                                }
                            }
                            else
                            {
                                resultDict[prop.Name] = selectStr;
                            }
                            break;
                        case FieldType.Text:
                        default:
                            // Keep as string
                            resultDict[prop.Name] = prop.Value.ToString();
                            break;
                    }
                }
                return JsonSerializer.Serialize(resultDict);
            }
            catch
            {
                logger.LogWarning($"LLM trả về JSON không hợp lệ cho Document {document.Id}");
                return "{}";
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"Lỗi Extraction Document {document.Id}");
            return null;
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
}
