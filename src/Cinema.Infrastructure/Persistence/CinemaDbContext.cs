using Cinema.Domain.Seats;
using Microsoft.EntityFrameworkCore;

namespace Cinema.Infrastructure.Persistence;

public sealed class CinemaDbContext(DbContextOptions<CinemaDbContext> options) : DbContext(options)
{
    public DbSet<Seat> Seats => Set<Seat>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Aplica todas las clases IEntityTypeConfiguration<T> de este ensamblado (por ahora, SeatConfiguration).
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CinemaDbContext).Assembly);
    }
}
