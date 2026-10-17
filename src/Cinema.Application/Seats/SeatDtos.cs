using Cinema.Domain.Seats;

namespace Cinema.Application.Seats;

/// <summary>
/// Vista pública de un asiento. No incluye el hold token (ADR-003).
/// <paramref name="ExpiresAt"/> solo tiene valor si el asiento está efectivamente seleccionado.
/// </summary>
public sealed record SeatDto(string Id, string Row, int Number, SeatStatus Status, DateTimeOffset? ExpiresAt)
{
    public static SeatDto From(Seat seat, DateTimeOffset now)
    {
        var status = seat.GetStatus(now);
        return new SeatDto(
            seat.Id,
            seat.Row,
            seat.Number,
            status,
            status == SeatStatus.Selected ? seat.ExpiresAt : null);
    }
}

/// <summary>
/// Respuesta a una selección exitosa. Es la única respuesta que incluye el hold token.
/// <paramref name="ExpiresInSeconds"/> le permite al cliente mostrar una cuenta regresiva
/// sin depender de que su reloj coincida con el del servidor (ADR-004).
/// </summary>
public sealed record SeatSelectionDto(string SeatId, Guid HoldToken, DateTimeOffset ExpiresAt, int ExpiresInSeconds);
