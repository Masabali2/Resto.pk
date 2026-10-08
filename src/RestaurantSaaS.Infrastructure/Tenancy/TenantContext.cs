using RestaurantSaaS.Application.Common.Interfaces;

namespace RestaurantSaaS.Infrastructure.Tenancy;

/// <summary>
/// Scoped, mutable holder for the current request's tenant. Registered per
/// request; written ONLY by TenantResolutionMiddleware (or a Hangfire job
/// activator). Read by EF Core global query filters and use cases.
/// </summary>
public sealed class TenantContext : ITenantContext, ITenantContextSetter
{
    private Guid? _tenantId;

    public Guid? TenantId => _tenantId;

    public bool HasTenant => _tenantId.HasValue;

    public void SetTenant(Guid tenantId)
    {
        // First resolution wins; prevents accidental overwrite mid-request.
        if (_tenantId is null)
            _tenantId = tenantId == Guid.Empty ? null : tenantId;
    }

    public Guid GetRequiredTenantId() =>
        _tenantId ?? throw new InvalidOperationException(
            "No tenant has been resolved for the current scope. Tenant-scoped operations require a resolved tenant.");
}
