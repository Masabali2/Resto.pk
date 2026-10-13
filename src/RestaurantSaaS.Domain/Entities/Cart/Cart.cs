
using RestaurantSaaS.Domain.Common;
using RestaurantSaaS.Domain.Enums;
using RestaurantSaaS.Domain.Exceptions;

namespace RestaurantSaaS.Domain.Entities.Carts;

public class Cart : AuditableEntity, ITenantOwned
{
    public static readonly TimeSpan DefaultLifetime = TimeSpan.FromHours(24);

    public Guid TenantId { get;  set; }

    public Guid? CustomerId { get; private set; }

    public string? SessionId { get; private set; }

    public CartStatus Status { get; private set; } = CartStatus.Active;

    public DateTime ExpiresAt { get; private set; }

    public Tenant Tenant { get; private set; } = null!;

    private readonly List<CartItem> _items = [];

    public IReadOnlyCollection<CartItem> Items => _items.AsReadOnly();

    private Cart()
    {
    }

    public static Cart Create(
        Guid tenantId,
        Guid? customerId,
        string? sessionId,
        DateTime utcNow)
    {
        EnsureUtc(utcNow);

        if (tenantId == Guid.Empty)
        {
            throw new DomainException(
                "CART_TENANT_REQUIRED",
                "A cart must belong to a tenant.");
        }

        if (customerId == Guid.Empty)
        {
            throw new DomainException(
                "CART_CUSTOMER_INVALID",
                "Customer ID cannot be an empty GUID.");
        }

        var normalizedSessionId = NormalizeSessionId(sessionId);

        if (customerId is null && normalizedSessionId is null)
        {
            throw new DomainException(
                "CART_OWNER_REQUIRED",
                "A cart needs a customer ID or a session ID.");
        }

        return new Cart
        {
            TenantId = tenantId,
            CustomerId = customerId,
            SessionId = normalizedSessionId,
            Status = CartStatus.Active,
            ExpiresAt = utcNow.Add(DefaultLifetime),
            CreatedAt = utcNow
        };
    }

    public void EnsureUsable(DateTime utcNow)
    {
        EnsureUtc(utcNow);

        if (Status != CartStatus.Active)
        {
            throw new DomainException(
                "CART_NOT_ACTIVE",
                $"Cart is {Status} and cannot be modified.");
        }

        if (ExpiresAt <= utcNow)
        {
            throw new DomainException(
                "CART_EXPIRED",
                "Cart has expired; start a new order.");
        }
    }

    public void AssignCustomer(Guid customerId, DateTime utcNow)
    {
        EnsureUsable(utcNow);

        if (customerId == Guid.Empty)
        {
            throw new DomainException(
                "CART_CUSTOMER_INVALID",
                "Customer ID cannot be an empty GUID.");
        }

        CustomerId = customerId;
        UpdatedAt = utcNow;
    }

    public void Renew(DateTime utcNow)
    {
        EnsureUsable(utcNow);

        ExpiresAt = utcNow.Add(DefaultLifetime);
        UpdatedAt = utcNow;
    }

    public void MarkConverted(DateTime utcNow)
    {
        EnsureUtc(utcNow);

        if (Status != CartStatus.Active)
        {
            throw new DomainException(
                "CART_NOT_ACTIVE",
                "Only an active cart can be converted.");
        }

        Status = CartStatus.Converted;
        UpdatedAt = utcNow;
    }

    public void MarkExpired(DateTime utcNow)
    {
        EnsureUtc(utcNow);

        if (Status != CartStatus.Active)
            return;

        Status = CartStatus.Expired;
        UpdatedAt = utcNow;
    }

    private static string? NormalizeSessionId(string? sessionId)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
            return null;

        var normalized = sessionId.Trim();

        if (normalized.Length > 200)
        {
            throw new DomainException(
                "CART_SESSION_ID_TOO_LONG",
                "Session ID cannot exceed 200 characters.");
        }

        return normalized;
    }

    private static void EnsureUtc(DateTime value)
    {
        if (value.Kind != DateTimeKind.Utc)
        {
            throw new DomainException(
                "CART_TIME_INVALID",
                "The supplied time must be UTC.");
        }
    }
}
