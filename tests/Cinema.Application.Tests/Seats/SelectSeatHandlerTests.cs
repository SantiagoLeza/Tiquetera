using Cinema.Domain.Seats;

namespace Cinema.Application.Tests.Seats;

public class SelectSeatHandlerTests : HandlerTestBase
{
    [Fact]
    public async Task Selects_saves_and_publishes_event()
    {
        var handler = CreateSelectHandler();

        var result = await handler.HandleAsync("A1", CancellationToken.None);

        Assert.True(result.IsSuccess);
        var selection = result.Value;
        Assert.Equal("A1", selection.SeatId);
        Assert.NotEqual(Guid.Empty, selection.HoldToken);
        Assert.Equal(Now + Ttl, selection.ExpiresAt);
        Assert.Equal(300, selection.ExpiresInSeconds);

        Assert.Equal(1, Repository.SaveCount);
        var published = Assert.Single(Events.Published);
        Assert.Equal(new SeatSelected("A1", Now + Ttl, Now), published);
    }

    [Fact]
    public async Task Unknown_seat_returns_not_found()
    {
        var handler = CreateSelectHandler();

        var result = await handler.HandleAsync("Z99", CancellationToken.None);

        AssertFailure(result, "seat.not_found");
        Assert.Empty(Events.Published);
    }

    [Fact]
    public async Task Domain_rejection_is_returned_without_saving_or_publishing()
    {
        SelectA1();
        var handler = CreateSelectHandler();

        var result = await handler.HandleAsync("A1", CancellationToken.None);

        AssertFailure(result, "seat.already_selected");
        Assert.Equal(0, Repository.SaveCount);
        Assert.Empty(Events.Published);
    }

    [Fact]
    public async Task Lost_race_returns_conflict_and_publishes_nothing()
    {
        Repository.SimulateConcurrentModificationOnNextSave();
        var handler = CreateSelectHandler();

        var result = await handler.HandleAsync("A1", CancellationToken.None);

        AssertFailure(result, "seat.concurrent_modification");
        Assert.Empty(Events.Published);
    }
}
