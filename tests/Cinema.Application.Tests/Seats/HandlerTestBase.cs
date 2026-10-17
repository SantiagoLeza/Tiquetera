using Cinema.Application.Seats;
using Cinema.Application.Tests.Fakes;
using Cinema.Domain.Common;
using Cinema.Domain.Seats;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace Cinema.Application.Tests.Seats;

/// <summary>Dobles y helpers compartidos. xUnit crea una instancia por test, así que cada test arranca limpio.</summary>
public abstract class HandlerTestBase
{
    protected static readonly TimeSpan Ttl = TimeSpan.FromMinutes(5);

    protected readonly FakeTimeProvider Time = new(new DateTimeOffset(2026, 10, 16, 20, 0, 0, TimeSpan.Zero));
    protected readonly RecordingSeatEventPublisher Events = new();
    protected readonly Seat SeatA1 = new("A", 1);
    protected readonly FakeSeatRepository Repository;

    protected HandlerTestBase()
    {
        Repository = new FakeSeatRepository(SeatA1, new Seat("A", 2), new Seat("B", 1));
    }

    protected DateTimeOffset Now => Time.GetUtcNow();

    protected SelectSeatHandler CreateSelectHandler() =>
        new(Repository, Events, Time, Options.Create(new SeatSelectionOptions { Ttl = Ttl }));

    /// <summary>Selecciona A1 directamente sobre el dominio, sin pasar por el handler ni registrar eventos.</summary>
    protected SeatHold SelectA1() => SeatA1.Select(Now, Ttl).Value;

    protected static void AssertFailure(Result result, string expectedCode)
    {
        Assert.True(result.IsFailure);
        Assert.Equal(expectedCode, result.Error.Code);
    }
}
