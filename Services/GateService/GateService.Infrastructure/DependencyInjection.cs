using GateService.Application.Features;
using GateService.Application.Features.GateConsole;
using GateService.Application.Features.GateDevices;
using GateService.Application.Features.GateEvents;
using GateService.Application.Features.PlateRecognition;
using GateService.Infrastructure.Persistence;
using GateService.Infrastructure.Persistence.Queries;
using GateService.Infrastructure.Persistence.Repositories;
using GateService.Infrastructure.Persistence.Seeding;
using GateService.Infrastructure.Recognition;
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
        // T-701: MVP dùng ManualPlateRecognizer; Phase 2 chỉ cần đổi dòng này sang OcrPlateRecognizer.
        services.AddScoped<IPlateRecognizer, ManualPlateRecognizer>();
        // T-702: Gate Console + nhật ký GateEvents.
        services.AddScoped<IParkingSessionWriter, ParkingSessionWriter>();
        services.AddScoped<IGateEventWriter, GateEventWriter>();
        // T-703: GateDevices.
        services.AddScoped<IGateDeviceQueries, GateDeviceQueries>();
        services.AddScoped<IGateDeviceRepository, GateDeviceRepository>();
        return services;
    }
}

