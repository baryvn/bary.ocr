namespace AutoOcrs.Core.Messages;

public record OcrJobMessage(
    Guid DocumentId,
    Guid ProjectId,
    string StorageKey
);
