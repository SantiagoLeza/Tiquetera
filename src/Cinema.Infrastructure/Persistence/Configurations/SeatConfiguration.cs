using Cinema.Domain.Seats;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cinema.Infrastructure.Persistence.Configurations;

/// <summary>
/// Mapeo de <see cref="Seat"/> a la tabla <c>seats</c>. El dominio no tiene atributos de EF: todo se declara acá.
/// El esquema lo define <c>db/init.sql</c>, no EF: este mapeo debe coincidir con ese script
/// (los tests de integración corren contra él y detectan cualquier diferencia).
/// </summary>
internal sealed class SeatConfiguration : IEntityTypeConfiguration<Seat>
{
    public void Configure(EntityTypeBuilder<Seat> builder)
    {
        builder.ToTable("seats");

        builder.HasKey(seat => seat.Id);

        builder.Property(seat => seat.Id)
            .HasColumnName("id")
            .HasMaxLength(4)
            .ValueGeneratedNever();

        builder.Property(seat => seat.Row)
            .HasColumnName("row")
            .HasMaxLength(1)
            .IsRequired();

        builder.Property(seat => seat.Number)
            .HasColumnName("number");

        // Status y HoldToken son propiedades privadas de Seat: EF las mapea por nombre.
        builder.Property<SeatStatus>("Status")
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(16)
            .IsRequired();

        builder.Property<Guid?>("HoldToken")
            .HasColumnName("hold_token");

        builder.Property(seat => seat.ExpiresAt)
            .HasColumnName("expires_at");

        // Token de concurrencia optimista (ADR-002). Npgsql mapea una propiedad uint marcada como row version
        // a la columna de sistema xmin, que PostgreSQL cambia en cada UPDATE de la fila: no se crea ninguna columna.
        // Es una shadow property (existe en el modelo de EF, no en la clase Seat), así el dominio no conoce versiones.
        // Cada UPDATE se emite como: UPDATE seats SET ... WHERE id = @id AND xmin = @versionLeida.
        builder.Property<uint>("Version")
            .IsRowVersion();
    }
}
