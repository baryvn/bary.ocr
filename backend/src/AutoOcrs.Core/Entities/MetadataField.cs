using AutoOcrs.Core.Enums;

namespace AutoOcrs.Core.Entities;

public class MetadataField : BaseEntity
{
    public Guid LabelId { get; set; }
    public string FieldName { get; set; } = string.Empty;
    public string FieldKey { get; set; } = string.Empty;
    public FieldType FieldType { get; set; }
    public string? Options { get; set; }
    public string? LlmExtractionPrompt { get; set; }
    public bool IsRequired { get; set; }
    public int SortOrder { get; set; }

    public Label? Label { get; set; }
}
