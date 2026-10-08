namespace RestaurantSaaS.Domain.Exceptions;

/// <summary>
/// Thrown when a domain invariant / business rule is violated.
/// Mapped to HTTP 400/422 by the API exception middleware — never leaks stack traces.
/// </summary>
public class DomainException : Exception
{
    /// <summary>Stable machine-readable error code (no magic strings scattered across layers).</summary>
    public string Code { get; }

    public DomainException(string code, string message)
        : base(message)
    {
        Code = code;
    }

    public DomainException(string code, string message, Exception innerException)
        : base(message, innerException)
    {
        Code = code;
    }
}
