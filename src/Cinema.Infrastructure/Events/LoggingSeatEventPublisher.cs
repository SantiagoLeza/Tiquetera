using Cinema.Application.Abstractions;
using Cinema.Domain.Seats;
using Microsoft.Extensions.Logging;

namespace Cinema.Infrastructure.Events;

/// <summary>
/// Implementación inicial de <see cref="ISeatEventPublisher"/>: registra cada evento en el log (ADR-010).
/// </summary>
/// <remarks>
/// Extensión a tiempo real: agregar, por ejemplo, un <c>ServerSentEventsSeatEventPublisher</c> que escriba los
/// eventos en un canal (<c>System.Threading.Channels</c>) leído por el endpoint <c>GET /seats/events</c>, y registrarlo
/// en DI en lugar de este (o junto con este, mediante un publicador compuesto). Los handlers no cambian.
/// Con varias instancias de la API, ese publicador debería difundir por PostgreSQL <c>LISTEN/NOTIFY</c> o Redis.
/// </remarks>
internal sealed class LoggingSeatEventPublisher(ILogger<LoggingSeatEventPublisher> logger) : ISeatEventPublisher
{
    public Task PublishAsync(SeatEvent seatEvent, CancellationToken cancellationToken)
    {
        // Plantilla estructurada: SeatId y EventType quedan como campos consultables, no solo como texto.
        logger.LogInformation(
            "Evento de asiento {EventType} en {SeatId}: {@SeatEvent}",
            seatEvent.GetType().Name,
            seatEvent.SeatId,
            seatEvent);

        return Task.CompletedTask;
    }
}
