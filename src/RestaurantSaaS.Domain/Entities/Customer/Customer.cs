
using RestaurantSaaS.Domain.Common;
using RestaurantSaaS.Domain.Exceptions;

namespace RestaurantSaaS.Domain.Entities.Customers;

public class Customer : AuditableEntity, ITenantOwned, ISoftDeletable
{
    public const int MaxNameLength = 150;
    public const int MaxPhoneLength = 30;
    public const int MaxEmailLength = 254;

    public Guid TenantId { get;  set; }

    public string? Name { get; private set; }

    public string? Phone { get; private set; }

    public string? Email { get; private set; }

    public bool IsDeleted { get; set; }

    public DateTime? DeletedAt { get; set; }

    public Tenant Tenant { get; private set; } = null!;

    private Customer()
    {
    }

    public static Customer Create(
        Guid tenantId,
        string? name,
        string? phone,
        string? email)
    {
        ValidateTenantId(tenantId);

        var normalizedName = NormalizeOptional(name);
        var normalizedPhone = NormalizeOptional(phone);
        var normalizedEmail = NormalizeEmail(email);

        ValidateName(normalizedName);
        ValidatePhone(normalizedPhone);
        ValidateEmail(normalizedEmail);

        if (normalizedName is null &&
            normalizedPhone is null &&
            normalizedEmail is null)
        {
            throw new DomainException(
                "CUSTOMER_IDENTITY_REQUIRED",
                "A customer needs at least a name, phone or email.");
        }

        return new Customer
        {
            TenantId = tenantId,
            Name = normalizedName,
            Phone = normalizedPhone,
            Email = normalizedEmail,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void UpdateContact(
        string? name,
        string? phone,
        string? email,
        bool updateName = false,
        bool updatePhone = false,
        bool updateEmail = false)
    {
        EnsureNotDeleted();

        var newName = updateName ? NormalizeOptional(name) : Name;
        var newPhone = updatePhone ? NormalizeOptional(phone) : Phone;
        var newEmail = updateEmail ? NormalizeEmail(email) : Email;

        ValidateName(newName);
        ValidatePhone(newPhone);
        ValidateEmail(newEmail);

        if (newName is null && newPhone is null && newEmail is null)
        {
            throw new DomainException(
                "CUSTOMER_IDENTITY_REQUIRED",
                "A customer needs at least a name, phone or email.");
        }

        Name = newName;
        Phone = newPhone;
        Email = newEmail;
        UpdatedAt = DateTime.UtcNow;
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
        {
            throw new DomainException(
                "CUSTOMER_DELETED",
                "A deleted customer cannot be modified.");
        }
    }

    private static void ValidateTenantId(Guid tenantId)
    {
        if (tenantId == Guid.Empty)
        {
            throw new DomainException(
                "CUSTOMER_TENANT_REQUIRED",
                "A customer must belong to a tenant.");
        }
    }

    private static void ValidateName(string? name)
    {
        if (name?.Length > MaxNameLength)
        {
            throw new DomainException(
                "CUSTOMER_NAME_TOO_LONG",
                $"Customer name cannot exceed {MaxNameLength} characters.");
        }
    }

    private static void ValidatePhone(string? phone)
    {
        if (phone?.Length > MaxPhoneLength)
        {
            throw new DomainException(
                "CUSTOMER_PHONE_TOO_LONG",
                $"Customer phone cannot exceed {MaxPhoneLength} characters.");
        }
    }

    private static void ValidateEmail(string? email)
    {
        if (email?.Length > MaxEmailLength)
        {
            throw new DomainException(
                "CUSTOMER_EMAIL_TOO_LONG",
                $"Customer email cannot exceed {MaxEmailLength} characters.");
        }
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? NormalizeEmail(string? email) =>
        NormalizeOptional(email)?.ToLowerInvariant();
}
