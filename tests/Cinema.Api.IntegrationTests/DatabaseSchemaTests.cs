using Cinema.Api.IntegrationTests.Infrastructure;
using Cinema.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Cinema.Api.IntegrationTests;

/// <summary>
/// Las restricciones de db/init.sql: segunda línea de defensa de las invariantes de Seat,
/// por si algo escribe en la base sin pasar por el dominio.
/// </summary>
public class DatabaseSchemaTests(CinemaApiFactory factory) : ApiTestBase(factory)
{
    [Theory]
    [InlineData("UPDATE seats SET status = 'Selected' WHERE id = 'A1'", "ck_seats_hold")]
    [InlineData("UPDATE seats SET hold_token = gen_random_uuid() WHERE id = 'A1'", "ck_seats_hold")]
    [InlineData("UPDATE seats SET status = 'Cancelled' WHERE id = 'A1'", "ck_seats_status")]
    [InlineData("INSERT INTO seats (id, \"row\", number, status) VALUES ('A11', 'B', 11, 'Free')", "ck_seats_id")]
    public async Task Database_rejects_states_that_break_seat_invariants(string sql, string expectedConstraint)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CinemaDbContext>();

        var exception = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlRawAsync(sql));

        Assert.Equal(PostgresErrorCodes.CheckViolation, exception.SqlState);
        Assert.Equal(expectedConstraint, exception.ConstraintName);
    }
}
