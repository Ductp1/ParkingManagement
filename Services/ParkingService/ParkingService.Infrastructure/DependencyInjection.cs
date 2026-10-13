using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ParkingManagement.ServiceDefaults;
using ParkingManagement.ServiceDefaults.Persistence;
using ParkingService.Application.Abstractions;
using ParkingService.Infrastructure.Persistence;
using ParkingService.Infrastructure.Persistence.Repositories;
using ParkingService.Infrastructure.Persistence.Seeding;

namespace ParkingService.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddParkingInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddServiceDbContext<ParkingDbContext>(configuration);
        services.AddScoped<IParkingLotRepository, ParkingLotRepository>();
        services.AddScoped<IParkingLotManagementRepository, ParkingLotManagementRepository>();
        services.AddScoped<IZoneRepository, ZoneRepository>();
        services.AddScoped<IFloorRepository, FloorRepository>();
        services.AddScoped<ISlotRepository, SlotRepository>();
        services.AddScoped<IKybApplicationRepository, KybApplicationRepository>();
        services.AddScoped<IDataSeeder<ParkingDbContext>, ParkingDataSeeder>();
        return services;
    }
}
