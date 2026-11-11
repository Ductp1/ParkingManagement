using GateService.Application.Features;
using GateService.Application.Features.GateConsole;
using GateService.Application.Features.GateDevices;
using GateService.Application.Features.GateEvents;
using GateService.Application.Features.GateExceptions;
using GateService.Application.Features.PlateRecognition;
using GateService.Application.Features.PmsIntegration;
using GateService.Application.Features.StaffCancellations;
using GateService.Infrastructure.Integrations;
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
        // US-039: tham số GateConsole (ân hạn 30', ngưỡng hủy bất thường) + ca trực.
        services.AddSingleton(configuration.GetSection("GateConsole").Get<GateConsoleOptions>() ?? GateConsoleOptions.Default);
        services.AddScoped<IShiftStore, ShiftStore>();
        // US-039/US-049/US-052: gọi BookingService & ParkingService qua Gateway.
        var gateway = configuration["Services:GatewayBaseUrl"] ?? "http://localhost:5000";
        services.AddHttpClient<IGateBookingClient, BookingServiceClient>(client =>
        {
            client.BaseAddress = new Uri(gateway.TrimEnd('/') + "/");
            client.Timeout = TimeSpan.FromSeconds(10);
        });
        services.AddHttpClient<IParkingSlotClient, ParkingServiceClient>(client =>
        {
            client.BaseAddress = new Uri(gateway.TrimEnd('/') + "/");
            client.Timeout = TimeSpan.FromSeconds(10);
        });
        // US-048: đọc audit GateEvents.
        services.AddScoped<IGateEventQueries, GateEventQueries>();
        // US-052: sự kiện PMS – auth qua ParkingService, inbox chống trùng, hàng chờ xử lý tay.
        services.AddHttpClient<IPmsAuthClient, PmsAuthClient>(client =>
        {
            client.BaseAddress = new Uri(gateway.TrimEnd('/') + "/");
            client.Timeout = TimeSpan.FromSeconds(10);
        });
        services.AddScoped<IPmsInbox, PmsInbox>();
        services.AddScoped<IPmsQuarantineStore, PmsQuarantineStore>();
        return services;
    }
}

