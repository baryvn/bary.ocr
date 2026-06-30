using AutoOcrs.Core.Enums;

namespace AutoOcrs.Core.DTOs.MetadataFields;

public record MetadataFieldResponse(
    Guid Id,
    Guid LabelId,
    string FieldName,
    string FieldKey,
    FieldType FieldType,
    string? Options,
    string? LlmExtractionPrompt,
    bool IsRequired,
    int SortOrder
);

public record CreateMetadataFieldRequest(
    string FieldName,
    string FieldKey,
    FieldType FieldType,
    string? Options,
    string? LlmExtractionPrompt,
    bool IsRequired,
    int? SortOrder
);

public record UpdateMetadataFieldRequest(
    string FieldName,
    string FieldKey,
    FieldType FieldType,
    string? Options,
    string? LlmExtractionPrompt,
    bool IsRequired,
    int SortOrder
);
