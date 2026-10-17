using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Cinema.Application.Seats;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Time.Testing;

namespace Cinema.Api.IntegrationTests.Infrastructure;

/// <summary>
/// Base de los tests de integración. Los tests de una misma colección corren en serie, así que
/// resetear los asientos al inicio de cada test es seguro.
/// </summary>
[Collection(ApiCollection.Name)]
public abstract class ApiTestBase(CinemaApiFactory factory) : IAsyncLifetime
{
    protected static readonly TimeSpan Ttl = TimeSpan.FromMinutes(5);

    protected static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    protected CinemaApiFactory Factory { get; } = factory;

    protected HttpClient Client { get; } = factory.CreateClient();

    protected FakeTimeProvider Time => Factory.Time;

    public Task InitializeAsync() => Factory.ResetSeatsAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    protected Task<HttpResponseMessage> SelectAsync(string seatId) =>
        Client.PostAsync($"/seats/{seatId}/selection", content: null);

    protected Task<HttpResponseMessage> ConfirmAsync(string seatId, Guid holdToken) =>
        Client.SendAsync(WithHoldToken(HttpMethod.Put, $"/seats/{seatId}/reservation", holdToken.ToString()));

    protected Task<HttpResponseMessage> ReleaseAsync(string seatId, Guid holdToken) =>
        Client.SendAsync(WithHoldToken(HttpMethod.Delete, $"/seats/{seatId}/selection", holdToken.ToString()));

    protected async Task<SeatSelectionDto> SelectSuccessfullyAsync(string seatId)
    {
        var response = await SelectAsync(seatId);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<SeatSelectionDto>(Json))!;
    }

    protected async Task<SeatDto> GetSeatAsync(string seatId)
    {
        var seats = await Client.GetFromJsonAsync<List<SeatDto>>("/seats", Json);
        return seats!.Single(seat => seat.Id == seatId);
    }

    protected static HttpRequestMessage WithHoldToken(HttpMethod method, string url, string? holdToken)
    {
        var request = new HttpRequestMessage(method, url);
        if (holdToken is not null)
        {
            request.Headers.Add("X-Hold-Token", holdToken);
        }

        return request;
    }

    /// <summary>Verifica que la respuesta sea un ProblemDetails con el status y el código de negocio esperados.</summary>
    protected static async Task AssertProblemAsync(HttpResponseMessage response, int expectedStatus, string? expectedCode = null)
    {
        Assert.Equal(expectedStatus, (int)response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(Json);
        Assert.NotNull(problem);
        Assert.Equal(expectedStatus, problem.Status);

        if (expectedCode is not null)
        {
            Assert.True(problem.Extensions.TryGetValue("code", out var code), "Falta la extensión 'code'.");
            Assert.Equal(expectedCode, code?.ToString());
        }
    }
}
