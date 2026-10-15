namespace RestaurantSaaS.Domain.Enums;

public enum NotificationType
{
    /// <summary>A new customer order arrived (SignalR push to kitchen/manager groups).</summary>
    NewOrder = 1,

    /// <summary>An order changed status.</summary>
    OrderStatusChanged = 2,

    /// <summary>Subscription trial/period ending.</summary>
    SubscriptionExpiring = 3,

    /// <summary>A staff invitation or membership change.</summary>
    StaffUpdate = 4,

    /// <summary>Generic system message for the tenant.</summary>
    System = 5,

    /// <summary>A successful sign-in security notification (email alert).</summary>
    SecuritySignIn = 6
}
