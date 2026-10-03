using GateService.Application.Features;
using Microsoft.Extensions.DependencyInjection;

namespace GateService.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddGateApplication(this IServiceCollection services)
    {
        services.AddScoped<IListLotSessionsUseCase, ListLotSessionsUseCase>();
        services.AddScoped<ILookupVehicleAtGateUseCase, LookupVehicleAtGateUseCase>();
        return services;
    }
}
