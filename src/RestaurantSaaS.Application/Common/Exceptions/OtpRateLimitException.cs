namespace RestaurantSaaS.Application.Common.Exceptions;

public sealed class OtpRateLimitException(
    string message,
    int retryAfterSeconds) : Exception(message)
{
    public int RetryAfterSeconds { get; } = retryAfterSeconds;
}