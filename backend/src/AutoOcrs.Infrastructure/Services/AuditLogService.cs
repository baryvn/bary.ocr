using AutoOcrs.Core.Entities;
using AutoOcrs.Infrastructure.Data;

namespace AutoOcrs.Infrastructure.Services;

public class AuditLogService(AppDbContext db)
{
    public async Task LogActionAsync(Guid? userId, string action, string entityType, Guid? entityId, string? oldValues = null, string? newValues = null, string? ipAddress = null)
    {
        var log = new AuditLog
        {
            UserId = userId,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            OldValues = oldValues,
            NewValues = newValues,
            IpAddress = ipAddress,
            CreatedAt = DateTime.UtcNow
        };

        db.AuditLogs.Add(log);
        await db.SaveChangesAsync();
    }
}
