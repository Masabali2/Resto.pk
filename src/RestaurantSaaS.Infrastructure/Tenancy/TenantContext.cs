using RestaurantSaaS.Application.Common.Interfaces;

namespace RestaurantSaaS.Infrastructure.Tenancy;

public sealed class TenantContext : ITenantContext, ITenantContextSetter
{
    private Guid? _tenantId;

    public Guid? TenantId => _tenantId;

    public bool HasTenant => _tenantId.HasValue;

    public void SetTenant(Guid tenantId)
    {
        if (_tenantId is null)
            _tenantId = tenantId == Guid.Empty ? null : tenantId;
    }

    public Guid GetRequiredTenantId() =>
        _tenantId ?? throw new InvalidOperationException(
            "No tenant has been resolved for the current scope.");
}