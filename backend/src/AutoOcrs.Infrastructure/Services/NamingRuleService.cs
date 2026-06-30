using System.Text.Json;
using System.Text.RegularExpressions;
using AutoOcrs.Core.Entities;

using AutoOcrs.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using AutoOcrs.Core.DTOs.NamingRules;

namespace AutoOcrs.Infrastructure.Services;

public class NamingRuleService(AppDbContext db)
{
    public async Task<object> GetRuleAsync(Guid projectId)
    {
        var project = await db.Projects.FindAsync(projectId) ?? throw new Exception("Không tìm thấy dự án.");
        return new { projectId = project.Id, namingRule = project.NamingRule, folderRule = project.FolderRule };
    }

    public async Task<object> UpdateRuleAsync(Guid projectId, UpdateNamingRuleRequest request)
    {
        var project = await db.Projects.FindAsync(projectId) ?? throw new Exception("Không tìm thấy dự án.");
        project.NamingRule = request.NamingRule;
        project.FolderRule = request.FolderRule;
        await db.SaveChangesAsync();
        return new { projectId = project.Id, namingRule = project.NamingRule, folderRule = project.FolderRule };
    }

    public async Task<object> PreviewAsync(Guid projectId, PreviewNamingRuleRequest request)
    {
        var project = await db.Projects.FindAsync(projectId) ?? throw new Exception("Không tìm thấy dự án.");
        
        // Mock a document
        var doc = new Document 
        { 
            Id = Guid.NewGuid(), 
            OriginalFilename = "sample.pdf",
            ReviewedMetadata = request.MockMetadata?.ToString()
        };

        var tempProject = new Project { NamingRule = request.NamingRule, FolderRule = request.FolderRule };
        string fileName = GenerateFilename(doc, tempProject);
        string folderPath = GenerateFolderPath(doc, tempProject);
        string fullPath = string.IsNullOrEmpty(folderPath) ? fileName : $"{folderPath}/{fileName}";

        return new { fileName, folderPath, fullPath };
    }

    public string GenerateFolderPath(Document document, Project project)
    {
        if (string.IsNullOrEmpty(project.FolderRule))
        {
            return document.BatchId.ToString(); // Mặc định là BatchId
        }

        string folderPath = project.FolderRule;

        Dictionary<string, string> metadataMap = new();
        if (!string.IsNullOrEmpty(document.ReviewedMetadata))
        {
            try
            {
                var textOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var parsedData = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(document.ReviewedMetadata, textOptions);
                if (parsedData != null)
                {
                    foreach (var kvp in parsedData)
                    {
                        metadataMap[kvp.Key] = kvp.Value.ValueKind == JsonValueKind.Null ? "" : kvp.Value.ToString() ?? "";
                    }
                }
            }
            catch { /* Lỗi parse thì bỏ qua */ }
        }

        var regex = new Regex(@"\{([^}]+)\}");
        folderPath = regex.Replace(folderPath, match =>
        {
            string key = match.Groups[1].Value.Trim();

            // Biến hệ thống (ví dụ: ngày giờ)
            if (key.Equals("current_date", StringComparison.OrdinalIgnoreCase))
                return DateTime.Now.ToString("yyyyMMdd");
            if (key.Equals("Year", StringComparison.OrdinalIgnoreCase))
                return DateTime.Now.ToString("yyyy");
            if (key.Equals("Month", StringComparison.OrdinalIgnoreCase))
                return DateTime.Now.ToString("MM");
            if (key.Equals("Day", StringComparison.OrdinalIgnoreCase))
                return DateTime.Now.ToString("dd");
            if (key.Equals("ProjectName", StringComparison.OrdinalIgnoreCase))
                return SanitizeFilename(project.Name ?? "Project");
            if (key.Equals("LabelCode", StringComparison.OrdinalIgnoreCase))
                return document.Label?.Code ?? "Unknown";

            // Biến metadata
            if (metadataMap.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
            {
                return SanitizeFilename(value);
            }

            return "Unknown";
        });

        // Thay thế multiple slashes
        folderPath = Regex.Replace(folderPath, @"/+", "/").Trim('/');
        return folderPath;
    }
    public string GenerateFilename(Document document, Project project)
    {
        if (string.IsNullOrEmpty(project.NamingRule))
        {
            return $"Approved_{document.OriginalFilename}";
        }

        string filename = project.NamingRule;
        
        // 1. Phân tích ReviewedMetadata
        Dictionary<string, string> metadataMap = new();
        if (!string.IsNullOrEmpty(document.ReviewedMetadata))
        {
            try
            {
                var textOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var parsedData = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(document.ReviewedMetadata, textOptions);
                if (parsedData != null)
                {
                    foreach (var kvp in parsedData)
                    {
                        metadataMap[kvp.Key] = kvp.Value.ValueKind == JsonValueKind.Null ? "" : kvp.Value.ToString() ?? "";
                    }
                }
            }
            catch { /* Lỗi parse thì bỏ qua */ }
        }

        // 2. Thay thế các field key trong chuỗi rule. VD: "{ho_ten}_{ngay_sinh}.pdf"
        var regex = new Regex(@"\{([^}]+)\}");
        filename = regex.Replace(filename, match =>
        {
            string key = match.Groups[1].Value.Trim();

            // Biến hệ thống
            if (key.Equals("current_date", StringComparison.OrdinalIgnoreCase))
                return DateTime.Now.ToString("yyyyMMdd");
            if (key.Equals("Year", StringComparison.OrdinalIgnoreCase))
                return DateTime.Now.ToString("yyyy");
            if (key.Equals("Month", StringComparison.OrdinalIgnoreCase))
                return DateTime.Now.ToString("MM");
            if (key.Equals("Day", StringComparison.OrdinalIgnoreCase))
                return DateTime.Now.ToString("dd");
            if (key.Equals("ProjectName", StringComparison.OrdinalIgnoreCase))
                return SanitizeFilename(project.Name ?? "Project");
            if (key.Equals("LabelCode", StringComparison.OrdinalIgnoreCase))
                return document.Label?.Code ?? "Unknown";
            if (key.Equals("original_name", StringComparison.OrdinalIgnoreCase) || key.Equals("OriginalFileName", StringComparison.OrdinalIgnoreCase))
                return Path.GetFileNameWithoutExtension(document.OriginalFilename);
            if (key.Equals("auto_id", StringComparison.OrdinalIgnoreCase) || key.Equals("AutoIncrement", StringComparison.OrdinalIgnoreCase))
                return document.Id.ToString()[..8]; // 8 chars of GUID for now
                
            // Biến metadata
            if (metadataMap.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
            {
                // Làm sạch value tránh chứa ký tự cấm của file
                return SanitizeFilename(value);
            }

            return "Unknown";
        });

        // Đảm bảo đuôi mở rộng
        if (!filename.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
        {
            filename += ".pdf";
        }

        return SanitizeFilename(filename, true);
    }

    private string SanitizeFilename(string name, bool allowDot = false)
    {
        string invalidChars = new string(Path.GetInvalidFileNameChars()) + new string(Path.GetInvalidPathChars());
        if (!allowDot) invalidChars += ".";
        
        foreach (char c in invalidChars)
        {
            name = name.Replace(c.ToString(), "");
        }
        return name;
    }
}
