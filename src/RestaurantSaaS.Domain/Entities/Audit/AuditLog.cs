
using RestaurantSaaS.Domain.Common;
using RestaurantSaaS.Domain.Exceptions;

namespace RestaurantSaaS.Domain.Entities.Audit;

public class AuditLog : BaseEntity
{
    public const int MaxActionLength = 150;
    public const int MaxEntityTypeLength = 150;
    public const int MaxDescriptionLength = 4000;
    public const int MaxIpAddressLength = 45;
    public const int MaxUserAgentLength = 512;

    public Guid? TenantId { get; private set; }

    public Guid? UserId { get; private set; }

    public string Action { get; private set; } = null!;

    public string EntityType { get; private set; } = null!;

    public Guid? EntityId { get; private set; }

    public string? Description { get; private set; }

    public string? IpAddress { get; private set; }

    public string? UserAgent { get; private set; }

    public DateTime CreatedAt { get; private set; }

    private AuditLog()
    {
    }

    public static AuditLog Record(
        Guid? tenantId,
        Guid? userId,
        string action,
        string entityType,
        Guid? entityId,
        string? description,
        string? ipAddress,
        string? userAgent,
        DateTime? utcNow = null)
    {
        ValidateOptionalId(tenantId, "AUDIT_TENANT_ID_INVALID");
        ValidateOptionalId(userId, "AUDIT_USER_ID_INVALID");
        ValidateOptionalId(entityId, "AUDIT_ENTITY_ID_INVALID");

        var normalizedAction = NormalizeRequired(action, MaxActionLength, "action");
        var normalizedEntityType = NormalizeRequired(entityType, MaxEntityTypeLength, "entity type");
        var normalizedDescription = NormalizeOptional(description, MaxDescriptionLength);
        var normalizedIpAddress = NormalizeOptional(ipAddress, MaxIpAddressLength);
        var normalizedUserAgent = NormalizeOptional(userAgent, MaxUserAgentLength);

        var timestamp = utcNow ?? DateTime.UtcNow;

        if (timestamp.Kind != DateTimeKind.Utc)
        {
            throw new DomainException(
                "AUDIT_TIMESTAMP_INVALID",
                "Audit timestamp must be UTC.");
        }

        return new AuditLog
        {
            TenantId = tenantId,
            UserId = userId,
            Action = normalizedAction,
            EntityType = normalizedEntityType,
            EntityId = entityId,
            Description = normalizedDescription,
            IpAddress = normalizedIpAddress,
            UserAgent = normalizedUserAgent,
            CreatedAt = timestamp
        };
    }

    private static void ValidateOptionalId(Guid? id, string errorCode)
    {
        if (id == Guid.Empty)
        {
            throw new DomainException(
                errorCode,
                "An optional identifier cannot contain an empty GUID.");
        }
    }

    private static string NormalizeRequired(
        string value,
        int maxLength,
        string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DomainException(
                "AUDIT_FIELD_REQUIRED",
                $"Audit {fieldName} is required.");
        }

        var normalized = value.Trim();

        if (normalized.Length > maxLength)
        {
            throw new DomainException(
                "AUDIT_FIELD_TOO_LONG",
                $"Audit {fieldName} cannot exceed {maxLength} characters.");
        }

        return normalized;
    }

    private static string? NormalizeOptional(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var normalized = value.Trim();

        return normalized.Length > maxLength
            ? normalized[..maxLength]
            : normalized;
    }
}
