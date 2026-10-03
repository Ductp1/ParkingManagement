using GateService.Application.Features;
using GateService.Infrastructure.Persistence;
using GateService.Infrastructure.Persistence.Queries;
using GateService.Infrastructure.Persistence.Seeding;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ParkingManagement.ServiceDefaults;
using ParkingManagement.ServiceDefaults.Persistence;

namespace GateService.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddGateInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddServiceDbContext<GateDbContext>(configuration);
        services.AddScoped<IParkingSessionQueries, ParkingSessionQueries>();
        services.AddScoped<IDataSeeder<GateDbContext>, GateDataSeeder>();
        return services;
    }
}
