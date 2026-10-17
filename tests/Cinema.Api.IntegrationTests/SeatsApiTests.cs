using System.Net;
using System.Net.Http.Json;
using Cinema.Api.IntegrationTests.Infrastructure;
using Cinema.Application.Seats;
using Cinema.Domain.Seats;

namespace Cinema.Api.IntegrationTests;

/// <summary>Comportamiento de la API de punta a punta: HTTP → handler → dominio → PostgreSQL.</summary>
public class SeatsApiTests(CinemaApiFactory factory) : ApiTestBase(factory)
{
    // ---------- GET /seats ----------

    [Fact]
    public async Task Get_seats_returns_the_seeded_auditorium_in_order()
    {
        var seats = await Client.GetFromJsonAsync<List<SeatDto>>("/seats", Json);

        Assert.NotNull(seats);
        Assert.Equal(80, seats.Count);
        Assert.All(seats, seat => Assert.Equal(SeatStatus.Free, seat.Status));
        Assert.Equal("A1", seats[0].Id);
        Assert.Equal("A2", seats[1].Id);
        Assert.Equal("H10", seats[^1].Id);
    }

    [Fact]
    public async Task Get_seats_never_exposes_the_hold_token()
    {
        var selection = await SelectSuccessfullyAsync("C7");

        var json = await Client.GetStringAsync("/seats");

        Assert.DoesNotContain(selection.HoldToken.ToString(), json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("holdToken", json, StringComparison.OrdinalIgnoreCase);
    }

    // ---------- Flujos felices ----------

    [Fact]
    public async Task Select_returns_201_with_hold_token_and_expiry()
    {
        var response = await SelectAsync("C7");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("/seats/C7/selection", response.Headers.Location?.ToString());

        var selection = await response.Content.ReadFromJsonAsync<SeatSelectionDto>(Json);
        Assert.NotNull(selection);
        Assert.Equal("C7", selection.SeatId);
        Assert.NotEqual(Guid.Empty, selection.HoldToken);
        Assert.Equal(Time.GetUtcNow() + Ttl, selection.ExpiresAt);
        Assert.Equal(300, selection.ExpiresInSeconds);

        var seat = await GetSeatAsync("C7");
        Assert.Equal(SeatStatus.Selected, seat.Status);
        Assert.Equal(selection.ExpiresAt, seat.ExpiresAt);
    }

    [Fact]
    public async Task Select_then_confirm_reserves_the_seat()
    {
        var selection = await SelectSuccessfullyAsync("C7");

        var response = await ConfirmAsync("C7", selection.HoldToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<SeatDto>(Json);
        Assert.Equal(SeatStatus.Reserved, body!.Status);
        Assert.Equal(SeatStatus.Reserved, (await GetSeatAsync("C7")).Status);
    }

    [Fact]
    public async Task Select_then_release_frees_the_seat()
    {
        var selection = await SelectSuccessfullyAsync("C7");

        var response = await ReleaseAsync("C7", selection.HoldToken);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(SeatStatus.Free, (await GetSeatAsync("C7")).Status);
    }

    // ---------- TTL (con el reloj falso de la API) ----------

    [Fact]
    public async Task Confirm_after_ttl_returns_410_and_seat_is_free_again()
    {
        var selection = await SelectSuccessfullyAsync("C7");
        Time.Advance(Ttl);

        var response = await ConfirmAsync("C7", selection.HoldToken);

        await AssertProblemAsync(response, 410, "seat.selection_expired");
        Assert.Equal(SeatStatus.Free, (await GetSeatAsync("C7")).Status);
    }

    [Fact]
    public async Task Confirm_just_before_ttl_succeeds()
    {
        var selection = await SelectSuccessfullyAsync("C7");
        Time.Advance(Ttl - TimeSpan.FromMilliseconds(1));

        var response = await ConfirmAsync("C7", selection.HoldToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Release_after_ttl_returns_410()
    {
        var selection = await SelectSuccessfullyAsync("C7");
        Time.Advance(Ttl);

        var response = await ReleaseAsync("C7", selection.HoldToken);

        await AssertProblemAsync(response, 410, "seat.selection_expired");
    }

    [Fact]
    public async Task Expired_selection_can_be_taken_by_someone_else_and_old_token_stops_working()
    {
        var first = await SelectSuccessfullyAsync("C7");
        Time.Advance(Ttl);

        var second = await SelectSuccessfullyAsync("C7");
        var confirmWithOldToken = await ConfirmAsync("C7", first.HoldToken);

        Assert.NotEqual(first.HoldToken, second.HoldToken);
        await AssertProblemAsync(confirmWithOldToken, 403, "seat.invalid_hold_token");
    }

    // ---------- Errores ----------

    [Fact]
    public async Task Selecting_an_already_selected_seat_returns_409()
    {
        await SelectSuccessfullyAsync("C7");

        var response = await SelectAsync("C7");

        await AssertProblemAsync(response, 409, "seat.already_selected");
    }

    [Fact]
    public async Task Confirm_with_wrong_token_returns_403()
    {
        await SelectSuccessfullyAsync("C7");

        var response = await ConfirmAsync("C7", Guid.NewGuid());

        await AssertProblemAsync(response, 403, "seat.invalid_hold_token");
    }

    [Fact]
    public async Task Release_reserved_seat_returns_409()
    {
        var selection = await SelectSuccessfullyAsync("C7");
        await ConfirmAsync("C7", selection.HoldToken);

        var response = await ReleaseAsync("C7", selection.HoldToken);

        await AssertProblemAsync(response, 409, "seat.already_reserved");
    }

    [Fact]
    public async Task Unknown_seat_returns_404_on_every_command()
    {
        await AssertProblemAsync(await SelectAsync("Z99"), 404, "seat.not_found");
        await AssertProblemAsync(await ConfirmAsync("Z99", Guid.NewGuid()), 404, "seat.not_found");
        await AssertProblemAsync(await ReleaseAsync("Z99", Guid.NewGuid()), 404, "seat.not_found");
    }

    // Regresión: en Development un error de binding del header terminaba en 500.
    [Theory]
    [InlineData("PUT", "/seats/C7/reservation", null)]
    [InlineData("PUT", "/seats/C7/reservation", "no-es-un-guid")]
    [InlineData("DELETE", "/seats/C7/selection", null)]
    [InlineData("DELETE", "/seats/C7/selection", "no-es-un-guid")]
    public async Task Missing_or_malformed_hold_token_returns_400(string method, string url, string? holdToken)
    {
        await SelectSuccessfullyAsync("C7");

        var response = await Client.SendAsync(WithHoldToken(new HttpMethod(method), url, holdToken));

        await AssertProblemAsync(response, 400);
        Assert.Equal(SeatStatus.Selected, (await GetSeatAsync("C7")).Status);
    }
}
