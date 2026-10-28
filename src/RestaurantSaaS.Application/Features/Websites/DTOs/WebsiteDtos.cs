using RestaurantSaaS.Application.Common.Models;

namespace RestaurantSaaS.Application.Features.Websites.DTOs;

// ===================== Templates (platform-owned catalogue) =====================

public sealed record WebsiteTemplateDto(
    Guid Id,
    string Name,
    string? Description,
    string TemplateKey,
    Guid? PreviewImageFileId,
    bool IsActive,
    DateTime CreatedAt,
    string? DesignConfigurationJson);

public sealed record CreateWebsiteTemplateRequest(
    string Name,
    string TemplateKey,
    string? Description,
    Guid? PreviewImageFileId,
    string? DesignConfigurationJson = null);

public sealed record UpdateWebsiteTemplateRequest(
    string? Name,
    string? Description,
    bool? IsActive,
    string? DesignConfigurationJson = null);

/// <summary>
/// Anonymous, read-only view of an ACTIVE catalogue template.
/// Deliberately excludes administrative metadata (<c>IsActive</c>,
/// <c>CreatedAt</c>) and storage references (<c>PreviewImageFileId</c>):
/// the public marketing site renders the template from
/// <see cref="DesignConfigurationJson"/> only.
/// </summary>
public sealed record PublicWebsiteTemplateDto(
    Guid Id,
    string Name,
    string? Description,
    string TemplateKey,
    string? DesignConfigurationJson);

// ===================== Websites (tenant-owned) =====================

public sealed record WebsiteDto(
    Guid Id,
    Guid TenantId,
    Guid RestaurantId,
    string RestaurantName,
    Guid WebsiteTemplateId,
    string TemplateName,
    string TemplateKey,
    string Name,
    string Slug,
    string Status,
    bool IsPublished,
    DateTime? PublishedAt,
    int? DraftVersion,
    int? PublishedVersion,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public sealed record CreateWebsiteRequest(Guid RestaurantId, Guid WebsiteTemplateId, string Name, string Slug);

public sealed record UpdateWebsiteRequest(string? Name, string? Slug, Guid? WebsiteTemplateId);

/// <summary>Save/replace the DRAFT configuration. Never affects what the public sees.</summary>
public sealed record SaveWebsiteDraftRequest(string ConfigurationJson);

// ===================== Public site DTOs (anonymous, minimal surface) =====================

/// <summary>Deliberately excludes tenant id, staff data, subscription state and internal FKs.</summary>
public sealed record PublicSiteDto(
    string SiteName,
    string Slug,
    PublicRestaurantDto Restaurant,
    PublicTemplateDto Template,
    string ConfigurationJson,
    int ConfigurationVersion,
    DateTime PublishedAt);

public sealed record PublicRestaurantDto(
    string Name,
    string? Description,
    string? Phone,
    string? Email,
    string City,
    string Country,
    string TimeZone);

public sealed record PublicTemplateDto(string Name, string TemplateKey);

public sealed record PublicMenuDto(
    Guid Id,
    string Name,
    string? Description,
    IReadOnlyList<PublicMenuCategoryDto> Categories);

public sealed record PublicMenuCategoryDto(
    Guid Id,
    string Name,
    string? Description,
    int DisplayOrder,
    IReadOnlyList<PublicMenuItemDto> Items);

public sealed record PublicMenuItemDto(
    Guid Id,
    string Name,
    string? Description,
    decimal Price,
    string Currency,
    int DisplayOrder,
    bool IsAvailable,
    Guid? ImageFileId);
