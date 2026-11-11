using GateService.Application.Features;
using GateService.Application.Features.GateConsole;
using GateService.Application.Features.GateDevices;
using GateService.Application.Features.GateEvents;
using GateService.Application.Features.LotCapacity;
using GateService.Application.Features.PlateRecognition;
using GateService.Infrastructure.HttpClients;
using ParkingManagement.ServiceDefaults.Messaging;
using ParkingManagement.ServiceDefaults.Persistence;
using GateService.Infrastructure.Integrations;
using GateService.Infrastructure.Persistence;
using GateService.Infrastructure.Persistence.Queries;
using GateService.Infrastructure.Persistence.Repositories;
using GateService.Infrastructure.Persistence.Seeding;
using ParkingManagement.ServiceDefaults.Messaging;
using ParkingManagement.ServiceDefaults.Persistence;
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
        services.AddScoped<GateService.Application.Features.AdminDashboard.IAdminGateDashboardQueries, AdminGateDashboardQueries>(); // US-081
        services.AddScoped<IDataSeeder<GateDbContext>, GateDataSeeder>();
        // T-701: MVP dùng ManualPlateRecognizer; Phase 2 chỉ cần đổi dòng này sang OcrPlateRecognizer.
        services.AddScoped<IPlateRecognizer, ManualPlateRecognizer>();
        // T-702: Gate Console + nhật ký GateEvents.
        services.AddScoped<IParkingSessionWriter, ParkingSessionWriter>();
        services.AddScoped<IGateEventWriter, GateEventWriter>();
        // T-703: GateDevices.
        services.AddScoped<IGateDeviceQueries, GateDeviceQueries>();
        services.AddScoped<IGateDeviceRepository, GateDeviceRepository>();
        services.AddScoped<IGetGateDevicesUseCase, GetGateDevicesUseCase>();
        services.AddScoped<IGetGateDeviceByIdUseCase, GetGateDeviceByIdUseCase>();
        services.AddScoped<ICreateGateDeviceUseCase, CreateGateDeviceUseCase>();
        services.AddScoped<IUpdateGateDeviceUseCase, UpdateGateDeviceUseCase>();
        services.AddScoped<IDeleteGateDeviceUseCase, DeleteGateDeviceUseCase>();
        services.AddScoped<IRecordGateDeviceHeartbeatUseCase, RecordGateDeviceHeartbeatUseCase>();
        // UC-20: đọc ca trực để xác định khung giờ "trong ca".
        services.AddScoped<IShiftQueries, ShiftQueries>();
        // UC-20/UC-21/UC-22: Gate Console gọi BookingService (TV3) và PaymentService (TV4) qua HTTP.
        // Cấu hình BaseUrl trong appsettings ("Services:BookingService:BaseUrl", "Services:PaymentService:BaseUrl").
        services.AddHttpClient<IGateBookingClient, GateBookingClient>((sp, client) =>
        {
            client.BaseAddress = new Uri(sp.GetRequiredService<IConfiguration>()["Services:BookingService:BaseUrl"] ?? "http://localhost:5104");
            client.Timeout = TimeSpan.FromSeconds(5);
        });
        services.AddHttpClient<IExitFeeCalculator, PaymentServiceClient>((sp, client) =>
        {
            client.BaseAddress = new Uri(sp.GetRequiredService<IConfiguration>()["Services:PaymentService:BaseUrl"] ?? "http://localhost:5105");
            client.Timeout = TimeSpan.FromSeconds(5);
        });
        services.AddHttpClient<IGateSettlementRecorder, PaymentServiceClient>((sp, client) =>
        {
            client.BaseAddress = new Uri(sp.GetRequiredService<IConfiguration>()["Services:PaymentService:BaseUrl"] ?? "http://localhost:5105");
            client.Timeout = TimeSpan.FromSeconds(5);
        });
        // US-044/US-046: ân hạn check-in và ân hạn miễn phí (mặc định 15' – khớp SystemConfigs của AdminDb).
        services.AddSingleton(new GateConsoleOptions
        {
            CheckInGracePeriodMinutes = ParsePositiveInt(configuration["GateConsole:CheckInGracePeriodMinutes"], 15),
            BillingGracePeriodMinutes = ParsePositiveInt(configuration["GateConsole:BillingGracePeriodMinutes"], 15),
        });
        // US-066: store + cổng vào/ra nguyên tử + publisher/counter HTTP (tự nuốt lỗi).
        services.AddScoped<ILotCapacityPolicyStore, LotCapacityStore>();
        services.AddScoped<ILotAdmissionGate, LotAdmissionGate>();
        services.AddScoped<ILotCapacityRealtimePublisher, NotificationCapacityPublisher>();
        services.AddScoped<IActiveSubscriberCounter, BookingSubscriberCounter>();
        // US-069: pull trạng thái vận hành bãi từ ParkingService khi xe vào —
        // chặn nếu bãi đang TẠM ĐÓNG KHẨN CẤP (fail-open khi ParkingService chết).
        services.AddHttpClient<ILotOperationalStatusProvider, ParkingServiceOperationalStatusClient>((_, client) =>
        {
            client.BaseAddress = new Uri(configuration["Services:ParkingService:BaseUrl"] ?? "http://localhost:5103");
            client.Timeout = TimeSpan.FromSeconds(3);
        });
        services.AddHttpClient<NotificationCapacityPublisher>((_, client) =>
            client.BaseAddress = new Uri(configuration["NotificationService:BaseUrl"] ?? "http://localhost:5106"));
        services.AddHttpClient<BookingSubscriberCounter>((_, client) =>
            client.BaseAddress = new Uri(configuration["BookingService:BaseUrl"] ?? "http://localhost:5104"));
        return services;
    }

    private static int ParsePositiveInt(string? raw, int fallback)
        => int.TryParse(raw, out var value) && value > 0 ? value : fallback;
}
