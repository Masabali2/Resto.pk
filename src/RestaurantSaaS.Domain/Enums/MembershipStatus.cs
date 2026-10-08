namespace RestaurantSaaS.Domain.Enums;

public enum MembershipStatus
{
    /// <summary>Invitation sent, user has not accepted/completed signup yet.</summary>
    Invited = 1,

    /// <summary>Active member of the tenant.</summary>
    Active = 2,

    /// <summary>Access revoked without removing history.</summary>
    Suspended = 3,

    /// <summary>Membership ended.</summary>
    Removed = 4
}
