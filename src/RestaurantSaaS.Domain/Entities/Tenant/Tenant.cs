
using RestaurantSaaS.Domain.Common;
using RestaurantSaaS.Domain.Enums;
using RestaurantSaaS.Domain.Exceptions;
using RestaurantSaaS.Domain.Validation;

namespace RestaurantSaaS.Domain.Entities.Tenants;

public class Tenant : AuditableEntity, ISoftDeletable
{
    public string Name { get; private set; } = null!;
    public string Slug { get; private set; } = null!;
    public TenantStatus Status { get; private set; } = TenantStatus.Pending;

    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }

    public ICollection<Restaurant> Restaurants { get; private set; } = new List<Restaurant>();
    public ICollection<StaffMembership> StaffMemberships { get; private set; } = new List<StaffMembership>();
    public ICollection<Subscription> Subscriptions { get; private set; } = new List<Subscription>();

    private Tenant()
    {
    }

    public static Tenant Create(string name, string slug)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("TENANT_NAME_REQUIRED", "Tenant name is required.");

        var normalizedName = name.Trim();

        if (normalizedName.Length > 150)
            throw new DomainException("TENANT_NAME_TOO_LONG", "Tenant name cannot exceed 150 characters.");

        return new Tenant
        {
            Name = normalizedName,
            Slug = SlugValidator.Normalize(slug),
            Status = TenantStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void Rename(string name)
    {
        EnsureNotDeleted();

        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("TENANT_NAME_REQUIRED", "Tenant name is required.");

        var normalizedName = name.Trim();

        if (normalizedName.Length > 150)
            throw new DomainException("TENANT_NAME_TOO_LONG", "Tenant name cannot exceed 150 characters.");

        Name = normalizedName;
        UpdatedAt = DateTime.UtcNow;
    }

    public void ChangeStatus(TenantStatus newStatus)
    {
        EnsureNotDeleted();

        // Cancelled IS a valid target: the platform-admin contract (UpdateTenantStatusRequest,
        // UpdateTenantStatusRequestValidator and TenantService.ChangeStatusForPlatformAsync)
        // all accept it. Cancelled is TERMINAL — once reached, the guard below blocks any
        // further transition.
        if (newStatus is not TenantStatus.Pending
            and not TenantStatus.Active
            and not TenantStatus.Suspended
            and not TenantStatus.Cancelled)
        {
            throw new DomainException("TENANT_STATUS_INVALID", "The requested tenant status is invalid.");
        }

        if (Status == TenantStatus.Cancelled)
            throw new DomainException("TENANT_ALREADY_CANCELLED", "A cancelled tenant cannot change status.");

        if (Status == newStatus)
            return;

        Status = newStatus;
        UpdatedAt = DateTime.UtcNow;
    }

    public void EnsureOperational()
    {
        EnsureNotDeleted();

        if (Status != TenantStatus.Active)
        {
            throw new DomainException(
                "TENANT_NOT_ACTIVE",
                Status switch
                {
                    TenantStatus.Pending => "This tenant is still pending activation.",
                    TenantStatus.Suspended => "This tenant is suspended. Contact support.",
                    TenantStatus.Cancelled => "This tenant has been cancelled.",
                    _ => "This tenant is not available for operations."
                });
        }
    }
    public static string NormalizeSlug(string slug)
    {
        if (string.IsNullOrWhiteSpace(slug))
            throw new ArgumentException(
                "Tenant slug cannot be empty.",
                nameof(slug));

        return slug.Trim().ToLowerInvariant();
    }
    public void SoftDelete()
    {
        if (IsDeleted)
            return;

        IsDeleted = true;
        DeletedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    private void EnsureNotDeleted()
    {
        if (IsDeleted)
            throw new DomainException("TENANT_DELETED", "This tenant has been deleted.");
    }
}