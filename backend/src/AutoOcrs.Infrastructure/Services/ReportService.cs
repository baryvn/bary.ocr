using System.Text.Json;
using AutoOcrs.Core.Entities;
using AutoOcrs.Core.Enums;
using AutoOcrs.Infrastructure.Data;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;

namespace AutoOcrs.Infrastructure.Services;

public class ReportService(AppDbContext db)
{
    public async Task<object> GetDashboardStatsAsync()
    {
        var totalProjects = await db.Projects.CountAsync();
        var totalBatches = await db.Batches.CountAsync();
        var totalDocs = await db.Documents.CountAsync();
        
        var docsByStatus = await db.Documents
            .GroupBy(d => d.Status)
            .Select(g => new { Status = g.Key.ToString(), Count = g.Count() })
            .ToListAsync();

        var recentActivities = await db.AuditLogs
            .Include(a => a.User)
            .OrderByDescending(a => a.CreatedAt)
            .Take(10)
            .Select(a => new
            {
                a.Action,
                a.EntityType,
                a.CreatedAt,
                User = a.User != null ? a.User.FullName : "System"
            })
            .ToListAsync();

        return new
        {
            TotalProjects = totalProjects,
            TotalBatches = totalBatches,
            TotalDocuments = totalDocs,
            DocumentsByStatus = docsByStatus,
            RecentActivities = recentActivities
        };
    }

    public async Task<object> GetProjectSummaryAsync(Guid projectId)
    {
        var totalDocs = await db.Documents.CountAsync(d => d.Batch != null && d.Batch.ProjectId == projectId);
        var docsByStatus = await db.Documents
            .Where(d => d.Batch != null && d.Batch.ProjectId == projectId)
            .GroupBy(d => d.Status)
            .Select(g => new { Status = g.Key.ToString(), Count = g.Count() })
            .ToListAsync();

        var docsByLabel = await db.Documents
            .Where(d => d.Batch != null && d.Batch.ProjectId == projectId && d.LabelId != null)
            .GroupBy(d => d.Label!.Name)
            .Select(g => new { Label = g.Key, Count = g.Count() })
            .ToListAsync();

        var avgConfidence = await db.Documents
            .Where(d => d.Batch != null && d.Batch.ProjectId == projectId && d.ClassificationConfidence != null)
            .AverageAsync(d => d.ClassificationConfidence);

        return new
        {
            TotalDocuments = totalDocs,
            DocumentsByStatus = docsByStatus,
            DocumentsByLabel = docsByLabel,
            AverageConfidence = avgConfidence
        };
    }

    public async Task<byte[]> ExportExcelAsync(Guid? projectId = null, Guid? batchId = null)
    {
        var query = db.Documents
            .Include(d => d.Label)
            .Include(d => d.Reviewer)
            .AsQueryable();

        if (batchId.HasValue)
        {
            query = query.Where(d => d.BatchId == batchId.Value);
        }
        else if (projectId.HasValue)
        {
            query = query.Where(d => d.Batch != null && d.Batch.ProjectId == projectId.Value);
        }

        var documents = await query.OrderByDescending(d => d.CreatedAt).ToListAsync();

        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Report");

        // Fixed Headers
        worksheet.Cell(1, 1).Value = "STT";
        worksheet.Cell(1, 2).Value = "File gốc";
        worksheet.Cell(1, 3).Value = "File mới";
        worksheet.Cell(1, 4).Value = "Số trang";
        worksheet.Cell(1, 5).Value = "Nhãn (Label)";
        worksheet.Cell(1, 6).Value = "Độ tin cậy";
        worksheet.Cell(1, 7).Value = "Trạng thái";
        worksheet.Cell(1, 8).Value = "Người duyệt";
        worksheet.Cell(1, 9).Value = "Ngày duyệt";
        worksheet.Cell(1, 10).Value = "Ghi chú";

        // Dynamic Metadata Columns
        int fixedColCount = 10;
        Dictionary<string, int> metadataColumnMap = new();

        int row = 2;
        foreach (var doc in documents)
        {
            worksheet.Cell(row, 1).Value = row - 1;
            worksheet.Cell(row, 2).Value = doc.OriginalFilename;
            worksheet.Cell(row, 3).Value = doc.OutputFilename ?? "";
            worksheet.Cell(row, 4).Value = doc.PageCount;
            worksheet.Cell(row, 5).Value = doc.Label?.Name ?? "";
            worksheet.Cell(row, 6).Value = doc.ClassificationConfidence.HasValue ? Math.Round(doc.ClassificationConfidence.Value * 100, 2) + "%" : "";
            worksheet.Cell(row, 7).Value = doc.Status.ToString();
            worksheet.Cell(row, 8).Value = doc.Reviewer?.FullName ?? "";
            worksheet.Cell(row, 9).Value = doc.ReviewedAt?.ToString("yyyy-MM-dd HH:mm:ss") ?? "";
            worksheet.Cell(row, 10).Value = doc.ReviewerNote ?? "";

            // Parse metadata
            if (!string.IsNullOrEmpty(doc.ReviewedMetadata) || !string.IsNullOrEmpty(doc.ExtractedMetadata))
            {
                string jsonStr = doc.ReviewedMetadata ?? doc.ExtractedMetadata!;
                try
                {
                    var metaDict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(jsonStr);
                    if (metaDict != null)
                    {
                        foreach (var kvp in metaDict)
                        {
                            string key = kvp.Key;
                            string val = kvp.Value.ValueKind == JsonValueKind.Null ? "" : kvp.Value.ToString() ?? "";

                            if (!metadataColumnMap.ContainsKey(key))
                            {
                                fixedColCount++;
                                metadataColumnMap[key] = fixedColCount;
                                worksheet.Cell(1, fixedColCount).Value = key; // Add header dynamically
                            }

                            worksheet.Cell(row, metadataColumnMap[key]).Value = val;
                        }
                    }
                }
                catch { /* Ignore parse error */ }
            }

            row++;
        }

        // Style header
        var headerRange = worksheet.Range(1, 1, 1, fixedColCount > 10 ? fixedColCount : 10);
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Fill.BackgroundColor = XLColor.LightGray;
        
        worksheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }
}
