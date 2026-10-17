using System.ComponentModel;
using Cinema.Api.Http;
using Cinema.Application.Seats;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Cinema.Api.Endpoints;

/// <summary>
/// Endpoints del recurso asiento (ADR-008, ADR-009). Cada endpoint solo traduce HTTP ↔ caso de uso:
/// recibe el request, invoca el handler y convierte el Result en una respuesta.
/// </summary>
public static class SeatEndpoints
{
    public const string HoldTokenHeader = "X-Hold-Token";

    private const string HoldTokenDescription =
        "Hold token devuelto por POST /seats/{id}/selection. Prueba que quien confirma o libera es quien seleccionó.";

    public static IEndpointRouteBuilder MapSeatEndpoints(this IEndpointRouteBuilder app)
    {
        var seats = app.MapGroup("/seats").WithTags("Seats");

        seats.MapGet("/", GetSeats)
            .WithName("GetSeats")
            .WithSummary("Lista los asientos con su estado efectivo")
            .WithDescription("Una selección vencida se informa como Free. Nunca incluye hold tokens.");

        seats.MapPost("/{id}/selection", SelectSeat)
            .WithName("SelectSeat")
            .WithSummary("Selecciona un asiento")
            .WithDescription("Devuelve el hold token que exigen la confirmación y la liberación, y el vencimiento de la selección.")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        seats.MapPut("/{id}/reservation", ConfirmReservation)
            .WithName("ConfirmReservation")
            .WithSummary("Confirma la reserva de un asiento seleccionado")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status410Gone);

        seats.MapDelete("/{id}/selection", ReleaseSeat)
            .WithName("ReleaseSeat")
            .WithSummary("Libera un asiento seleccionado")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status410Gone);

        return app;
    }

    private static async Task<Ok<IReadOnlyList<SeatDto>>> GetSeats(
        GetSeatsHandler handler,
        CancellationToken cancellationToken)
    {
        var seats = await handler.HandleAsync(cancellationToken);
        return TypedResults.Ok(seats);
    }

    private static async Task<Results<Created<SeatSelectionDto>, ProblemHttpResult>> SelectSeat(
        string id,
        SelectSeatHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(id, cancellationToken);
        return result.IsSuccess
            ? TypedResults.Created($"/seats/{id}/selection", result.Value)
            : result.Error.ToProblem();
    }

    // Header obligatorio y tipado como Guid: si falta o no es un GUID válido, ASP.NET responde 400 antes de llegar acá.
    private static async Task<Results<Ok<SeatDto>, ProblemHttpResult>> ConfirmReservation(
        string id,
        [FromHeader(Name = HoldTokenHeader), Description(HoldTokenDescription)] Guid holdToken,
        ConfirmReservationHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(id, holdToken, cancellationToken);
        return result.IsSuccess
            ? TypedResults.Ok(result.Value)
            : result.Error.ToProblem();
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> ReleaseSeat(
        string id,
        [FromHeader(Name = HoldTokenHeader), Description(HoldTokenDescription)] Guid holdToken,
        ReleaseSeatHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(id, holdToken, cancellationToken);
        return result.IsSuccess
            ? TypedResults.NoContent()
            : result.Error.ToProblem();
    }
}
