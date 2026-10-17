using Cinema.Domain.Common;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Cinema.Api.Http;

/// <summary>
/// Único punto donde un error de negocio se traduce a HTTP (ADR-005): ErrorType → status code,
/// con el cuerpo en formato ProblemDetails (RFC 9457).
/// </summary>
public static class ErrorHttpMapping
{
    public static ProblemHttpResult ToProblem(this Error error)
    {
        var (statusCode, title) = error.Type switch
        {
            ErrorType.NotFound => (StatusCodes.Status404NotFound, "Recurso inexistente"),
            ErrorType.Conflict => (StatusCodes.Status409Conflict, "Conflicto con el estado del asiento"),
            ErrorType.Forbidden => (StatusCodes.Status403Forbidden, "Hold token inválido"),
            ErrorType.Gone => (StatusCodes.Status410Gone, "Selección vencida"),
            _ => throw new ArgumentOutOfRangeException(nameof(error), error.Type, "Tipo de error sin mapeo HTTP."),
        };

        return TypedResults.Problem(
            title: title,
            detail: error.Message,
            statusCode: statusCode,
            // "code" es estable y apto para que el cliente decida qué hacer; title y detail son para humanos.
            extensions: new Dictionary<string, object?> { ["code"] = error.Code });
    }
}
