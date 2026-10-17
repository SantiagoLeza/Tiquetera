using Cinema.Domain.Common;
using Cinema.Domain.Seats;

namespace Cinema.Application.Abstractions;

/// <summary>
/// Persistencia de asientos. La define Application y la implementa Infrastructure (ADR-007).
/// </summary>
public interface ISeatRepository
{
    /// <summary>Todos los asientos, para consulta. Las entidades devueltas no deben modificarse.</summary>
    Task<IReadOnlyList<Seat>> GetAllAsync(CancellationToken cancellationToken);

    /// <summary>El asiento con ese id, listo para modificarse y guardarse con <see cref="SaveAsync"/>; <c>null</c> si no existe.</summary>
    Task<Seat?> GetByIdAsync(string seatId, CancellationToken cancellationToken);

    /// <summary>
    /// Persiste los cambios hechos sobre un asiento obtenido con <see cref="GetByIdAsync"/>.
    /// </summary>
    /// <returns>
    /// Falla con <see cref="SeatErrors.ConcurrentModification"/> si otra operación modificó el asiento
    /// después de leerlo (ADR-002). Perder una carrera es un resultado esperado, no una excepción (ADR-005):
    /// la implementación traduce el error de concurrencia de la base, y así Application no conoce a EF Core.
    /// </returns>
    Task<Result> SaveAsync(Seat seat, CancellationToken cancellationToken);
}
