namespace RestaurantSaaS.Domain.Common;

/// <summary>
/// Entities that carry creation/modification audit stamps.
/// Stamps are populated by an EF Core SaveChanges interceptor in Infrastructure,
/// never by clients. All timestamps are UTC.
/// </summary>
public abstract class AuditableEntity : BaseEntity
{
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
