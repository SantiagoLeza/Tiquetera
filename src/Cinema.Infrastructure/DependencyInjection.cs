using Cinema.Application.Abstractions;
using Cinema.Infrastructure.Events;
using Cinema.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Cinema.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string connectionString)
    {
        // AddDbContext registra el DbContext como scoped: una instancia (y un change tracker) por request.
        services.AddDbContext<CinemaDbContext>(options => options.UseNpgsql(connectionString));

        services.AddScoped<ISeatRepository, SeatRepository>();

        // Singleton: no tiene estado propio y ILogger es thread-safe.
        services.AddSingleton<ISeatEventPublisher, LoggingSeatEventPublisher>();

        return services;
    }

    /// <summary>
    /// Aplica las migraciones pendientes. Se invoca al arrancar la API para que levantarla localmente
    /// sea solo <c>docker compose up</c> + <c>dotnet run</c>.
    /// </summary>
    /// <remarks>
    /// Con varias instancias arrancando a la vez esto no es lo adecuado: en un despliegue real se aplicarían
    /// las migraciones como un paso previo (script SQL o migration bundle de EF).
    /// </remarks>
    public static async Task MigrateDatabaseAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CinemaDbContext>();
        await db.Database.MigrateAsync(cancellationToken);
    }
}
