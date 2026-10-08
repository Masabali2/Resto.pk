using RestaurantSaaS.Domain.Common;
using RestaurantSaaS.Domain.Exceptions;

namespace RestaurantSaaS.Domain.Entities.Customers;

/// <summary>
/// A diner record created implicitly by guest checkout or explicitly via the
/// public site. NOT an ASP.NET Identity user in V1 (requirement #6).
/// Tenant-scoped: the same phone number at two tenants = two customers.
/// Soft-deletable because orders keep CustomerId history.
/// </summary>
public class Customer : AuditableEntity, ITenantOwned, ISoftDeletable
{
    public Guid TenantId { get; set; }

    public string? Name { get; private set; }
    public string? Phone { get; private set; }
    public string? Email { get; private set; }

    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }

    public Tenant Tenant { get; private set; } = null!;

    public static Customer Create(Guid tenantId, string? name, string? phone, string? email)
    {
        if (tenantId == Guid.Empty)
            throw new DomainException("CUSTOMER_TENANT_REQUIRED", "A customer must belong to a tenant.");

        if (string.IsNullOrWhiteSpace(name) && string.IsNullOrWhiteSpace(phone) && string.IsNullOrWhiteSpace(email))
            throw new DomainException("CUSTOMER_IDENTITY_REQUIRED",
                "A customer needs at least a name, phone or e-mail.");

        return new Customer
        {
            TenantId = tenantId,
            Name = name?.Trim(),
            Phone = phone?.Trim(),
            Email = email?.Trim().ToLowerInvariant(),
            CreatedAt = DateTime.UtcNow
        };
    }

    public void UpdateContact(string? name, string? phone, string? email)
    {
        if (IsDeleted)
            throw new DomainException("CUSTOMER_DELETED", "A deleted customer cannot be modified.");

        if (name is not null) Name = name.Trim();
        if (phone is not null) Phone = phone.Trim();
        if (email is not null) Email = email.Trim().ToLowerInvariant();

        UpdatedAt = DateTime.UtcNow;
    }

    public void SoftDelete()
    {
        if (IsDeleted) return;
        IsDeleted = true;
        DeletedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }
}
