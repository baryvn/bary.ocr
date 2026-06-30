using AutoOcrs.Core.Enums;

namespace AutoOcrs.Core.Entities;

public class Batch : BaseEntity
{
    public Guid ProjectId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? SourceFolder { get; set; }
    public int TotalFiles { get; set; }
    public int ProcessedFiles { get; set; }
    public int ApprovedFiles { get; set; }
    public BatchStatus Status { get; set; } = BatchStatus.Created;
    public string? MinioPrefix { get; set; }

    public Project? Project { get; set; }
    public ICollection<Document> Documents { get; set; } = [];
    public ICollection<ReviewAssignment> ReviewAssignments { get; set; } = [];
}
