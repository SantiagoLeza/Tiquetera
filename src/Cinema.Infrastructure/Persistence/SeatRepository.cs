using Cinema.Application.Abstractions;
using Cinema.Domain.Common;
using Cinema.Domain.Seats;
using Microsoft.EntityFrameworkCore;

namespace Cinema.Infrastructure.Persistence;

internal sealed class SeatRepository(CinemaDbContext db) : ISeatRepository
{
    public async Task<IReadOnlyList<Seat>> GetAllAsync(CancellationToken cancellationToken) =>
        // Solo lectura: sin tracking, EF no guarda una copia de cada entidad para detectar cambios.
        await db.Seats.AsNoTracking().ToListAsync(cancellationToken);

    public async Task<Seat?> GetByIdAsync(string seatId, CancellationToken cancellationToken) =>
        // Con tracking: EF recuerda el valor original de xmin para usarlo en el WHERE del UPDATE.
        await db.Seats.SingleOrDefaultAsync(seat => seat.Id == seatId, cancellationToken);

    public async Task<Result> SaveAsync(Seat seat, CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return Result.Success();
        }
        catch (DbUpdateConcurrencyException)
        {
            // El UPDATE afectó 0 filas: otra operación cambió el asiento (y su xmin) después de que lo leímos.
            // Es una carrera perdida, un resultado de negocio esperado (ADR-002, ADR-005).
            return SeatErrors.ConcurrentModification(seat.Id);
        }
    }
}
