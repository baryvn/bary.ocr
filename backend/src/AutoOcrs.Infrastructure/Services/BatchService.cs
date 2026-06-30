using AutoOcrs.Core.DTOs.Batches;
using AutoOcrs.Core.DTOs.Common;
using AutoOcrs.Core.DTOs.Documents;
using AutoOcrs.Core.Entities;
using AutoOcrs.Core.Enums;
using AutoOcrs.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AutoOcrs.Infrastructure.Services;

public class BatchService(AppDbContext db, MinioStorageService minioService, NamingRuleService namingService)
{
    public async Task<PagedResult<BatchResponse>> GetBatchesAsync(Guid projectId, int page = 1, int pageSize = 20)
    {
        var query = db.Batches.Where(b => b.ProjectId == projectId);
        var total = await query.CountAsync();
        
        var items = await query
            .Include(b => b.Documents)
            .Include(b => b.ReviewAssignments)
            .ThenInclude(r => r.Reviewer)
            .OrderByDescending(b => b.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
            
        // Auto-fix stats for display
        foreach(var b in items) {
            b.ApprovedFiles = b.Documents.Count(d => d.Status == DocumentStatus.Approved);
            b.ProcessedFiles = b.Documents.Count(d => d.Status >= DocumentStatus.ReadyForReview || d.Status == DocumentStatus.Error);
            if (b.ApprovedFiles >= b.TotalFiles && b.TotalFiles > 0)
                b.Status = BatchStatus.Completed;
            else if (b.ProcessedFiles >= b.TotalFiles && b.TotalFiles > 0 && b.Status < BatchStatus.ReadyForReview)
                b.Status = BatchStatus.ReadyForReview;
        }

        var responseItems = items.Select(b => new BatchResponse(
            b.Id, b.ProjectId, b.Name, b.SourceFolder, 
            b.TotalFiles, b.ProcessedFiles, b.ApprovedFiles, 
            b.Status.ToString(), b.CreatedAt,
            b.ReviewAssignments.OrderByDescending(r => r.CreatedAt).Select(r => r.Reviewer!.FullName).FirstOrDefault()
        )).ToList();

        return new PagedResult<BatchResponse>(responseItems, total, page, pageSize);
    }

    public async Task<BatchResponse> CreateBatchAsync(Guid projectId, CreateBatchRequest request)
    {
        var project = await db.Projects.FindAsync(projectId) ?? throw new Exception("Không tìm thấy dự án.");

        var exists = await db.Batches.AnyAsync(b => b.ProjectId == projectId && b.Name == request.Name);
        if (exists)
        {
            throw new Exception("Tên lô đã tồn tại trong dự án này. Vui lòng chọn tên khác.");
        }

        int totalFiles = request.UploadedFileKeys?.Count ?? 0;
        
        var batch = new Batch
        {
            ProjectId = projectId,
            Name = request.Name,
            SourceFolder = request.SourceFolder,
            TotalFiles = totalFiles,
            Status = BatchStatus.Created
        };

        db.Batches.Add(batch);

        if (request.UploadedFileKeys != null && request.UploadedFileKeys.Any())
        {
            foreach (var key in request.UploadedFileKeys)
            {
                var originalFilename = key.Split('/').LastOrDefault() ?? key;
                var doc = new Document
                {
                    BatchId = batch.Id,
                    OriginalFilename = originalFilename,
                    StorageKey = key,
                    Status = DocumentStatus.Pending
                };

                // Move file on MinIO to follow NamingRule and FolderRule
                try
                {
                    string folderPath = namingService.GenerateFolderPath(doc, project);
                    string newName = namingService.GenerateFilename(doc, project);
                    string p = string.IsNullOrEmpty(project.SourcePath) ? "uploads" : project.SourcePath;
                    string targetKey = $"{p.Trim('/')}/{folderPath}/{newName}";
                    
                    if (key != targetKey)
                    {
                        await minioService.MoveFileAsync(key, targetKey);
                        doc.StorageKey = targetKey;
                        doc.OriginalFilename = newName; // Cập nhật tên mới để UI hiển thị theo cấu hình
                    }
                }
                catch
                {
                    // If move fails, just keep the original key
                }

                db.Documents.Add(doc);
            }
        }

        await db.SaveChangesAsync();

        return new BatchResponse(
            batch.Id, batch.ProjectId, batch.Name, batch.SourceFolder, 
            batch.TotalFiles, batch.ProcessedFiles, batch.ApprovedFiles, 
            batch.Status.ToString(), batch.CreatedAt, null);
    }

    public async Task<BatchResponse> AddDocumentsAsync(Guid batchId, AddDocumentsRequest request)
    {
        var batch = await db.Batches.Include(b => b.Project).FirstOrDefaultAsync(b => b.Id == batchId) ?? throw new Exception("Không tìm thấy lô.");
        
        if (request.UploadedFileKeys != null && request.UploadedFileKeys.Any())
        {
            foreach (var key in request.UploadedFileKeys)
            {
                var originalFilename = key.Split('/').LastOrDefault() ?? key;
                var doc = new Document
                {
                    BatchId = batch.Id,
                    OriginalFilename = originalFilename,
                    StorageKey = key,
                    Status = DocumentStatus.Pending
                };

                // Move file on MinIO to follow NamingRule and FolderRule
                if (batch.Project != null)
                {
                    try
                    {
                        string folderPath = namingService.GenerateFolderPath(doc, batch.Project);
                        string newName = namingService.GenerateFilename(doc, batch.Project);
                        string p = string.IsNullOrEmpty(batch.Project.SourcePath) ? "uploads" : batch.Project.SourcePath;
                        string targetKey = $"{p.Trim('/')}/{folderPath}/{newName}";
                        
                        if (key != targetKey)
                        {
                            await minioService.MoveFileAsync(key, targetKey);
                            doc.StorageKey = targetKey;
                            doc.OriginalFilename = newName; // Cập nhật tên mới để UI hiển thị theo cấu hình
                        }
                    }
                    catch
                    {
                        // If move fails, just keep the original key
                    }
                }

                db.Documents.Add(doc);
                batch.TotalFiles++;
            }
            
            // Nếu lô đang ở trạng thái Completed/ReadyForReview, đẩy về lại trạng thái Created hoặc tương ứng
            if (batch.Status == BatchStatus.Completed || batch.Status == BatchStatus.ReadyForReview)
            {
                batch.Status = BatchStatus.Importing; // hoặc Created
            }

            await db.SaveChangesAsync();
        }

        return await GetBatchByIdAsync(batchId);
    }

    public async Task<BatchResponse> GetBatchByIdAsync(Guid batchId)
    {
        var b = await db.Batches.Include(b => b.Documents).FirstOrDefaultAsync(b => b.Id == batchId) ?? throw new Exception("Không tìm thấy lô.");
        
        // Auto-fix stats for display
        b.ApprovedFiles = b.Documents.Count(d => d.Status == DocumentStatus.Approved);
        b.ProcessedFiles = b.Documents.Count(d => d.Status >= DocumentStatus.ReadyForReview || d.Status == DocumentStatus.Error);
        
        if (b.ApprovedFiles >= b.TotalFiles && b.TotalFiles > 0)
            b.Status = BatchStatus.Completed;
        else if (b.ProcessedFiles >= b.TotalFiles && b.TotalFiles > 0 && b.Status < BatchStatus.ReadyForReview)
            b.Status = BatchStatus.ReadyForReview;
            
        return new BatchResponse(
            b.Id, b.ProjectId, b.Name, b.SourceFolder, 
            b.TotalFiles, b.ProcessedFiles, b.ApprovedFiles, 
            b.Status.ToString(), b.CreatedAt, null);
    }

    public async Task<PagedResult<DocumentResponse>> GetDocumentsAsync(Guid batchId, int page = 1, int pageSize = 20)
    {
        var query = db.Documents
            .Include(d => d.Label)
            .Where(d => d.BatchId == batchId);
            
        var total = await query.CountAsync();
        
        var items = await query
            .OrderBy(d => d.OriginalFilename)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(d => new DocumentResponse(
                d.Id, d.OriginalFilename, d.Status.ToString(), d.PageCount, 
                d.LabelId, d.Label != null ? d.Label.Name : null, 
                d.ClassificationConfidence, d.ErrorMessage, d.ProcessingMessage, 
                d.CreatedAt, d.UpdatedAt))
            .ToListAsync();

        return new PagedResult<DocumentResponse>(items, total, page, pageSize);
    }

    public async Task<BatchProgressResponse> GetProgressAsync(Guid batchId)
    {
        var docs = await db.Documents.Where(d => d.BatchId == batchId).ToListAsync();

        return new BatchProgressResponse(
            TotalFiles: docs.Count,
            ProcessedFiles: docs.Count(d => d.Status >= DocumentStatus.Extracting), // Ví dụ
            ApprovedFiles: docs.Count(d => d.Status == DocumentStatus.Approved),
            Errors: docs.Count(d => d.Status == DocumentStatus.Error),
            Classifying: docs.Count(d => d.Status == DocumentStatus.Classifying),
            Extracting: docs.Count(d => d.Status == DocumentStatus.Extracting),
            Reviewing: docs.Count(d => d.Status == DocumentStatus.Reviewing || d.Status == DocumentStatus.ReadyForReview)
        );
    }

    public async Task ProcessBatchAsync(Guid batchId, OcrJobPublisher publisher)
    {
        var batch = await db.Batches.FindAsync(batchId) ?? throw new Exception("Không tìm thấy lô.");
        
        var docs = await db.Documents
            .Where(d => d.BatchId == batchId && (d.Status == DocumentStatus.Pending || d.Status == DocumentStatus.Error))
            .ToListAsync();

        batch.Status = BatchStatus.Importing;

        foreach (var doc in docs)
        {
            doc.Status = DocumentStatus.Importing;
            // Publish message
            if (!string.IsNullOrEmpty(doc.StorageKey))
            {
                await publisher.PublishJobAsync(doc.Id, batch.ProjectId, doc.StorageKey);
            }
        }

        await db.SaveChangesAsync();
    }

    public async Task<BatchResponse> UpdateBatchAsync(Guid batchId, UpdateBatchRequest request)
    {
        var batch = await db.Batches.FindAsync(batchId) ?? throw new Exception("Không tìm thấy lô.");
        
        var exists = await db.Batches.AnyAsync(b => b.ProjectId == batch.ProjectId && b.Name == request.Name && b.Id != batchId);
        if (exists)
        {
            throw new Exception("Tên lô đã tồn tại trong dự án này. Vui lòng chọn tên khác.");
        }

        batch.Name = request.Name;
        batch.SourceFolder = request.SourceFolder;
        
        await db.SaveChangesAsync();

        return new BatchResponse(
            batch.Id, batch.ProjectId, batch.Name, batch.SourceFolder, 
            batch.TotalFiles, batch.ProcessedFiles, batch.ApprovedFiles, 
            batch.Status.ToString(), batch.CreatedAt, null);
    }

    public async Task DeleteBatchAsync(Guid batchId)
    {
        var batch = await db.Batches.FindAsync(batchId) ?? throw new Exception("Không tìm thấy lô.");
        
        // Find all documents
        var docs = await db.Documents.Where(d => d.BatchId == batchId).ToListAsync();
        
        // Delete physical files from MinIO
        foreach (var doc in docs)
        {
            if (!string.IsNullOrEmpty(doc.StorageKey))
            {
                try { await minioService.DeleteFileAsync(doc.StorageKey); } catch {}
            }
            if (!string.IsNullOrEmpty(doc.OcrPdfKey))
            {
                try { await minioService.DeleteFileAsync(doc.OcrPdfKey); } catch {}
            }
        }

        db.Batches.Remove(batch);
        await db.SaveChangesAsync();
    }
}
