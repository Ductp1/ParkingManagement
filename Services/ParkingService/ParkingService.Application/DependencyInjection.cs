using Microsoft.Extensions.DependencyInjection;
using ParkingService.Application.Features.ParkingLots;

namespace ParkingService.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddParkingApplication(this IServiceCollection services)
    {
        services.AddScoped<IGetParkingLotByIdUseCase, GetParkingLotByIdUseCase>();
        services.AddScoped<ISearchParkingLotsUseCase, SearchParkingLotsUseCase>();
        return services;
    }
}
