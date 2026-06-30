namespace AutoOcrs.Core.DTOs.Projects;

public record CreateProjectRequest(string Name, string? Description, string? SourcePath, string? OutputPath, string? ClassificationPrompt, string? ExtractionPrompt);

public record UpdateProjectRequest(string? Name, string? Description, string? SourcePath, string? OutputPath, string? ClassificationPrompt, string? ExtractionPrompt);

public record ProjectResponse(
    Guid Id, string Name, string? Description, string? SourcePath, string? OutputPath,
    string? NamingRule, string? FolderRule, string? ClassificationPrompt, string? ExtractionPrompt, string Status,
    string CreatedByName, DateTime CreatedAt,
    int TotalBatches, int TotalDocuments, int ApprovedDocuments);
