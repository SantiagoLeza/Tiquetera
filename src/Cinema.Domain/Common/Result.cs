using System.Diagnostics.CodeAnalysis;

namespace Cinema.Domain.Common;

/// <summary>
/// Resultado de una operación que puede fallar por un motivo de negocio esperado (ADR-005).
/// Las fallas inesperadas (bugs, infraestructura) siguen siendo excepciones.
/// </summary>
public class Result
{
    protected Result(Error? error)
    {
        Error = error;
    }

    /// <summary>El error si la operación falló; <c>null</c> si tuvo éxito.</summary>
    public Error? Error { get; }

    [MemberNotNullWhen(false, nameof(Error))]
    public bool IsSuccess => Error is null;

    [MemberNotNullWhen(true, nameof(Error))]
    public bool IsFailure => !IsSuccess;

    public static Result Success() => new(null);

    public static Result Failure(Error error) => new(error);

    /// <summary>Permite escribir <c>return SeatErrors.NotSelected(id);</c> en un método que devuelve <see cref="Result"/>.</summary>
    public static implicit operator Result(Error error) => Failure(error);
}

/// <summary>Resultado que, en caso de éxito, transporta un valor.</summary>
public sealed class Result<T> : Result
{
    private readonly T? _value;

    private Result(T value) : base(null)
    {
        _value = value;
    }

    private Result(Error error) : base(error)
    {
    }

    /// <summary>El valor de una operación exitosa. Leerlo en un resultado fallido es un bug, por eso lanza.</summary>
    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException($"No se puede leer el valor de un resultado fallido ({Error.Code}).");

    public static Result<T> Success(T value) => new(value);

    public static new Result<T> Failure(Error error) => new(error);

    public static implicit operator Result<T>(T value) => Success(value);

    public static implicit operator Result<T>(Error error) => Failure(error);
}
