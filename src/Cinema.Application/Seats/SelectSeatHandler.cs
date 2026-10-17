using Cinema.Application.Abstractions;
using Cinema.Domain.Common;
using Cinema.Domain.Seats;
using Microsoft.Extensions.Options;

namespace Cinema.Application.Seats;

/// <summary>Caso de uso: seleccionar un asiento por el TTL configurado.</summary>
public sealed class SelectSeatHandler(
    ISeatRepository seats,
    ISeatEventPublisher events,
    TimeProvider timeProvider,
    IOptions<SeatSelectionOptions> options)
{
    public async Task<Result<SeatSelectionDto>> HandleAsync(string seatId, CancellationToken cancellationToken)
    {
        var seat = await seats.GetByIdAsync(seatId, cancellationToken);
        if (seat is null)
        {
            return SeatErrors.NotFound(seatId);
        }

        var now = timeProvider.GetUtcNow();
        var selection = seat.Select(now, options.Value.Ttl);
        if (selection.IsFailure)
        {
            return selection.Error;
        }

        var saved = await seats.SaveAsync(seat, cancellationToken);
        if (saved.IsFailure)
        {
            return saved.Error;
        }

        var hold = selection.Value;
        await events.PublishAsync(new SeatSelected(seat.Id, hold.ExpiresAt, now), cancellationToken);

        return new SeatSelectionDto(
            seat.Id,
            hold.Token,
            hold.ExpiresAt,
            (int)(hold.ExpiresAt - now).TotalSeconds);
    }
}
