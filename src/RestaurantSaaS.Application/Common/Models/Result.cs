namespace RestaurantSaaS.Application.Common.Models;

/// <summary>Lightweight functional result for use cases that must not throw on expected failures.</summary>
public sealed class Result
{
    public bool IsSuccess { get; }
    public string? Error { get; }
    public string? ErrorCode { get; }

    private Result(bool isSuccess, string? errorCode, string? error)
    {
        IsSuccess = isSuccess;
        ErrorCode = errorCode;
        Error = error;
    }

    public static Result Success() => new(true, null, null);
    public static Result Failure(string errorCode, string error) => new(false, errorCode, error);
}

public sealed class Result<T> : Result
{
    public T? Value { get; }

    private Result(T? value, bool isSuccess, string? errorCode, string? error)
        : base(isSuccess, errorCode, error)
    {
        Value = value;
    }

    public static Result<T> Success(T value) => new(value, true, null, null);
    public static new Result<T> Failure(string errorCode, string error) => new(default, false, errorCode, error);
}
