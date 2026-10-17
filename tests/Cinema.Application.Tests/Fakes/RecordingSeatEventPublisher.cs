using Cinema.Application.Abstractions;
using Cinema.Domain.Seats;

namespace Cinema.Application.Tests.Fakes;

/// <summary>Publicador que solo guarda los eventos recibidos, para verificarlos en los tests.</summary>
public sealed class RecordingSeatEventPublisher : ISeatEventPublisher
{
    private readonly List<SeatEvent> _published = [];

    public IReadOnlyList<SeatEvent> Published => _published;

    public Task PublishAsync(SeatEvent seatEvent, CancellationToken cancellationToken)
    {
        _published.Add(seatEvent);
        return Task.CompletedTask;
    }
}
