namespace RestaurantSaaS.Domain.Common;

public interface ITenantOwned
{
    Guid TenantId { get; set; }
}
