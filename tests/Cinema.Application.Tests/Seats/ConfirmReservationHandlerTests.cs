using Cinema.Application.Seats;
using Cinema.Domain.Seats;

namespace Cinema.Application.Tests.Seats;

public class ConfirmReservationHandlerTests : HandlerTestBase
{
    private ConfirmReservationHandler CreateHandler() => new(Repository, Events, Time);

    [Fact]
    public async Task Confirms_saves_and_publishes_event()
    {
        var hold = SelectA1();

        var result = await CreateHandler().HandleAsync("A1", hold.Token, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(SeatStatus.Reserved, result.Value.Status);
        Assert.Null(result.Value.ExpiresAt);
        Assert.Equal(1, Repository.SaveCount);
        Assert.Equal(new SeatReserved("A1", Now), Assert.Single(Events.Published));
    }

    [Fact]
    public async Task Unknown_seat_returns_not_found()
    {
        var result = await CreateHandler().HandleAsync("Z99", Guid.NewGuid(), CancellationToken.None);

        AssertFailure(result, "seat.not_found");
    }

    [Fact]
    public async Task Wrong_token_returns_forbidden_without_saving_or_publishing()
    {
        SelectA1();

        var result = await CreateHandler().HandleAsync("A1", Guid.NewGuid(), CancellationToken.None);

        AssertFailure(result, "seat.invalid_hold_token");
        Assert.Equal(0, Repository.SaveCount);
        Assert.Empty(Events.Published);
    }

    [Fact]
    public async Task Expired_selection_returns_gone()
    {
        var hold = SelectA1();
        Time.Advance(Ttl);

        var result = await CreateHandler().HandleAsync("A1", hold.Token, CancellationToken.None);

        AssertFailure(result, "seat.selection_expired");
        Assert.Empty(Events.Published);
    }

    [Fact]
    public async Task Lost_race_returns_conflict_and_publishes_nothing()
    {
        var hold = SelectA1();
        Repository.SimulateConcurrentModificationOnNextSave();

        var result = await CreateHandler().HandleAsync("A1", hold.Token, CancellationToken.None);

        AssertFailure(result, "seat.concurrent_modification");
        Assert.Empty(Events.Published);
    }
}
