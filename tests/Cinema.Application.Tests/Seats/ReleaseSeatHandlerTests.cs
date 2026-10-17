using Cinema.Application.Seats;
using Cinema.Domain.Seats;

namespace Cinema.Application.Tests.Seats;

public class ReleaseSeatHandlerTests : HandlerTestBase
{
    private ReleaseSeatHandler CreateHandler() => new(Repository, Events, Time);

    [Fact]
    public async Task Releases_saves_and_publishes_event()
    {
        var hold = SelectA1();

        var result = await CreateHandler().HandleAsync("A1", hold.Token, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(SeatStatus.Free, SeatA1.GetStatus(Now));
        Assert.Equal(1, Repository.SaveCount);
        Assert.Equal(new SeatReleased("A1", Now), Assert.Single(Events.Published));
    }

    [Fact]
    public async Task Unknown_seat_returns_not_found()
    {
        var result = await CreateHandler().HandleAsync("Z99", Guid.NewGuid(), CancellationToken.None);

        AssertFailure(result, "seat.not_found");
    }

    [Fact]
    public async Task Free_seat_returns_conflict_without_saving()
    {
        var result = await CreateHandler().HandleAsync("A1", Guid.NewGuid(), CancellationToken.None);

        AssertFailure(result, "seat.not_selected");
        Assert.Equal(0, Repository.SaveCount);
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
