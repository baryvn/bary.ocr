using System.Text.Json;

namespace AutoOcrs.Core.DTOs.NamingRules;

public record NamingRuleResponse(
    string? NamingRule,
    string? FolderRule
);

public record UpdateNamingRuleRequest(
    string? NamingRule,
    string? FolderRule
);

public record PreviewNamingRuleRequest(
    string? NamingRule,
    string? FolderRule,
    Guid? LabelId,
    JsonElement? MockMetadata
);

public record PreviewNamingRuleResponse(
    string FolderPath,
    string FileName,
    string FullPath
);
