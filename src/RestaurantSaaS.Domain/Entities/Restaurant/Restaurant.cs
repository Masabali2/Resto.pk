
using RestaurantSaaS.Domain.Common;
using RestaurantSaaS.Domain.Enums;
using RestaurantSaaS.Domain.Exceptions;
using RestaurantSaaS.Domain.ValueObjects;

namespace RestaurantSaaS.Domain.Entities.Restaurants;

public class Restaurant : AuditableEntity, ITenantOwned, ISoftDeletable
{
    public Guid TenantId { get; set; }

    public string Name { get; private set; } = null!;
    public string? Description { get; private set; }
    public string? Phone { get; private set; }
    public string? Email { get; private set; }

    public string AddressLine1 { get; private set; } = null!;
    public string? AddressLine2 { get; private set; }
    public string City { get; private set; } = null!;
    public string Country { get; private set; } = null!;

    public string TimeZone { get; private set; } = "Asia/Karachi";

    public RestaurantStatus Status { get; private set; } = RestaurantStatus.Draft;

    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }

    public Tenants.Tenant Tenant { get; private set; } = null!;

    public ICollection<RestaurantAsset> Assets { get; private set; }
        = new List<RestaurantAsset>();

    private Restaurant()
    {
    }

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
            throw new DomainException(
                "RESTAURANT_TENANT_REQUIRED",
                "A restaurant must belong to a tenant.");

        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException(
                "RESTAURANT_NAME_REQUIRED",
                "Restaurant name is required.");

        var normalizedName = name.Trim();

        if (normalizedName.Length > 150)
            throw new DomainException(
                "RESTAURANT_NAME_TOO_LONG",
                "Restaurant name cannot exceed 150 characters.");

        ArgumentNullException.ThrowIfNull(address);

        if (string.IsNullOrWhiteSpace(timeZone))
            throw new DomainException(
                "RESTAURANT_TIMEZONE_REQUIRED",
                "Restaurant time zone is required.");

        return new Restaurant
        {
            TenantId = tenantId,
            Name = normalizedName,
            Description = string.IsNullOrWhiteSpace(description)
                ? null
                : description.Trim(),
            AddressLine1 = address.Line1,
            AddressLine2 = address.Line2,
            City = address.City,
            Country = address.Country,
            Phone = phone?.Value,
            Email = email?.Value,
            TimeZone = timeZone.Trim(),
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

        if (name is not null)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new DomainException(
                    "RESTAURANT_NAME_REQUIRED",
                    "Restaurant name is required.");

            var normalizedName = name.Trim();

            if (normalizedName.Length > 150)
                throw new DomainException(
                    "RESTAURANT_NAME_TOO_LONG",
                    "Restaurant name cannot exceed 150 characters.");

            Name = normalizedName;
        }

        if (description is not null)
            Description = string.IsNullOrWhiteSpace(description)
                ? null
                : description.Trim();

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

        if (timeZone is not null)
        {
            if (string.IsNullOrWhiteSpace(timeZone))
                throw new DomainException(
                    "RESTAURANT_TIMEZONE_REQUIRED",
                    "Restaurant time zone is required.");

            TimeZone = timeZone.Trim();
        }

        UpdatedAt = DateTime.UtcNow;
    }

    public void ChangeStatus(RestaurantStatus newStatus)
    {
        EnsureNotDeleted();

        if (!Enum.IsDefined(newStatus))
            throw new DomainException(
                "RESTAURANT_STATUS_INVALID",
                "The requested restaurant status is invalid.");

        if (Status == RestaurantStatus.Closed && newStatus != RestaurantStatus.Closed)
            throw new DomainException(
                "RESTAURANT_CLOSED_TERMINAL",
                "A permanently closed restaurant cannot be reactivated; create a new location.");

        if (Status == newStatus)
            return;

        Status = newStatus;
        UpdatedAt = DateTime.UtcNow;
    }

    public bool CanAcceptOrders =>
        !IsDeleted &&
        Status == RestaurantStatus.Active;

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
            throw new DomainException(
                "RESTAURANT_DELETED",
                "A deleted restaurant cannot be modified.");
    }
}