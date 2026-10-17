using Cinema.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;
using Testcontainers.PostgreSql;

namespace Cinema.Api.IntegrationTests.Infrastructure;

/// <summary>
/// Levanta la API completa en memoria (<see cref="WebApplicationFactory{TEntryPoint}"/>) contra un PostgreSQL real
/// en un contenedor de Docker (Testcontainers). Se comparte entre todos los tests de la colección "Api":
/// un solo contenedor y una sola API para toda la corrida.
/// </summary>
public sealed class CinemaApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    // Misma imagen y mismo script de esquema que docker-compose.yml.
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine")
        .WithResourceMapping(new FileInfo(Path.Combine(AppContext.BaseDirectory, "db", "init.sql")), "/docker-entrypoint-initdb.d/")
        .Build();

    /// <summary>Reloj controlado por los tests: reemplaza a <see cref="TimeProvider.System"/> en la API.</summary>
    public FakeTimeProvider Time { get; } = new(new DateTimeOffset(2026, 10, 16, 20, 0, 0, TimeSpan.Zero));

    public async Task InitializeAsync() => await _postgres.StartAsync();

    async Task IAsyncLifetime.DisposeAsync()
    {
        await base.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Pisa la cadena de conexión de appsettings.Development.json con la del contenedor,
        // que ya tiene el esquema y los 80 asientos de db/init.sql.
        builder.UseSetting("ConnectionStrings:Cinema", _postgres.GetConnectionString());

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(Time);
        });
    }

    /// <summary>Deja los 80 asientos libres. Se llama antes de cada test para que no dependan entre sí.</summary>
    public async Task ResetSeatsAsync()
    {
        await using var scope = Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CinemaDbContext>();
        await db.Database.ExecuteSqlRawAsync("UPDATE seats SET status = 'Free', hold_token = NULL, expires_at = NULL");
    }
}

[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<CinemaApiFactory>
{
    public const string Name = "Api";
}
