using Cinema.Application.Seats;
using Cinema.Domain.Seats;

namespace Cinema.Application.Tests.Seats;

public class GetSeatsHandlerTests : HandlerTestBase
{
    [Fact]
    public async Task Returns_all_seats_ordered_by_row_and_number()
    {
        var handler = new GetSeatsHandler(Repository, Time);

        var seats = await handler.HandleAsync(CancellationToken.None);

        Assert.Equal(["A1", "A2", "B1"], seats.Select(seat => seat.Id));
    }

    [Fact]
    public async Task Selected_seat_shows_its_expiry()
    {
        var hold = SelectA1();
        var handler = new GetSeatsHandler(Repository, Time);

        var seats = await handler.HandleAsync(CancellationToken.None);

        var a1 = seats.Single(seat => seat.Id == "A1");
        Assert.Equal(SeatStatus.Selected, a1.Status);
        Assert.Equal(hold.ExpiresAt, a1.ExpiresAt);
    }

    [Fact]
    public async Task Expired_selection_is_shown_as_free_without_expiry()
    {
        SelectA1();
        Time.Advance(Ttl);
        var handler = new GetSeatsHandler(Repository, Time);

        var seats = await handler.HandleAsync(CancellationToken.None);

        var a1 = seats.Single(seat => seat.Id == "A1");
        Assert.Equal(SeatStatus.Free, a1.Status);
        Assert.Null(a1.ExpiresAt);
    }
}
