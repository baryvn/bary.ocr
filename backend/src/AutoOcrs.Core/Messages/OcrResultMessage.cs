namespace AutoOcrs.Core.Messages;

public record OcrResultMessage(
    Guid DocumentId,
    string Status,
    string? ErrorMessage
);
