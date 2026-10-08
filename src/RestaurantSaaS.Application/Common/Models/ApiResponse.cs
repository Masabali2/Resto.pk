namespace RestaurantSaaS.Application.Common.Models;

/// <summary>Uniform envelope for API responses — controllers never return EF entities.</summary>
public sealed class ApiResponse<T>
{
    public bool Success { get; init; }
    public T? Data { get; init; }
    public string? ErrorCode { get; init; }
    public string? Message { get; init; }
    public IReadOnlyList<ValidationError>? Errors { get; init; }
    public string? TraceId { get; init; }

    public static ApiResponse<T> Ok(T data) => new() { Success = true, Data = data };

    public static ApiResponse<T> Fail(string errorCode, string message, IReadOnlyList<ValidationError>? errors = null)
        => new() { Success = false, ErrorCode = errorCode, Message = message, Errors = errors };
}

public sealed record ValidationError(string Field, string Message);
