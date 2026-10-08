namespace RestaurantSaaS.Domain.Enums;

/// <summary>
/// Order lifecycle. Legal transitions are enforced in the domain layer
/// (Order.CanTransitionTo) — never trust a client-supplied status jump.
/// New -> Confirmed -> Preparing -> Ready -> Completed ; Cancelled from non-terminal states.
/// </summary>
public enum OrderStatus
{
    New = 1,
    Confirmed = 2,
    Preparing = 3,
    Ready = 4,
    Completed = 5,
    Cancelled = 6
}
