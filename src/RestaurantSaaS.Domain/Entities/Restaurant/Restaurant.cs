using RestaurantSaaS.Domain.Common;
using RestaurantSaaS.Domain.Enums;
using RestaurantSaaS.Domain.Exceptions;
using RestaurantSaaS.Domain.ValueObjects;

namespace RestaurantSaaS.Domain.Entities.Restaurants;

/// <summary>
/// A physical restaurant/location. This entity doubles as the "branch" concept:
/// Tenant 1 -> M Restaurants (Pizza House Daska, Pizza House Sialkot, …).
/// There is deliberately no separate Branch table (requirement #2/#39).
///
/// Tenant integrity: the composite unique key (TenantId, Id) on this table lets
/// dependent tables (Menus, Websites, Orders, QRCodes) declare a composite FK
/// (TenantId, RestaurantId), making "menu of tenant A pointing at restaurant of
/// tenant B" impossible at the DATABASE level. See EF configurations.
/// </summary>
public class Restaurant : AuditableEntity, ITenantOwned, ISoftDeletable
{
    public Guid TenantId { get; set; }

    public string Name { get; private set; } = null!;
    public string? Description { get; private set; }

    /// <summary>Contact phone, normalized E.164-style digits.</summary>
    public string? Phone { get; private set; }

    /// <summary>Contact email, lower-cased.</summary>
    public string? Email { get; private set; }

    // Address (owned value object, flattened to columns by EF configuration)
    public string AddressLine1 { get; private set; } = null!;
    public string? AddressLine2 { get; private set; }
    public string City { get; private set; } = null!;
    public string Country { get; private set; } = null!;

    /// <summary>IANA time zone id (e.g. "Asia/Karachi") used for opening hours & reports.</summary>
    public string TimeZone { get; private set; } = "Asia/Karachi";

    public RestaurantStatus Status { get; private set; } = RestaurantStatus.Draft;

    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }

    // Navigation properties
    public Tenants.Tenant Tenant { get; private set; } = null!;
    public ICollection<RestaurantAsset> Assets { get; set; } = new List<RestaurantAsset>();

    /// <summary>
    /// Creates a restaurant inside a tenant. Slug/public identity is owned by
    /// the Website aggregate — restaurants are addressed via their websites.
    /// </summary>
    public static Restaurant Create(
        Guid tenantId,
        string name,
        Address address,
        PhoneNumber? phone = null,
        EmailAddress? email = null,
        string? description = null,
        string timeZone = "Asia/Karachi")
    {
        if (tenantId == Guid.Empty)
            throw new DomainException("RESTAURANT_TENANT_REQUIRED", "A restaurant must belong to a tenant.");

        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("RESTAURANT_NAME_REQUIRED", "Restaurant name is required.");

        if (name.Length > 150)
            throw new DomainException("RESTAURANT_NAME_TOO_LONG", "Restaurant name exceeds 150 characters.");

        return new Restaurant
        {
            TenantId = tenantId,
            Name = name.Trim(),
            Description = description?.Trim(),
            AddressLine1 = address.Line1,
            AddressLine2 = address.Line2,
            City = address.City,
            Country = address.Country,
            Phone = phone?.Value,
            Email = email?.Value,
            TimeZone = timeZone,
            Status = RestaurantStatus.Draft,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void UpdateProfile(
        string? name = null,
        string? description = null,
        Address? address = null,
        PhoneNumber? phone = null,
        EmailAddress? email = null,
        string? timeZone = null)
    {
        EnsureNotDeleted();

        if (!string.IsNullOrWhiteSpace(name))
        {
            if (name.Length > 150)
                throw new DomainException("RESTAURANT_NAME_TOO_LONG", "Restaurant name exceeds 150 characters.");
            Name = name.Trim();
        }

        if (description is not null)
            Description = description.Trim();

        if (address is not null)
        {
            AddressLine1 = address.Line1;
            AddressLine2 = address.Line2;
            City = address.City;
            Country = address.Country;
        }

        if (phone is not null)
            Phone = phone.Value;

        if (email is not null)
            Email = email.Value;

        if (!string.IsNullOrWhiteSpace(timeZone))
            TimeZone = timeZone;

        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Status transitions. Business rule: a Closed restaurant cannot be
    /// reopened directly — it must go through Active/Draft review first.
    /// </summary>
    public void ChangeStatus(RestaurantStatus newStatus)
    {
        EnsureNotDeleted();

        if (Status == RestaurantStatus.Closed && newStatus != RestaurantStatus.Closed)
            throw new DomainException("RESTAURANT_CLOSED_TERMINAL",
                "A permanently closed restaurant cannot be reactivated; create a new location.");

        Status = newStatus;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>A restaurant accepts orders only when active and not deleted.</summary>
    public bool CanAcceptOrders =>
        !IsDeleted && Status is RestaurantStatus.Active or RestaurantStatus.TemporarilyClosed;

    public void SoftDelete()
    {
        if (IsDeleted) return;
        IsDeleted = true;
        DeletedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    private void EnsureNotDeleted()
    {
        if (IsDeleted)
            throw new DomainException("RESTAURANT_DELETED", "A deleted restaurant cannot be modified.");
    }
}
