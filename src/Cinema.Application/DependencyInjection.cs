using Cinema.Application.Seats;
using Microsoft.Extensions.DependencyInjection;

namespace Cinema.Application;

public static class DependencyInjection
{
    /// <summary>
    /// Registra los handlers de los casos de uso. Son scoped porque dependen del repositorio,
    /// que vive lo mismo que un request.
    /// La configuración (<see cref="SeatSelectionOptions"/>) y <see cref="TimeProvider"/> los registra la API.
    /// </summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<GetSeatsHandler>();
        services.AddScoped<SelectSeatHandler>();
        services.AddScoped<ConfirmReservationHandler>();
        services.AddScoped<ReleaseSeatHandler>();
        return services;
    }
}
