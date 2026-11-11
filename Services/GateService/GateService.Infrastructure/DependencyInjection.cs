using GateService.Application.Features;
using GateService.Application.Features.GateConsole;
using GateService.Application.Features.GateDevices;
using GateService.Application.Features.GateEvents;
using GateService.Application.Features.GateExceptions;
using GateService.Application.Features.LotCapacity;
using GateService.Application.Features.PlateRecognition;
using GateService.Application.Features.PmsIntegration;
using GateService.Application.Features.StaffCancellations;
using GateService.Infrastructure.HttpClients;
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
        // US-039: thao tác No-show cài trên cùng GateBookingClient (cùng BaseUrl BookingService).
        services.AddHttpClient<IGateNoShowClient, GateBookingClient>((sp, client) =>
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
            // US-039: mốc 30' Staff được hủy No-show; US-077: ngưỡng hủy No-show trong ca để gắn cờ ca.
            NoShowGraceMinutes = ParsePositiveInt(configuration["GateConsole:NoShowGraceMinutes"], 30),
            AbnormalNoShowCancelThreshold = ParsePositiveInt(configuration["GateConsole:AbnormalNoShowCancelThreshold"], 5),
            CriticalNoShowCancelThreshold = ParsePositiveInt(configuration["GateConsole:CriticalNoShowCancelThreshold"], 10),
            StaffCancellationWindowHours = ParsePositiveInt(configuration["GateConsole:StaffCancellationWindowHours"], 24),
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
        // US-039/US-077: ca trực (tăng bộ đếm hủy No-show / ngoại lệ, gắn cờ ca bất thường).
        services.AddScoped<IShiftStore, ShiftStore>();
        // US-039/US-049: gọi ParkingService (TV5) – ghi đè trạng thái slot / nhả chỗ.
        services.AddHttpClient<IParkingSlotClient, ParkingServiceClient>((sp, client) =>
        {
            client.BaseAddress = new Uri(sp.GetRequiredService<IConfiguration>()["Services:ParkingService:BaseUrl"] ?? "http://localhost:5103");
            client.Timeout = TimeSpan.FromSeconds(10);
        });
        // US-048/US-077: đọc audit GateEvents.
        services.AddScoped<IGateEventQueries, GateEventQueries>();
        // US-052: sự kiện PMS – auth qua ParkingService, inbox chống trùng, hàng chờ xử lý tay.
        services.AddHttpClient<IPmsAuthClient, PmsAuthClient>((sp, client) =>
        {
            client.BaseAddress = new Uri(sp.GetRequiredService<IConfiguration>()["Services:ParkingService:BaseUrl"] ?? "http://localhost:5103");
            client.Timeout = TimeSpan.FromSeconds(10);
        });
        // US-077: đẩy cảnh báo Staff hủy No-show bất thường sang AdminService (endpoint /internal, khóa nội bộ).
        var adminBaseUrl = configuration[AdminServiceConnection.BaseUrlKey];
        services.AddSingleton(new AdminServiceConnection(
            new Uri((string.IsNullOrWhiteSpace(adminBaseUrl) ? "http://localhost:5108" : adminBaseUrl.TrimEnd('/')) + "/"),
            configuration[AdminServiceConnection.ApiKeyKey]));
        services.AddHttpClient<IAdminAlertClient, AdminAlertClient>(client => client.Timeout = TimeSpan.FromSeconds(5));
        services.AddScoped<IPmsInbox, PmsInbox>();
        services.AddScoped<IPmsQuarantineStore, PmsQuarantineStore>();
        return services;
    }

    private static int ParsePositiveInt(string? raw, int fallback)
        => int.TryParse(raw, out var value) && value > 0 ? value : fallback;
}


