namespace RestaurantSaaS.Application.Features.Subscriptions;

/// <summary>
/// Single source of truth for the free-trial rules.
///
/// The policy lives in the Application layer because both the API (error
/// messaging, contract documentation) and the Infrastructure layer (trial
/// activation) must agree on the same duration and the same backing plan.
/// </summary>
public static class SubscriptionTrialPolicy
{
    /// <summary>
    /// Length of the free trial, in days. The trial starts the moment the
    /// backend accepts the activation request (UTC) and ends exactly 30 days
    /// later.
    /// </summary>
    public const int TrialDurationDays = 30;

    /// <summary>
    /// Name of the subscription plan that backs the free trial.
    ///
    /// The trial always runs on this plan: the client never chooses the plan a
    /// trial grants, so a client cannot use the trial to unlock a paid plan.
    /// The plan is seeded by <c>DbSeeder</c>; platform administrators manage
    /// the rest of the catalogue through the existing plan endpoints.
    /// </summary>
    public const string TrialPlanName = "Free Trial";

    /// <summary>
    /// Computes the exact end of a trial that starts at <paramref name="startsAt"/>.
    /// </summary>
    public static DateTime CalculateTrialEnd(DateTime startsAt) =>
        startsAt.AddDays(TrialDurationDays);
}
