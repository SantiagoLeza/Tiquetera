using Cinema.Domain.Common;
using Cinema.Domain.Seats;
using Microsoft.Extensions.Time.Testing;

namespace Cinema.Domain.Tests.Seats;

public class SeatTests
{
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan OneTick = TimeSpan.FromTicks(1);

    // xUnit crea una instancia nueva de la clase por cada test, así que cada test tiene su propio reloj.
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 16, 20, 0, 0, TimeSpan.Zero));

    private DateTimeOffset Now => _time.GetUtcNow();

    // ---------- Creación ----------

    [Fact]
    public void New_seat_is_free_with_natural_id()
    {
        var seat = new Seat("C", 7);

        Assert.Equal("C7", seat.Id);
        Assert.Equal("C", seat.Row);
        Assert.Equal(7, seat.Number);
        Assert.Equal(SeatStatus.Free, seat.GetStatus(Now));
        Assert.Null(seat.ExpiresAt);
    }

    [Theory]
    [InlineData("", 1)]
    [InlineData("AB", 1)]
    [InlineData("c", 1)]
    [InlineData("1", 1)]
    [InlineData("A", 0)]
    [InlineData("A", -3)]
    public void Constructor_rejects_invalid_coordinates(string row, int number)
    {
        Assert.ThrowsAny<ArgumentException>(() => new Seat(row, number));
    }

    // ---------- Select ----------

    [Fact]
    public void Select_free_seat_returns_hold_and_marks_it_selected()
    {
        var seat = new Seat("A", 1);

        var result = seat.Select(Now, Ttl);

        Assert.True(result.IsSuccess);
        Assert.NotEqual(Guid.Empty, result.Value.Token);
        Assert.Equal(Now + Ttl, result.Value.ExpiresAt);
        Assert.Equal(Now + Ttl, seat.ExpiresAt);
        Assert.Equal(SeatStatus.Selected, seat.GetStatus(Now));
    }

    [Fact]
    public void Select_already_selected_seat_fails_with_conflict()
    {
        var seat = new Seat("A", 1);
        seat.Select(Now, Ttl);

        var result = seat.Select(Now, Ttl);

        AssertFailure(result, "seat.already_selected", ErrorType.Conflict);
    }

    [Fact]
    public void Select_reserved_seat_fails_with_conflict()
    {
        var seat = ReservedSeat();

        var result = seat.Select(Now, Ttl);

        AssertFailure(result, "seat.already_reserved", ErrorType.Conflict);
    }

    [Fact]
    public void Select_expired_selection_issues_new_token_and_invalidates_the_old_one()
    {
        var seat = new Seat("A", 1);
        var oldHold = seat.Select(Now, Ttl).Value;
        _time.Advance(Ttl);

        var newHold = seat.Select(Now, Ttl);

        Assert.True(newHold.IsSuccess);
        Assert.NotEqual(oldHold.Token, newHold.Value.Token);
        Assert.Equal(Now + Ttl, newHold.Value.ExpiresAt);
        AssertFailure(seat.Confirm(oldHold.Token, Now), "seat.invalid_hold_token", ErrorType.Forbidden);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Select_with_non_positive_ttl_is_a_programming_error(int seconds)
    {
        var seat = new Seat("A", 1);

        Assert.Throws<ArgumentOutOfRangeException>(() => seat.Select(Now, TimeSpan.FromSeconds(seconds)));
    }

    // ---------- Expiración perezosa ----------

    [Fact]
    public void Selection_is_still_active_one_tick_before_expiry()
    {
        var seat = new Seat("A", 1);
        seat.Select(Now, Ttl);

        _time.Advance(Ttl - OneTick);

        Assert.Equal(SeatStatus.Selected, seat.GetStatus(Now));
    }

    [Fact]
    public void Selection_counts_as_free_exactly_at_expiry()
    {
        var seat = new Seat("A", 1);
        seat.Select(Now, Ttl);

        _time.Advance(Ttl);

        Assert.Equal(SeatStatus.Free, seat.GetStatus(Now));
    }

    // ---------- Confirm ----------

    [Fact]
    public void Confirm_with_valid_token_reserves_the_seat_and_clears_the_hold()
    {
        var seat = new Seat("A", 1);
        var hold = seat.Select(Now, Ttl).Value;

        var result = seat.Confirm(hold.Token, Now);

        Assert.True(result.IsSuccess);
        Assert.Equal(SeatStatus.Reserved, seat.GetStatus(Now));
        Assert.Null(seat.ExpiresAt);
    }

    [Fact]
    public void Confirm_one_tick_before_expiry_succeeds()
    {
        var seat = new Seat("A", 1);
        var hold = seat.Select(Now, Ttl).Value;
        _time.Advance(Ttl - OneTick);

        var result = seat.Confirm(hold.Token, Now);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void Reserved_seat_never_expires()
    {
        var seat = ReservedSeat();

        _time.Advance(TimeSpan.FromDays(1));

        Assert.Equal(SeatStatus.Reserved, seat.GetStatus(Now));
    }

    [Fact]
    public void Confirm_exactly_at_expiry_fails_with_gone()
    {
        var seat = new Seat("A", 1);
        var hold = seat.Select(Now, Ttl).Value;
        _time.Advance(Ttl);

        var result = seat.Confirm(hold.Token, Now);

        AssertFailure(result, "seat.selection_expired", ErrorType.Gone);
    }

    [Fact]
    public void Confirm_with_wrong_token_fails_with_forbidden_and_keeps_the_selection()
    {
        var seat = new Seat("A", 1);
        var hold = seat.Select(Now, Ttl).Value;

        var result = seat.Confirm(Guid.NewGuid(), Now);

        AssertFailure(result, "seat.invalid_hold_token", ErrorType.Forbidden);
        Assert.Equal(SeatStatus.Selected, seat.GetStatus(Now));
        Assert.True(seat.Confirm(hold.Token, Now).IsSuccess);
    }

    [Fact]
    public void Confirm_free_seat_fails_with_conflict()
    {
        var seat = new Seat("A", 1);

        var result = seat.Confirm(Guid.NewGuid(), Now);

        AssertFailure(result, "seat.not_selected", ErrorType.Conflict);
    }

    [Fact]
    public void Confirm_reserved_seat_fails_with_conflict()
    {
        var seat = ReservedSeat();

        var result = seat.Confirm(Guid.NewGuid(), Now);

        AssertFailure(result, "seat.already_reserved", ErrorType.Conflict);
    }

    // ---------- Release ----------

    [Fact]
    public void Release_with_valid_token_frees_the_seat_and_allows_selecting_it_again()
    {
        var seat = new Seat("A", 1);
        var hold = seat.Select(Now, Ttl).Value;

        var result = seat.Release(hold.Token, Now);

        Assert.True(result.IsSuccess);
        Assert.Equal(SeatStatus.Free, seat.GetStatus(Now));
        Assert.Null(seat.ExpiresAt);
        Assert.True(seat.Select(Now, Ttl).IsSuccess);
    }

    [Fact]
    public void Released_token_can_no_longer_confirm()
    {
        var seat = new Seat("A", 1);
        var hold = seat.Select(Now, Ttl).Value;
        seat.Release(hold.Token, Now);

        var result = seat.Confirm(hold.Token, Now);

        AssertFailure(result, "seat.not_selected", ErrorType.Conflict);
    }

    [Fact]
    public void Release_with_wrong_token_fails_with_forbidden()
    {
        var seat = new Seat("A", 1);
        seat.Select(Now, Ttl);

        var result = seat.Release(Guid.NewGuid(), Now);

        AssertFailure(result, "seat.invalid_hold_token", ErrorType.Forbidden);
        Assert.Equal(SeatStatus.Selected, seat.GetStatus(Now));
    }

    [Fact]
    public void Release_expired_selection_fails_with_gone()
    {
        var seat = new Seat("A", 1);
        var hold = seat.Select(Now, Ttl).Value;
        _time.Advance(Ttl);

        var result = seat.Release(hold.Token, Now);

        AssertFailure(result, "seat.selection_expired", ErrorType.Gone);
    }

    [Fact]
    public void Release_free_seat_fails_with_conflict()
    {
        var seat = new Seat("A", 1);

        var result = seat.Release(Guid.NewGuid(), Now);

        AssertFailure(result, "seat.not_selected", ErrorType.Conflict);
    }

    [Fact]
    public void Release_reserved_seat_fails_with_conflict()
    {
        var seat = ReservedSeat();

        var result = seat.Release(Guid.NewGuid(), Now);

        AssertFailure(result, "seat.already_reserved", ErrorType.Conflict);
    }

    // ---------- Helpers ----------

    private Seat ReservedSeat()
    {
        var seat = new Seat("A", 1);
        var hold = seat.Select(Now, Ttl).Value;
        seat.Confirm(hold.Token, Now);
        return seat;
    }

    private static void AssertFailure(Result result, string expectedCode, ErrorType expectedType)
    {
        Assert.True(result.IsFailure);
        Assert.Equal(expectedCode, result.Error.Code);
        Assert.Equal(expectedType, result.Error.Type);
    }
}
