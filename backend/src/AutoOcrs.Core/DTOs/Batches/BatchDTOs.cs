using AutoOcrs.Core.Enums;

namespace AutoOcrs.Core.DTOs.Batches;

public record BatchResponse(
    Guid Id,
    Guid ProjectId,
    string Name,
    string? SourceFolder,
    int TotalFiles,
    int ProcessedFiles,
    int ApprovedFiles,
    string Status,
    DateTime CreatedAt,
    string? ReviewerName = null
);

public record CreateBatchRequest(
    string Name,
    string? SourceFolder,
    List<string>? UploadedFileKeys // Nếu dùng phương pháp tải lên thay vì quét thư mục
);

public record UpdateBatchRequest(
    string Name,
    string? SourceFolder
);

public record AddDocumentsRequest(
    List<string> UploadedFileKeys
);

public record BatchProgressResponse(
    int TotalFiles,
    int ProcessedFiles,
    int ApprovedFiles,
    int Errors,
    int Classifying,
    int Extracting,
    int Reviewing
);
