namespace RestaurantSaaS.Application.Common.Interfaces;

/// <summary>
/// Ambient tenant for the current request/job. Populated by tenant RESOLUTION
/// (authenticated membership or public slug/subdomain), consumed by EF Core
/// global query filters, ownership validation, SignalR groups and file storage.
///
/// SECURITY: This is set exclusively server-side. A client-supplied TenantId
/// is never read into this context.
/// </summary>
public interface ITenantContext
{
    Guid? TenantId { get; }

    bool HasTenant { get; }

    /// <summary>Throws if no tenant resolved — use inside tenant-scoped use cases as a hard guard.</summary>
    Guid GetRequiredTenantId();
}

/// <summary>Writable side used only by the tenant-resolution middleware / job activator.</summary>
public interface ITenantContextSetter
{
    void SetTenant(Guid tenantId);
}
