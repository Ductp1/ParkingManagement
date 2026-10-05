using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ParkingManagement.ServiceDefaults;
using ParkingManagement.ServiceDefaults.Persistence;
using VehicleService.Application.Interfaces;
using VehicleService.Infrastructure.Persistence;
using VehicleService.Infrastructure.Persistence.Queries;
using VehicleService.Infrastructure.Persistence.Seeding;

namespace VehicleService.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddVehicleInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddServiceDbContext<VehicleDbContext>(configuration);
        services.AddScoped<IVehicleQueries, VehicleQueries>();
        services.AddScoped<IDataSeeder<VehicleDbContext>, VehicleDataSeeder>();
        return services;
    }
}
