using Cinema.Domain.Seats;

namespace Cinema.Infrastructure.Persistence;

/// <summary>
/// Composición fija de la sala: 8 filas (A–H) × 10 asientos (ADR-012).
/// Vive en Infrastructure porque solo se usa para el seed: el dominio valida un asiento, no conoce la sala.
/// </summary>
public static class AuditoriumLayout
{
    public const string Rows = "ABCDEFGH";
    public const int SeatsPerRow = 10;

    public static IEnumerable<Seat> CreateSeats() =>
        from row in Rows
        from number in Enumerable.Range(1, SeatsPerRow)
        select new Seat(row.ToString(), number);
}
