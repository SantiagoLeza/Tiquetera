using Cinema.Domain.Seats;

namespace Cinema.Application.Abstractions;

/// <summary>
/// Publica los eventos de asientos (ADR-010). Los handlers lo invocan después de persistir:
/// publicar antes podría anunciar una selección que luego pierde la carrera de concurrencia.
/// </summary>
/// <remarks>
/// <para>
/// Contrato: una implementación no debe lanzar excepciones por fallas al notificar. Cuando se publica,
/// el cambio ya está confirmado en la base, y fallar el request le diría al cliente que la operación
/// no ocurrió cuando sí ocurrió.
/// </para>
/// <para>
/// Extensión prevista: una implementación que envíe los eventos por Server-Sent Events (<c>GET /seats/events</c>)
/// se registra en DI sin tocar los handlers. Si la API corre en varias instancias, esa implementación
/// debería difundir por un canal compartido (PostgreSQL <c>LISTEN/NOTIFY</c> o Redis) para que el evento
/// llegue a los clientes conectados a cualquier instancia. Los vencimientos no generan eventos (expiración
/// perezosa, ADR-004): el cliente los resuelve con el <c>ExpiresAt</c> de <see cref="SeatSelected"/>.
/// </para>
/// </remarks>
public interface ISeatEventPublisher
{
    Task PublishAsync(SeatEvent seatEvent, CancellationToken cancellationToken);
}
