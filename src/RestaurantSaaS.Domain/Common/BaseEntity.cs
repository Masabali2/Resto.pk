namespace RestaurantSaaS.Domain.Common;

/// <summary>
/// Base type for all persisted entities. Identity (GUID) keys are used because
/// keys may be generated client-side (e.g. guest carts/sessions) and because
/// GUIDs avoid sequential-key enumeration attacks in a public SaaS surface.
/// </summary>
public abstract class BaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
}
