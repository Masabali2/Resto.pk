namespace RestaurantSaaS.Application.Common.Interfaces;

/// <summary>Injectable clock — all timestamps UTC. Enables deterministic tests.</summary>
public interface IDateTimeProvider
{
    DateTime UtcNow { get; }
}
