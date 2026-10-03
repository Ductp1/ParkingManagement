using Microsoft.Extensions.DependencyInjection;
using VehicleService.Application.Features.Vehicles;

namespace VehicleService.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddVehicleApplication(this IServiceCollection services)
    {
        services.AddScoped<IGetVehiclesByUserUseCase, GetVehiclesByUserUseCase>();
        services.AddScoped<IFindVehicleByPlateUseCase, FindVehicleByPlateUseCase>();
        return services;
    }
}
