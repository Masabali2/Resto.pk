using Microsoft.EntityFrameworkCore;
using RestaurantSaaS.Application.Common.Interfaces;
using RestaurantSaaS.Domain.Enums;
using RestaurantSaaS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;

namespace RestaurantSaaS.Infrastructure.Tenancy;

public sealed class PublicTenantResolver(
    IHttpContextAccessor httpContextAccessor,
    ApplicationDbContext db,
    ICurrentUserService currentUser) : ITenantResolver
{
    /// <summary>Configured apex domain(s) whose first label is treated as a tenant slug.</summary>
    public const string RootDomainConfigurationKey = "Tenancy:PublicRootDomains";

    public async Task<Guid?> ResolveAsync(CancellationToken cancellationToken = default)
    {
        // Public resolver is for ANONYMOUS traffic only — authenticated dashboard
        // traffic must go through AuthenticatedTenantResolver (membership-checked).
        if (currentUser.IsAuthenticated)
            return null;

        var http = httpContextAccessor.HttpContext;
        if (http is null)
            return null;

        string? slug = ExtractSubdomainLabel(http.Request.Host.Host);

        if (slug is null &&
            http.Request.Query.TryGetValue("tenantSlug", out var q) &&
            !string.IsNullOrWhiteSpace(q.ToString()))
        {
            slug = Domain.Entities.Tenants.Tenant.NormalizeSlug(q.ToString());
        }

        if (slug is null)
            return null;

        var previous = db.DisableTenantFilters;
        db.DisableTenantFilters = true; // pre-trust lookup by design: no tenant context yet
        try
        {
            var tenantId = await db.Tenants.AsNoTracking()
                .Where(t => t.Slug == slug && t.Status == TenantStatus.Active && !t.IsDeleted)
                .Select(t => t.Id)
                .FirstOrDefaultAsync(cancellationToken);

            return tenantId == Guid.Empty ? null : tenantId;
        }
        finally
        {
            db.DisableTenantFilters = previous;
        }
    }

    private static string? ExtractSubdomainLabel(string host)
    {
        // "pizza.saas.com" -> "pizza"; bare "saas.com" / "www" / "api" -> null.
        var parts = host.Split('.');
        if (parts.Length < 3)
            return null;

        var label = parts[0];
        return label is "www" or "api" ? null : label.ToLowerInvariant();
    }
}
