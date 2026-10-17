using Cinema.Domain.Common;

namespace Cinema.Domain.Seats;

/// <summary>Catálogo de errores de negocio relacionados con asientos.</summary>
public static class SeatErrors
{
    public static Error NotFound(string seatId) =>
        new("seat.not_found", $"El asiento {seatId} no existe.", ErrorType.NotFound);

    public static Error AlreadySelected(string seatId) =>
        new("seat.already_selected", $"El asiento {seatId} ya está seleccionado.", ErrorType.Conflict);

    public static Error AlreadyReserved(string seatId) =>
        new("seat.already_reserved", $"El asiento {seatId} ya está reservado.", ErrorType.Conflict);

    public static Error NotSelected(string seatId) =>
        new("seat.not_selected", $"El asiento {seatId} no tiene una selección vigente.", ErrorType.Conflict);

    public static Error InvalidHoldToken(string seatId) =>
        new("seat.invalid_hold_token", $"El hold token no corresponde a la selección vigente del asiento {seatId}.", ErrorType.Forbidden);

    public static Error SelectionExpired(string seatId) =>
        new("seat.selection_expired", $"La selección del asiento {seatId} venció.", ErrorType.Gone);

    /// <summary>
    /// Otra operación modificó el asiento entre la lectura y la escritura (ADR-002).
    /// El dominio no lo detecta: lo produce la capa de persistencia y se traduce a este error fuera del dominio.
    /// </summary>
    public static Error ConcurrentModification(string seatId) =>
        new("seat.concurrent_modification", $"El asiento {seatId} fue modificado por otra operación. Consultá su estado y reintentá.", ErrorType.Conflict);
}
