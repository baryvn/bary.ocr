using AutoOcrs.Core.DTOs.Labels;
using AutoOcrs.Core.Entities;
using AutoOcrs.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AutoOcrs.Infrastructure.Services;

public class LabelService(AppDbContext db)
{
    public async Task<List<LabelResponse>> GetTreeAsync(Guid projectId)
    {
        var allLabels = await db.Labels
            .Where(l => l.ProjectId == projectId)
            .OrderBy(l => l.SortOrder)
            .ToListAsync();

        return BuildTree(allLabels, null);
    }

    public async Task<List<LabelResponse>> GetFlatListAsync(Guid projectId)
    {
        var allLabels = await db.Labels
            .Where(l => l.ProjectId == projectId && l.IsActive)
            .OrderBy(l => l.Level).ThenBy(l => l.SortOrder)
            .ToListAsync();

        return allLabels.Select(MapToResponse).ToList();
    }

    public async Task<LabelResponse> CreateAsync(CreateLabelRequest request)
    {
        int level = 1;
        if (request.ParentId.HasValue)
        {
            var parent = await db.Labels.FindAsync(request.ParentId.Value) 
                ?? throw new Exception("Không tìm thấy nhãn cha.");
            if (parent.ProjectId != request.ProjectId)
                throw new Exception("Nhãn cha không thuộc cùng một dự án.");
            
            level = parent.Level + 1;
            if (level > 3)
                throw new Exception("Hệ thống chỉ hỗ trợ cây nhãn tối đa 3 cấp.");
        }

        var label = new Label
        {
            ProjectId = request.ProjectId,
            ParentId = request.ParentId,
            Name = request.Name,
            Code = request.Code,
            Level = level,
            SortOrder = request.SortOrder ?? (await GetMaxSortOrderAsync(request.ProjectId, request.ParentId) + 1),
            Description = request.Description,
            LlmPrompt = request.LlmPrompt,
            IsActive = true
        };

        db.Labels.Add(label);
        await db.SaveChangesAsync();

        return MapToResponse(label);
    }

    public async Task<LabelResponse> UpdateAsync(Guid id, UpdateLabelRequest request)
    {
        var label = await db.Labels.FindAsync(id) ?? throw new Exception("Không tìm thấy nhãn.");

        label.Name = request.Name;
        label.Code = request.Code;
        label.Description = request.Description;
        label.LlmPrompt = request.LlmPrompt;
        label.IsActive = request.IsActive;
        label.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();
        return MapToResponse(label);
    }

    public async Task DeleteAsync(Guid id)
    {
        var label = await db.Labels.FindAsync(id) ?? throw new Exception("Không tìm thấy nhãn.");
        
        // Check documents (Assuming Document has LabelId)
        bool hasDocuments = await db.Documents.AnyAsync(d => d.LabelId == id);
        if (hasDocuments)
            throw new Exception("Không thể xóa nhãn đang có tài liệu đính kèm.");

        // Cascade delete is handled by EF Core (OnDelete Cascade)
        // But we should also check if any children have documents
        await CheckChildrenDocumentsAsync(id);

        db.Labels.Remove(label);
        await db.SaveChangesAsync();
    }

    public async Task MoveAsync(Guid id, MoveLabelRequest request)
    {
        var label = await db.Labels.FindAsync(id) ?? throw new Exception("Không tìm thấy nhãn.");
        if (label.ParentId == request.NewParentId && label.SortOrder == request.NewSortOrder)
            return; // No change

        int newLevel = 1;
        if (request.NewParentId.HasValue)
        {
            if (request.NewParentId.Value == id)
                throw new Exception("Nhãn không thể là cha của chính nó.");

            var parent = await db.Labels.FindAsync(request.NewParentId.Value) 
                ?? throw new Exception("Không tìm thấy nhãn cha mới.");
            
            if (parent.ProjectId != label.ProjectId)
                throw new Exception("Nhãn cha mới không thuộc cùng dự án.");
            
            // Check circular reference (parent is not a descendant of current label)
            if (await IsDescendantAsync(id, request.NewParentId.Value))
                throw new Exception("Không thể di chuyển nhãn vào một nhãn con của nó.");

            newLevel = parent.Level + 1;
        }

        // Calculate max depth of current label's subtree to ensure moving it doesn't exceed 3 levels
        int subtreeDepth = await GetSubtreeDepthAsync(id);
        if (newLevel + subtreeDepth - 1 > 3)
            throw new Exception($"Di chuyển nhãn này sẽ làm vượt quá giới hạn 3 cấp (tổng cộng {newLevel + subtreeDepth - 1} cấp).");

        label.ParentId = request.NewParentId;
        label.Level = newLevel;
        label.SortOrder = request.NewSortOrder;
        label.UpdatedAt = DateTime.UtcNow;

        // Need to recursively update levels of all descendants
        await UpdateDescendantsLevelsAsync(id, newLevel);

        await db.SaveChangesAsync();
    }

    public async Task ReorderAsync(List<ReorderLabelRequest> requests)
    {
        foreach (var req in requests)
        {
            var label = await db.Labels.FindAsync(req.LabelId);
            if (label != null)
            {
                label.SortOrder = req.NewSortOrder;
            }
        }
        await db.SaveChangesAsync();
    }

    // --- Helpers ---
    
    private List<LabelResponse> BuildTree(List<Label> allLabels, Guid? parentId)
    {
        return allLabels
            .Where(l => l.ParentId == parentId)
            .Select(l => MapToResponse(l) with { Children = BuildTree(allLabels, l.Id) })
            .ToList();
    }

    private LabelResponse MapToResponse(Label l) => new(
        l.Id, l.ProjectId, l.ParentId, l.Name, l.Code, l.Level, 
        l.SortOrder, l.Description, l.LlmPrompt, l.IsActive, new List<LabelResponse>()
    );

    private async Task<int> GetMaxSortOrderAsync(Guid projectId, Guid? parentId)
    {
        var max = await db.Labels
            .Where(l => l.ProjectId == projectId && l.ParentId == parentId)
            .MaxAsync(l => (int?)l.SortOrder);
        return max ?? 0;
    }

    private async Task CheckChildrenDocumentsAsync(Guid parentId)
    {
        var children = await db.Labels.Where(l => l.ParentId == parentId).ToListAsync();
        foreach (var child in children)
        {
            bool hasDocs = await db.Documents.AnyAsync(d => d.LabelId == child.Id);
            if (hasDocs) throw new Exception($"Không thể xóa: Nhãn con '{child.Name}' đang có tài liệu.");
            await CheckChildrenDocumentsAsync(child.Id);
        }
    }

    private async Task<bool> IsDescendantAsync(Guid ancestorId, Guid potentialDescendantId)
    {
        var current = await db.Labels.FindAsync(potentialDescendantId);
        while (current?.ParentId != null)
        {
            if (current.ParentId == ancestorId) return true;
            current = await db.Labels.FindAsync(current.ParentId);
        }
        return false;
    }

    private async Task<int> GetSubtreeDepthAsync(Guid labelId)
    {
        var children = await db.Labels.Where(l => l.ParentId == labelId).ToListAsync();
        if (!children.Any()) return 1;

        int maxChildDepth = 0;
        foreach (var child in children)
        {
            int depth = await GetSubtreeDepthAsync(child.Id);
            if (depth > maxChildDepth) maxChildDepth = depth;
        }
        return 1 + maxChildDepth;
    }

    private async Task UpdateDescendantsLevelsAsync(Guid parentId, int newParentLevel)
    {
        var children = await db.Labels.Where(l => l.ParentId == parentId).ToListAsync();
        foreach (var child in children)
        {
            child.Level = newParentLevel + 1;
            await UpdateDescendantsLevelsAsync(child.Id, child.Level);
        }
    }
}
