namespace Cinema.Domain.Seats;

/// <summary>
/// Hecho ocurrido sobre un asiento (ADR-010). Se publica solo después de persistir el cambio con éxito.
/// Ningún evento transporta el hold token: los eventos están pensados para difundirse a cualquier cliente.
/// </summary>
public abstract record SeatEvent(string SeatId, DateTimeOffset OccurredAt);

public sealed record SeatSelected(string SeatId, DateTimeOffset ExpiresAt, DateTimeOffset OccurredAt)
    : SeatEvent(SeatId, OccurredAt);

public sealed record SeatReserved(string SeatId, DateTimeOffset OccurredAt)
    : SeatEvent(SeatId, OccurredAt);

public sealed record SeatReleased(string SeatId, DateTimeOffset OccurredAt)
    : SeatEvent(SeatId, OccurredAt);
