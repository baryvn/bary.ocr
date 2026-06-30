namespace AutoOcrs.Core.DTOs.Documents;

public record DocumentResponse(
    Guid Id,
    string OriginalFilename,
    string Status,
    int PageCount,
    Guid? LabelId,
    string? LabelName,
    float? ClassificationConfidence,
    string? ErrorMessage,
    string? ProcessingMessage,
    DateTime CreatedAt,
    DateTime? UpdatedAt
);

public record DocumentDetailResponse(
    Guid Id,
    Guid ProjectId,
    string OriginalFilename,
    string Status,
    int PageCount,
    Guid? LabelId,
    string? LabelName,
    string? ExtractedMetadata,
    string? ReviewedMetadata,
    string? ErrorMessage,
    string? ProcessingMessage,
    string? ReviewerNote,
    List<DocumentPageResponse> Pages
);

public record DocumentPageResponse(
    Guid Id,
    int PageNumber,
    string? ImageKey,
    int Width,
    int Height
);
