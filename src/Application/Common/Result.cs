using System.Diagnostics.CodeAnalysis;

namespace Application.Common;

/// <summary>How an <see cref="AppError"/> maps onto HTTP; the API's central mapper owns the status codes.</summary>
public enum ErrorKind
{
    Validation,
    Unauthorized,
    Forbidden,
    NotFound,
    Conflict,
    Gone,
}

/// <summary>
/// A domain/application failure. <see cref="Code"/> is one of <c>Contracts.ErrorCodes</c> (or null);
/// <see cref="Errors"/> holds per-field messages (camelCase keys) for <see cref="ErrorKind.Validation"/>.
/// </summary>
public sealed record AppError(ErrorKind Kind, string? Code, string Message, IReadOnlyDictionary<string, string[]>? Errors = null)
{
    public static AppError Validation(IReadOnlyDictionary<string, string[]> errors) =>
        new(ErrorKind.Validation, null, "One or more validation errors occurred.", errors);

    public static AppError Conflict(string code, string message) => new(ErrorKind.Conflict, code, message);
}

/// <summary>Either a value or an <see cref="AppError"/>.</summary>
public readonly struct Result<T>
{
    private readonly T? _value;
    private readonly AppError? _error;

    private Result(T? value, AppError? error)
    {
        _value = value;
        _error = error;
    }

    [MemberNotNullWhen(false, nameof(_error))]
    public bool IsSuccess => _error is null;

    public T Value => IsSuccess ? _value! : throw new InvalidOperationException($"Result is an error: {_error.Message}");

    public AppError Error => _error ?? throw new InvalidOperationException("Result is a success.");

    public static Result<T> Success(T value) => new(value, null);
    public static Result<T> Failure(AppError error) => new(default, error);

    public static implicit operator Result<T>(T value) => Success(value);
    public static implicit operator Result<T>(AppError error) => Failure(error);
}
