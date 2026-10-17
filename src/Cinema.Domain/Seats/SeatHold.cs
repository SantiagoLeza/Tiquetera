namespace Cinema.Domain.Seats;

/// <summary>
/// Lo que obtiene quien selecciona un asiento: el token que prueba la propiedad de la selección
/// y el instante en que vence. Es la única vía por la que el token sale de <see cref="Seat"/> (ADR-003).
/// </summary>
public sealed record SeatHold(Guid Token, DateTimeOffset ExpiresAt);
