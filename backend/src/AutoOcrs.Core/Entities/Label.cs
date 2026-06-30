namespace AutoOcrs.Core.Entities;

public class Label : BaseEntity
{
    public Guid ProjectId { get; set; }
    public Guid? ParentId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Code { get; set; }
    public int Level { get; set; }
    public int SortOrder { get; set; }
    public string? Description { get; set; }
    public string? LlmPrompt { get; set; }
    public bool IsActive { get; set; } = true;

    public Project? Project { get; set; }
    public Label? Parent { get; set; }
    public ICollection<Label> Children { get; set; } = [];
    public ICollection<MetadataField> MetadataFields { get; set; } = [];
    public ICollection<Document> Documents { get; set; } = [];
}
