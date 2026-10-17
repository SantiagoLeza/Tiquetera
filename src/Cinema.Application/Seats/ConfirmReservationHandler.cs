using Cinema.Application.Abstractions;
using Cinema.Domain.Common;
using Cinema.Domain.Seats;

namespace Cinema.Application.Seats;

/// <summary>Caso de uso: confirmar la reserva de un asiento seleccionado.</summary>
public sealed class ConfirmReservationHandler(
    ISeatRepository seats,
    ISeatEventPublisher events,
    TimeProvider timeProvider)
{
    public async Task<Result<SeatDto>> HandleAsync(string seatId, Guid holdToken, CancellationToken cancellationToken)
    {
        var seat = await seats.GetByIdAsync(seatId, cancellationToken);
        if (seat is null)
        {
            return SeatErrors.NotFound(seatId);
        }

        var now = timeProvider.GetUtcNow();
        var confirmation = seat.Confirm(holdToken, now);
        if (confirmation.IsFailure)
        {
            return confirmation.Error;
        }

        var saved = await seats.SaveAsync(seat, cancellationToken);
        if (saved.IsFailure)
        {
            return saved.Error;
        }

        await events.PublishAsync(new SeatReserved(seat.Id, now), cancellationToken);

        return SeatDto.From(seat, now);
    }
}
