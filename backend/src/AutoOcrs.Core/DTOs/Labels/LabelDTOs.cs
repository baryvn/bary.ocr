namespace AutoOcrs.Core.DTOs.Labels;

public record LabelResponse(
    Guid Id,
    Guid ProjectId,
    Guid? ParentId,
    string Name,
    string? Code,
    int Level,
    int SortOrder,
    string? Description,
    string? LlmPrompt,
    bool IsActive,
    List<LabelResponse>? Children = null
);

public record CreateLabelRequest(
    Guid ProjectId,
    Guid? ParentId,
    string Name,
    string? Code,
    string? Description,
    string? LlmPrompt,
    int? SortOrder
);

public record UpdateLabelRequest(
    string Name,
    string? Code,
    string? Description,
    string? LlmPrompt,
    bool IsActive
);

public record MoveLabelRequest(
    Guid? NewParentId,
    int NewSortOrder
);

public record ReorderLabelRequest(
    Guid LabelId,
    int NewSortOrder
);
