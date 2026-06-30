using AutoOcrs.Core.Enums;

namespace AutoOcrs.Core.Entities;

public class ReviewAssignment : BaseEntity
{
    public Guid BatchId { get; set; }
    public Guid ReviewerId { get; set; }
    public Guid AssignedBy { get; set; }
    public int TotalDocuments { get; set; }
    public int ReviewedDocuments { get; set; }
    public ReviewStatus Status { get; set; } = ReviewStatus.Assigned;
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    public Batch? Batch { get; set; }
    public User? Reviewer { get; set; }
    public User? Assigner { get; set; }
}
