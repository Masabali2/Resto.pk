using RestaurantSaaS.Domain.Common;

namespace RestaurantSaaS.Domain.Entities.Audit;

/// <summary>
/// Append-only audit trail of security/business-relevant actions.
/// NEVER updated or deleted by application code; retention handled by jobs.
/// TenantId/UserId are nullable: platform-level actions have neither.
/// PII discipline: Description must not contain passwords/tokens/full payment data.
/// </summary>
public class AuditLog : BaseEntity
{
    public Guid? TenantId { get; private set; }
    public Guid? UserId { get; private set; }

    /// <summary>Stable action identifier, e.g. "order.status.changed".</summary>
    public string Action { get; private set; } = null!;

    public string EntityType { get; private set; } = null!;
    public Guid? EntityId { get; private set; }

    public string? Description { get; private set; }
    public string? IpAddress { get; private set; }
    public string? UserAgent { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public static AuditLog Record(
        Guid? tenantId,
        Guid? userId,
        string action,
        string entityType,
        Guid? entityId,
        string? description,
        string? ipAddress,
        string? userAgent)
    {
        return new AuditLog
        {
            TenantId = tenantId,
            UserId = userId,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            Description = description is { Length: > 4000 } ? description[..4000] : description,
            IpAddress = ipAddress,
            UserAgent = userAgent is { Length: > 512 } ? userAgent[..512] : userAgent,
            CreatedAt = DateTime.UtcNow
        };
    }
}
