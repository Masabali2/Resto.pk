namespace RestaurantSaaS.Domain.Enums;

public enum CartStatus
{
    Active = 1,

    /// <summary>Converted into an order. Terminal state.</summary>
    Converted = 2,

    /// <summary>Abandoned/expired. Terminal state.</summary>
    Expired = 3,

    /// <summary>Merged after a guest session authenticated. Terminal state.</summary>
    Merged = 4
}
