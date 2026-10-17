using System.Net;
using System.Net.Http.Json;
using Cinema.Api.IntegrationTests.Infrastructure;
using Cinema.Application.Abstractions;
using Cinema.Domain.Seats;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Abstractions;

namespace Cinema.Api.IntegrationTests;

/// <summary>
/// Concurrencia optimista contra PostgreSQL real (ADR-002, ADR-011). Con el proveedor en memoria de EF
/// o con SQLite estos tests no probarían nada: no existe xmin.
/// </summary>
public class ConcurrencyTests(CinemaApiFactory factory, ITestOutputHelper output) : ApiTestBase(factory)
{
    private const int ParallelRequests = 20;

    /// <summary>
    /// Prueba determinística del token de concurrencia, sin depender de que los requests se pisen en el tiempo:
    /// dos operaciones leen la misma versión del asiento y ambas intentan guardar. La segunda debe perder.
    /// </summary>
    [Fact]
    public async Task Second_writer_with_a_stale_version_loses()
    {
        await using var scopeA = Factory.Services.CreateAsyncScope();
        await using var scopeB = Factory.Services.CreateAsyncScope();
        var repositoryA = scopeA.ServiceProvider.GetRequiredService<ISeatRepository>();
        var repositoryB = scopeB.ServiceProvider.GetRequiredService<ISeatRepository>();
        var now = Time.GetUtcNow();

        // Ambos leen el asiento libre (la misma versión xmin).
        var seatA = (await repositoryA.GetByIdAsync("D4", CancellationToken.None))!;
        var seatB = (await repositoryB.GetByIdAsync("D4", CancellationToken.None))!;

        // Para el dominio, ambas selecciones son válidas: cada uno vio el asiento libre.
        Assert.True(seatA.Select(now, Ttl).IsSuccess);
        Assert.True(seatB.Select(now, Ttl).IsSuccess);

        var savedA = await repositoryA.SaveAsync(seatA, CancellationToken.None);
        var savedB = await repositoryB.SaveAsync(seatB, CancellationToken.None);

        Assert.True(savedA.IsSuccess);
        Assert.True(savedB.IsFailure);
        Assert.Equal("seat.concurrent_modification", savedB.Error.Code);
    }

    [Fact]
    public async Task Parallel_selections_of_a_free_seat_have_exactly_one_winner()
    {
        var responses = await SendInParallelAsync(() => SelectAsync("E5"));

        await AssertExactlyOneWinnerAsync(responses, HttpStatusCode.Created);
        Assert.Equal(SeatStatus.Selected, (await GetSeatAsync("E5")).Status);
    }

    [Fact]
    public async Task Parallel_selections_of_an_expired_seat_have_exactly_one_winner()
    {
        await SelectSuccessfullyAsync("E5");
        Time.Advance(Ttl);

        var responses = await SendInParallelAsync(() => SelectAsync("E5"));

        await AssertExactlyOneWinnerAsync(responses, HttpStatusCode.Created);
    }

    [Fact]
    public async Task Parallel_confirmations_with_the_same_token_reserve_only_once()
    {
        var selection = await SelectSuccessfullyAsync("E5");

        var responses = await SendInParallelAsync(() => ConfirmAsync("E5", selection.HoldToken));

        await AssertExactlyOneWinnerAsync(responses, HttpStatusCode.OK);
        Assert.Equal(SeatStatus.Reserved, (await GetSeatAsync("E5")).Status);
    }

    [Fact]
    public async Task Parallel_operations_on_different_seats_do_not_conflict()
    {
        var seatIds = Enumerable.Range(1, 10).Select(number => $"F{number}").ToList();

        var responses = await Task.WhenAll(seatIds.Select(SelectAsync));

        Assert.All(responses, response => Assert.Equal(HttpStatusCode.Created, response.StatusCode));
    }

    /// <summary>
    /// Lanza todos los requests a la vez. Una barrera (<see cref="TaskCompletionSource"/>) los retiene
    /// hasta que todos están listos, para maximizar la superposición.
    /// </summary>
    private static async Task<HttpResponseMessage[]> SendInParallelAsync(Func<Task<HttpResponseMessage>> send)
    {
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var requests = Enumerable.Range(0, ParallelRequests)
            .Select(async _ =>
            {
                await start.Task;
                return await send();
            })
            .ToList();

        start.SetResult();
        return await Task.WhenAll(requests);
    }

    private async Task AssertExactlyOneWinnerAsync(HttpResponseMessage[] responses, HttpStatusCode successStatus)
    {
        Assert.Single(responses, response => response.StatusCode == successStatus);

        // Todos los demás pierden con 409: o el estado ya cambió cuando leyeron el asiento (ej. already_selected),
        // o leyeron la versión vieja y el UPDATE condicionado por xmin no afectó filas (concurrent_modification).
        var losers = responses.Where(response => response.StatusCode != successStatus).ToList();
        Assert.All(losers, response => Assert.Equal(HttpStatusCode.Conflict, response.StatusCode));

        var loserCodes = new List<string?>();
        foreach (var loser in losers)
        {
            var problem = await loser.Content.ReadFromJsonAsync<ProblemDetails>(Json);
            Assert.NotNull(problem);
            loserCodes.Add(problem.Extensions["code"]?.ToString());
        }

        Assert.All(loserCodes, code => Assert.Contains(
            code,
            new[] { "seat.already_selected", "seat.already_reserved", "seat.concurrent_modification" }));

        // Informativo (visible con --logger "console;verbosity=detailed"): cuántos perdieron por cada motivo.
        foreach (var group in loserCodes.GroupBy(code => code))
        {
            output.WriteLine($"{group.Key}: {group.Count()}");
        }
    }
}
