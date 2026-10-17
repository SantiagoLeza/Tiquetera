using Cinema.Application.Abstractions;

namespace Cinema.Application.Seats;

/// <summary>Caso de uso: consultar el estado efectivo de todos los asientos.</summary>
public sealed class GetSeatsHandler(ISeatRepository seats, TimeProvider timeProvider)
{
    public async Task<IReadOnlyList<SeatDto>> HandleAsync(CancellationToken cancellationToken)
    {
        var all = await seats.GetAllAsync(cancellationToken);
        var now = timeProvider.GetUtcNow();

        return all
            .OrderBy(seat => seat.Row)
            .ThenBy(seat => seat.Number)
            .Select(seat => SeatDto.From(seat, now))
            .ToList();
    }
}
