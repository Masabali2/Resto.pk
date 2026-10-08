using RestaurantSaaS.Domain.Common;
using RestaurantSaaS.Domain.Enums;
using RestaurantSaaS.Domain.Exceptions;

namespace RestaurantSaaS.Domain.Entities.Carts;

/// <summary>
/// Guest-capable shopping cart. Either CustomerId (identified guest) or
/// SessionId (anonymous browser session) — or both after identification.
/// ExpiresAt supports background cleanup jobs (Hangfire).
///
/// Business rule (requirement #32): all CartItems of one cart must resolve to
/// menu items of the SAME tenant AND SAME restaurant. The restaurant binding
/// lives on CartItem -> MenuItem -> Menu(RestaurantId); the application layer
/// validates it on every add. TenantId here is justified for direct querying
/// and global-filter enforcement (requirement #38 exception path).
/// </summary>
public class Cart : AuditableEntity, ITenantOwned
{
    /// <summary>Default guest-cart lifetime before expiry cleanup.</summary>
    public static readonly TimeSpan DefaultLifetime = TimeSpan.FromHours(24);

    public Guid TenantId { get; set; }
    public Guid? CustomerId { get; private set; }
    public string? SessionId { get; private set; }
    public CartStatus Status { get; private set; } = CartStatus.Active
;
    public DateTime ExpiresAt { get; private set; }

    public Tenant Tenant { get; private set; } = null!;
    public ICollection<CartItem> Items { get; set; } = new List<CartItem>();

    public static Cart Create(Guid tenantId, Guid? customerId, string? sessionId, DateTime utcNow)
    {
        if (tenantId == Guid.Empty)
            throw new DomainException("CART_TENANT_REQUIRED", "A cart must belong to a tenant.");

        if (customerId is null && string.IsNullOrWhiteSpace(sessionId))
            throw new DomainException("CART_OWNER_REQUIRED", "A cart needs a customer id or a session id.");

        return new Cart
        {
            TenantId = tenantId,
            CustomerId = customerId,
            SessionId = sessionId?.Trim(),
            Status = CartStatus.Active,
            ExpiresAt = utcNow.Add(DefaultLifetime),
            CreatedAt = utcNow
        };
    }

    public void EnsureUsable(DateTime utcNow)
    {
        if (Status != CartStatus.Active)
            throw new DomainException("CART_NOT_ACTIVE", $"Cart is {Status} and cannot be modified.");

        if (ExpiresAt <= utcNow)
            throw new DomainException("CART_EXPIRED", "Cart has expired; start a new order.");
    }

    public void MarkConverted(DateTime utcNow)
    {
        if (Status != CartStatus.Active)
            throw new DomainException("CART_NOT_ACTIVE", "Only an active cart can be converted.");

        Status = CartStatus.Converted;
        UpdatedAt = utcNow;
    }

    public void MarkExpired(DateTime utcNow)
    {
        if (Status != CartStatus.Active)
            return; // idempotent for cleanup jobs

        Status = CartStatus.Expired;
        UpdatedAt = utcNow;
    }
}
