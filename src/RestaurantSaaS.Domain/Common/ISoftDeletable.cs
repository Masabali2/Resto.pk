namespace RestaurantSaaS.Domain.Common;

/// <summary>
/// Marker for entities whose deletion is logical rather than physical.
/// Historical records (orders, audit logs) must remain queryable even when
/// the referenced business entity is "deleted".
/// A global query filter hides soft-deleted rows by default (see DbContext).
/// </summary>
public interface ISoftDeletable
{
    bool IsDeleted { get; set; }
    DateTime? DeletedAt { get; set; }
}
