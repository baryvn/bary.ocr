using AutoOcrs.Core.Messages;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace AutoOcrs.Infrastructure.Services;

public class OcrJobPublisher(IPublishEndpoint publishEndpoint, ILogger<OcrJobPublisher> logger)
{
    public async Task PublishJobAsync(Guid documentId, Guid projectId, string storageKey)
    {
        try
        {
            await publishEndpoint.Publish(new OcrJobMessage(documentId, projectId, storageKey));
            logger.LogInformation($"Đã đẩy OcrJobMessage cho Document {documentId} vào Queue");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"Lỗi khi publish OcrJobMessage cho Document {documentId}");
            throw;
        }
    }
}
