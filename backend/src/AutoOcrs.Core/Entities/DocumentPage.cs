namespace AutoOcrs.Core.Entities;

public class DocumentPage : BaseEntity
{
    public Guid DocumentId { get; set; }
    public int PageNumber { get; set; }
    public string? ImageKey { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public string? OcrResult { get; set; }
    public string? FullText { get; set; }

    public Document? Document { get; set; }
}
