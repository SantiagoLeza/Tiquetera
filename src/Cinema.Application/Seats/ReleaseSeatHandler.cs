using Cinema.Application.Abstractions;
using Cinema.Domain.Common;
using Cinema.Domain.Seats;

namespace Cinema.Application.Seats;

/// <summary>Caso de uso: liberar un asiento seleccionado.</summary>
public sealed class ReleaseSeatHandler(
    ISeatRepository seats,
    ISeatEventPublisher events,
    TimeProvider timeProvider)
{
    public async Task<Result> HandleAsync(string seatId, Guid holdToken, CancellationToken cancellationToken)
    {
        var seat = await seats.GetByIdAsync(seatId, cancellationToken);
        if (seat is null)
        {
            return SeatErrors.NotFound(seatId);
        }

        var now = timeProvider.GetUtcNow();
        var release = seat.Release(holdToken, now);
        if (release.IsFailure)
        {
            return release;
        }

        var saved = await seats.SaveAsync(seat, cancellationToken);
        if (saved.IsFailure)
        {
            return saved;
        }

        await events.PublishAsync(new SeatReleased(seat.Id, now), cancellationToken);

        return Result.Success();
    }
}
