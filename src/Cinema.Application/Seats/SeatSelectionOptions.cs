namespace Cinema.Application.Seats;

/// <summary>Configuración de la selección de asientos. Se carga de la sección <c>SeatSelection</c> de appsettings.json.</summary>
public sealed class SeatSelectionOptions
{
    public const string SectionName = "SeatSelection";

    /// <summary>Tiempo que tiene quien selecciona un asiento para confirmarlo (ADR-004).</summary>
    public TimeSpan Ttl { get; init; } = TimeSpan.FromMinutes(5);
}
