using AutoOcrs.Core.Enums;

namespace AutoOcrs.Core.Entities;

public class Project : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? SourcePath { get; set; }
    public string? OutputPath { get; set; }
    public string? NamingRule { get; set; }
    public string? FolderRule { get; set; }
    public string? ClassificationPrompt { get; set; }
    public string? ExtractionPrompt { get; set; }
    public ProjectStatus Status { get; set; } = ProjectStatus.Active;
    public Guid CreatedBy { get; set; }

    public User? Creator { get; set; }
    public ICollection<Label> Labels { get; set; } = [];
    public ICollection<Batch> Batches { get; set; } = [];
}
