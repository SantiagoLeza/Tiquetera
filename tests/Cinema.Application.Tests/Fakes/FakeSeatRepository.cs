using Cinema.Application.Abstractions;
using Cinema.Domain.Common;
using Cinema.Domain.Seats;

namespace Cinema.Application.Tests.Fakes;

/// <summary>
/// Repositorio en memoria para tests de handlers. Devuelve siempre la misma instancia de cada asiento,
/// como haría el change tracker de EF Core dentro de un request.
/// </summary>
public sealed class FakeSeatRepository : ISeatRepository
{
    private readonly Dictionary<string, Seat> _seats;
    private bool _failNextSave;

    public FakeSeatRepository(params Seat[] seats)
    {
        _seats = seats.ToDictionary(seat => seat.Id);
    }

    public int SaveCount { get; private set; }

    /// <summary>Hace que el próximo <see cref="SaveAsync"/> se comporte como una carrera perdida.</summary>
    public void SimulateConcurrentModificationOnNextSave() => _failNextSave = true;

    public Task<IReadOnlyList<Seat>> GetAllAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Seat>>(_seats.Values.ToList());

    public Task<Seat?> GetByIdAsync(string seatId, CancellationToken cancellationToken) =>
        Task.FromResult(_seats.GetValueOrDefault(seatId));

    public Task<Result> SaveAsync(Seat seat, CancellationToken cancellationToken)
    {
        if (_failNextSave)
        {
            _failNextSave = false;
            return Task.FromResult<Result>(SeatErrors.ConcurrentModification(seat.Id));
        }

        SaveCount++;
        return Task.FromResult(Result.Success());
    }
}
