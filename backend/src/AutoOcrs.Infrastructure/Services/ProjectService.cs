using AutoOcrs.Core.DTOs.Common;
using AutoOcrs.Core.DTOs.Projects;
using AutoOcrs.Core.Entities;
using AutoOcrs.Core.Enums;
using AutoOcrs.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AutoOcrs.Infrastructure.Services;

/// <summary>CRUD quản lý dự án</summary>
public class ProjectService(AppDbContext db)
{
    public async Task<PagedResult<ProjectResponse>> GetAllAsync(int page = 1, int pageSize = 20, string? search = null)
    {
        var query = db.Projects.Where(p => p.Status == ProjectStatus.Active);
        if (!string.IsNullOrEmpty(search))
            query = query.Where(p => p.Name.Contains(search));

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(p => p.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Include(p => p.Creator)
            .Select(p => new ProjectResponse(
                p.Id, p.Name, p.Description, p.SourcePath, p.OutputPath,
                p.NamingRule, p.FolderRule, p.ClassificationPrompt, p.ExtractionPrompt, p.Status.ToString(),
                p.Creator!.FullName, p.CreatedAt,
                p.Batches.Count,
                p.Batches.SelectMany(b => b.Documents).Count(),
                p.Batches.SelectMany(b => b.Documents).Count(d => d.Status == DocumentStatus.Approved)
            ))
            .ToListAsync();

        return new PagedResult<ProjectResponse>(items, total, page, pageSize);
    }

    public async Task<ProjectResponse?> GetByIdAsync(Guid id)
    {
        return await db.Projects
            .Where(p => p.Id == id)
            .Include(p => p.Creator)
            .Select(p => new ProjectResponse(
                p.Id, p.Name, p.Description, p.SourcePath, p.OutputPath,
                p.NamingRule, p.FolderRule, p.ClassificationPrompt, p.ExtractionPrompt, p.Status.ToString(),
                p.Creator!.FullName, p.CreatedAt,
                p.Batches.Count,
                p.Batches.SelectMany(b => b.Documents).Count(),
                p.Batches.SelectMany(b => b.Documents).Count(d => d.Status == DocumentStatus.Approved)
            ))
            .FirstOrDefaultAsync();
    }

    public async Task<ProjectResponse> CreateAsync(CreateProjectRequest request, Guid userId)
    {
        var project = new Project
        {
            Name = request.Name,
            Description = request.Description,
            SourcePath = request.SourcePath,
            OutputPath = request.OutputPath,
            ClassificationPrompt = request.ClassificationPrompt,
            ExtractionPrompt = request.ExtractionPrompt,
            CreatedBy = userId
        };

        db.Projects.Add(project);
        await db.SaveChangesAsync();

        var user = await db.Users.FindAsync(userId);
        return new ProjectResponse(
            project.Id, project.Name, project.Description, project.SourcePath, project.OutputPath,
            null, null, project.ClassificationPrompt, project.ExtractionPrompt, project.Status.ToString(),
            user?.FullName ?? "", project.CreatedAt, 0, 0, 0);
    }

    public async Task<bool> UpdateAsync(Guid id, UpdateProjectRequest request)
    {
        var project = await db.Projects.FindAsync(id);
        if (project == null) return false;

        if (request.Name != null) project.Name = request.Name;
        if (request.Description != null) project.Description = request.Description;
        if (request.SourcePath != null) project.SourcePath = request.SourcePath;
        if (request.OutputPath != null) project.OutputPath = request.OutputPath;
        if (request.ClassificationPrompt != null) project.ClassificationPrompt = request.ClassificationPrompt;
        if (request.ExtractionPrompt != null) project.ExtractionPrompt = request.ExtractionPrompt;

        await db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var project = await db.Projects.FindAsync(id);
        if (project == null) return false;

        project.Status = ProjectStatus.Archived;
        await db.SaveChangesAsync();
        return true;
    }
}
