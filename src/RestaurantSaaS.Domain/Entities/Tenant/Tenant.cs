using RestaurantSaaS.Domain.Common;
using RestaurantSaaS.Domain.Enums;
using RestaurantSaaS.Domain.Exceptions;

namespace RestaurantSaaS.Domain.Entities.Tenants;

/// <summary>
/// The SaaS customer/business organization — the security boundary for all
/// tenant data. One tenant owns many restaurants (branches), staff
/// memberships, subscriptions and all tenant-scoped data.
///
/// NAMING NOTE: the namespace segment is plural "Tenants" because C# does not
/// allow a namespace element to have the same name as its member type
/// (...Entities.Tenant.Tenant => CS0542). The file/folder layout still mirrors the
/// requested Entities/Tenant structure.
/// </summary>
public class Tenant : AuditableEntity, ISoftDeletable
{
    public string Name { get; private set; } = null!;

    /// <summary>URL-safe unique identifier (subdomain candidate). Immutable after creation.</summary>
    public string Slug { get; private set; } = null!;

    public TenantStatus Status { get; private set; } = TenantStatus.Pending;

    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }

    // Navigation properties
    public ICollection<Restaurant> Restaurants { get; set; } = new List<Restaurant>();
    public ICollection<StaffMembership> StaffMemberships { get; set; } = new List<StaffMembership>();
    public ICollection<Subscription> Subscriptions { get; set; } = new List<Subscription>();

    /// <summary>
    /// Factory for the onboarding use case. Slug uniqueness is enforced at the
    /// database level (unique index); callers must handle DbUpdateException.
    /// </summary>
    public static Tenant Create(string name, string slug)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("TENANT_NAME_REQUIRED", "Tenant name is required.");

        var normalizedSlug = NormalizeSlug(slug);

        return new Tenant
        {
            Name = name.Trim(),
            Slug = normalizedSlug,
            Status = TenantStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void Rename(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("TENANT_NAME_REQUIRED", "Tenant name is required.");

        Name = name.Trim();
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>Platform-admin operation (activation/suspension). Business rule: only Pending/Active/Suspended transitions from here; Cancelled handled by subscription lifecycle.</summary>
    public void ChangeStatus(TenantStatus newStatus)
    {
        if (IsDeleted)
            throw new DomainException("TENANT_DELETED", "A deleted tenant cannot change status.");

        Status = newStatus;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>Tenant operational gate used by tenant-scoped use cases.</summary>
    public void EnsureOperational()
    {
        if (IsDeleted)
            throw new DomainException("TENANT_DELETED", "This tenant has been deleted.");

        if (Status != TenantStatus.Active)
            throw new DomainException("TENANT_NOT_ACTIVE",
                Status switch
                {
                    TenantStatus.Pending => "This tenant is still pending activation.",
                    TenantStatus.Suspended => "This tenant is suspended. Contact support.",
                    _ => "This tenant is no longer active."
                });
    }

    public static string NormalizeSlug(string slug)
    {
        if (string.IsNullOrWhiteSpace(slug))
            throw new DomainException("TENANT_SLUG_REQUIRED", "Tenant slug is required.");

        var normalized = new string(slug.ToLowerInvariant()
            .Select(c => char.IsLetterOrDigit(c) || c == '-' ? c : '-')
            .ToArray())
            .Trim('-');

        normalized = string.Join('-', normalized.Split('-', StringSplitOptions.RemoveEmptyEntries));

        if (normalized.Length is < 3 or > 63)
            throw new DomainException("TENANT_SLUG_INVALID",
                "Tenant slug must contain 3 to 63 alphanumeric characters.");

        return normalized;
    }
}
