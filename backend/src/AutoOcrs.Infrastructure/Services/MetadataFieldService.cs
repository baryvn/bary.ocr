using AutoOcrs.Core.DTOs.MetadataFields;
using AutoOcrs.Core.Entities;
using AutoOcrs.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AutoOcrs.Infrastructure.Services;

public class MetadataFieldService(AppDbContext db)
{
    public async Task<List<MetadataFieldResponse>> GetFieldsByLabelAsync(Guid labelId)
    {
        var fields = await db.MetadataFields
            .Where(f => f.LabelId == labelId)
            .OrderBy(f => f.SortOrder)
            .ToListAsync();

        return fields.Select(MapToResponse).ToList();
    }

    public async Task<List<MetadataFieldResponse>> GetInheritedFieldsByLabelAsync(Guid labelId)
    {
        var ancestorIds = new List<Guid>();
        var currentLabelId = (Guid?)labelId;
        
        while (currentLabelId.HasValue)
        {
            ancestorIds.Add(currentLabelId.Value);
            currentLabelId = await db.Labels
                .Where(l => l.Id == currentLabelId.Value)
                .Select(l => l.ParentId)
                .FirstOrDefaultAsync();
        }

        var fields = await db.MetadataFields
            .Where(f => ancestorIds.Contains(f.LabelId))
            .OrderBy(f => f.SortOrder)
            .ToListAsync();

        return fields.Select(MapToResponse).ToList();
    }

    public async Task<MetadataFieldResponse> CreateAsync(Guid labelId, CreateMetadataFieldRequest request)
    {
        var label = await db.Labels.FindAsync(labelId) ?? throw new Exception("Không tìm thấy nhãn.");


        // Check duplicate field key
        if (await db.MetadataFields.AnyAsync(f => f.LabelId == labelId && f.FieldKey == request.FieldKey))
        {
            throw new Exception("Mã trường (Field Key) đã tồn tại trong nhãn này.");
        }

        var field = new MetadataField
        {
            LabelId = labelId,
            FieldName = request.FieldName,
            FieldKey = request.FieldKey,
            FieldType = request.FieldType,
            Options = string.IsNullOrWhiteSpace(request.Options) ? null : request.Options,
            LlmExtractionPrompt = request.LlmExtractionPrompt,
            IsRequired = request.IsRequired,
            SortOrder = request.SortOrder ?? (await GetMaxSortOrderAsync(labelId) + 1)
        };

        db.MetadataFields.Add(field);
        await db.SaveChangesAsync();

        return MapToResponse(field);
    }

    public async Task<MetadataFieldResponse> UpdateAsync(Guid id, UpdateMetadataFieldRequest request)
    {
        var field = await db.MetadataFields.FindAsync(id) ?? throw new Exception("Không tìm thấy trường dữ liệu.");

        // Check duplicate field key (excluding itself)
        if (field.FieldKey != request.FieldKey && await db.MetadataFields.AnyAsync(f => f.LabelId == field.LabelId && f.FieldKey == request.FieldKey))
        {
            throw new Exception("Mã trường (Field Key) đã tồn tại trong nhãn này.");
        }

        field.FieldName = request.FieldName;
        field.FieldKey = request.FieldKey;
        field.FieldType = request.FieldType;
        field.Options = string.IsNullOrWhiteSpace(request.Options) ? null : request.Options;
        field.LlmExtractionPrompt = request.LlmExtractionPrompt;
        field.IsRequired = request.IsRequired;
        field.SortOrder = request.SortOrder;
        field.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();
        return MapToResponse(field);
    }

    public async Task DeleteAsync(Guid id)
    {
        var field = await db.MetadataFields.FindAsync(id) ?? throw new Exception("Không tìm thấy trường dữ liệu.");
        db.MetadataFields.Remove(field);
        await db.SaveChangesAsync();
    }

    private MetadataFieldResponse MapToResponse(MetadataField f) => new(
        f.Id, f.LabelId, f.FieldName, f.FieldKey, f.FieldType, 
        f.Options, f.LlmExtractionPrompt, f.IsRequired, f.SortOrder
    );

    private async Task<int> GetMaxSortOrderAsync(Guid labelId)
    {
        var max = await db.MetadataFields
            .Where(f => f.LabelId == labelId)
            .MaxAsync(f => (int?)f.SortOrder);
        return max ?? 0;
    }
}
