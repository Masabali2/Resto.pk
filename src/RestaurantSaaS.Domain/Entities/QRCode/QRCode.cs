
using System.Security.Cryptography;
using RestaurantSaaS.Domain.Common;
using RestaurantSaaS.Domain.Exceptions;

namespace RestaurantSaaS.Domain.Entities.QRCodes;

public class QRCode : AuditableEntity, ITenantOwned
{
    public const int CodeLength = 16;

    private const string CodeAlphabet = "abcdefghjkmnpqrstuvwxyz23456789";

    public Guid TenantId { get; set; }
    public Guid RestaurantId { get; private set; }

    public string Name { get; private set; } = null!;
    public string Code { get; private set; } = null!;
    public string TargetUrl { get; private set; } = null!;
    public bool IsActive { get; private set; } = true;

    public Restaurants.Restaurant Restaurant { get; private set; } = null!;

    private QRCode()
    {
    }

    public static QRCode Create(
        Guid tenantId,
        Guid restaurantId,
        string name,
        string code,
        string targetUrl)
    {
        if (tenantId == Guid.Empty)
            throw new DomainException(
                "QR_TENANT_REQUIRED",
                "A QR code must belong to a tenant.");

        if (restaurantId == Guid.Empty)
            throw new DomainException(
                "QR_RESTAURANT_REQUIRED",
                "A QR code must belong to a restaurant.");

        ValidateName(name);

        var normalizedCode = NormalizeCode(code);
        var normalizedTargetUrl = ValidateTargetUrl(targetUrl);

        return new QRCode
        {
            TenantId = tenantId,
            RestaurantId = restaurantId,
            Name = name.Trim(),
            Code = normalizedCode,
            TargetUrl = normalizedTargetUrl,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
    }

    public static string GenerateCode()
    {
        return RandomNumberGenerator.GetString(CodeAlphabet, CodeLength);
    }

    public void Rename(string name)
    {
        ValidateName(name);

        var normalizedName = name.Trim();

        if (Name == normalizedName)
            return;

        Name = normalizedName;
        UpdatedAt = DateTime.UtcNow;
    }

    public void UpdateTargetUrl(string targetUrl)
    {
        var normalizedTargetUrl = ValidateTargetUrl(targetUrl);

        if (TargetUrl == normalizedTargetUrl)
            return;

        TargetUrl = normalizedTargetUrl;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetActive(bool active)
    {
        if (IsActive == active)
            return;

        IsActive = active;
        UpdatedAt = DateTime.UtcNow;
    }

    private static string NormalizeCode(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new DomainException(
                "QR_CODE_INVALID",
                "QR routing code is required.");

        var normalizedCode = code.Trim().ToLowerInvariant();

        if (normalizedCode.Length != CodeLength ||
            normalizedCode.Any(c => !CodeAlphabet.Contains(c)))
        {
            throw new DomainException(
                "QR_CODE_INVALID",
                $"QR routing code must contain exactly {CodeLength} valid characters.");
        }

        return normalizedCode;
    }

    private static string ValidateTargetUrl(string targetUrl)
    {
        if (string.IsNullOrWhiteSpace(targetUrl) ||
            !Uri.TryCreate(targetUrl.Trim(), UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp &&
             uri.Scheme != Uri.UriSchemeHttps) ||
            string.IsNullOrWhiteSpace(uri.Host))
        {
            throw new DomainException(
                "QR_TARGET_INVALID",
                "QR target must be an absolute HTTP or HTTPS URL.");
        }

        return uri.AbsoluteUri;
    }

    private static void ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException(
                "QR_NAME_REQUIRED",
                "QR code name is required.");

        if (name.Trim().Length > 150)
            throw new DomainException(
                "QR_NAME_TOO_LONG",
                "QR code name cannot exceed 150 characters.");
    }
}

