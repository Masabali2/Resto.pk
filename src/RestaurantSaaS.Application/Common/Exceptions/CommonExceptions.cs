using RestaurantSaaS.Domain.Exceptions;

namespace RestaurantSaaS.Application.Common.Exceptions;

/// <summary>Mapped to 401 by exception middleware.</summary>
public sealed class NotAuthenticatedException : DomainException
{
    public NotAuthenticatedException(string code = "AUTH_UNAUTHORIZED", string? message = null)
        : base(code, message ?? "Authentication is required for this operation.")
    {
    }
}

/// <summary>
/// Mapped to 403. Raised when a user lacks the tenant role/permission OR when a
/// resource fails cross-tenant ownership validation. Message intentionally
/// generic — never reveal whether the resource exists in another tenant.
/// </summary>
public sealed class CrossTenantAccessException : Exception
{
    public CrossTenantAccessException(string? message = null)
        : base(message ?? "The requested resource does not belong to the current tenant.")
    {
    }
}

/// <summary>Mapped to 404.</summary>
public sealed class NotFoundException : Exception
{
    public NotFoundException(string entityName, object key)
        : base($"{entityName} '{key}' was not found.")
    {
    }

    public NotFoundException(string message) : base(message)
    {
    }
}

/// <summary>Mapped to 409 (DbUpdateConcurrencyException also maps here).</summary>
public sealed class ConflictException : Exception
{
    public ConflictException(string message) : base(message)
    {
    }
}
