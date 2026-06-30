using AutoOcrs.Core.Enums;

namespace AutoOcrs.Core.Entities;

public class Document : BaseEntity
{
    public Guid BatchId { get; set; }
    public string OriginalFilename { get; set; } = string.Empty;
    public string? StorageKey { get; set; }
    public string? OcrPdfKey { get; set; }
    public string? OutputFilename { get; set; }
    public string? OutputKey { get; set; }
    public int PageCount { get; set; }
    public Guid? LabelId { get; set; }
    public float? ClassificationConfidence { get; set; }
    public string? ExtractedMetadata { get; set; }
    public string? ReviewedMetadata { get; set; }
    public DocumentStatus Status { get; set; } = DocumentStatus.Pending;
    public string? ErrorMessage { get; set; }
    public string? ProcessingMessage { get; set; }
    public string? ReviewerNote { get; set; }
    public Guid? ReviewedBy { get; set; }
    public DateTime? OcrCompletedAt { get; set; }
    public DateTime? ClassifiedAt { get; set; }
    public DateTime? ExtractedAt { get; set; }
    public DateTime? ReviewedAt { get; set; }

    public Batch? Batch { get; set; }
    public Label? Label { get; set; }
    public User? Reviewer { get; set; }
    public ICollection<DocumentPage> Pages { get; set; } = [];
}
