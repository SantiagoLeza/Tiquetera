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
}
