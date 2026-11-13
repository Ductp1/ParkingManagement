using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ParkingManagement.ServiceDefaults;
using ParkingManagement.ServiceDefaults.Messaging;
using ParkingManagement.ServiceDefaults.Persistence;
using ParkingService.Application.Abstractions;
using ParkingService.Application.Features.KybMisdeclaration;
using ParkingService.Application.Features.LotHeartbeat;
using ParkingService.Application.Features.LotOperations;
using ParkingService.Infrastructure.HttpClients;
using ParkingService.Infrastructure.Integrations;
using ParkingService.Infrastructure.Persistence;
using ParkingService.Infrastructure.Persistence.Queries;
using ParkingService.Infrastructure.Persistence.Repositories;
using ParkingService.Infrastructure.Persistence.Seeding;
using ParkingService.Infrastructure.Workers;

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
        services.AddScoped<ISlotStateRepository, SlotStateRepository>(); // US-039/US-049: trạng thái vận hành slot + SlotStateLogs
        // US-052: kết nối PMS (xác thực API key, đẩy booking qua webhook ký HMAC).
        services.AddScoped<ILotIntegrationRepository, LotIntegrationRepository>();
        services.AddHttpClient<IPmsWebhookSender, PmsWebhookSender>(client => client.Timeout = TimeSpan.FromSeconds(5));
        services.AddScoped<ILayoutVersionRepository, LayoutVersionRepository>();
        services.AddScoped<ILotCapacityConfigRepository, LotCapacityConfigRepository>();
        services.AddScoped<IClosureScheduleRepository, ClosureScheduleRepository>(); // US-063
        services.AddScoped<ILotDetailsRepository, LotDetailsRepository>();           // US-064
        services.AddScoped<ILotOperatingHourRepository, LotOperatingHourRepository>();
        services.AddScoped<ILotPhysicalChangeRepository, LotPhysicalChangeRepository>(); // US-061
        services.AddScoped<IKybApplicationRepository, KybApplicationRepository>();
        services.AddScoped<IKybReviewRepository, KybReviewRepository>();       // US-055 (ghi)
        services.AddScoped<IAdminKybQueries, AdminKybQueries>();               // US-055 (đọc)
        services.AddScoped<IAdminLotSummaryQueries, AdminLotSummaryQueries>(); // US-081 (đọc)
        services.AddScoped<ILotMisdeclarationRepository, LotMisdeclarationRepository>(); // US-056 (ghi)
        services.AddScoped<ILotMisdeclarationQueries, LotMisdeclarationQueries>();       // US-056 (đọc)
        services.AddScoped<ISanctionStateRepository, SanctionStateRepository>();         // US-097 (ghi)
        services.AddScoped<ISanctionStateQueries, SanctionStateQueries>();               // US-097 (đọc, cả tìm kiếm)
        // US-056: hạn sửa mặc định của sai sót nhẹ ("KybMisdeclaration:MinorCorrectionDays", 1–30, mặc định 7) – sai cấu hình → lỗi lúc khởi động.
        services.AddSingleton(new KybMisdeclarationSettings(
            configuration.GetValue<int?>($"{KybMisdeclarationSettings.SectionName}:MinorCorrectionDays")
            ?? KybMisdeclarationSettings.DefaultMinorCorrectionDays));
        // US-068: ngưỡng stale ("LotHeartbeat:StaleAfterMinutes", 1–120, mặc định 3) + chu kỳ quét worker ("PollSeconds", 5–3600, mặc định 30).
        services.AddSingleton(new LotHeartbeatSettings(
            configuration.GetValue<int?>($"{LotHeartbeatSettings.SectionName}:StaleAfterMinutes")
                ?? LotHeartbeatSettings.DefaultStaleAfterMinutes,
            configuration.GetValue<int?>($"{LotHeartbeatSettings.SectionName}:PollSeconds")
                ?? LotHeartbeatSettings.DefaultPollSeconds));
        services.AddScoped<ILotStalenessRepository, LotStalenessRepository>();
        // US-068: worker quét bãi Active quá hạn heartbeat → IsDataStale + event LotDataMarkedStale (outbox).
        services.AddHostedService<StalenessMonitorWorker>();
        services.AddScoped<IDataSeeder<ParkingDbContext>, ParkingDataSeeder>();

        // Lot Operations (US-067/068/069): repository + outbox publisher dùng CHUNG ParkingDbContext scope
        // (use case Enqueue rồi SaveChanges → lot + audit log + outbox atomic).
        services.AddScoped<ILotOperationsRepository, LotOperationsRepository>();
        services.AddScoped<ILotEventPublisher, OutboxLotEventPublisher>();

        // US-067/069: tra cứu booking của bãi từ BookingService qua HTTP nội bộ (mặc định :5104).
        services.AddHttpClient<ILotBookingLookup, BookingServiceLotBookingLookup>((_, client) =>
        {
            client.BaseAddress = new Uri(configuration["BookingService:BaseUrl"] ?? "http://localhost:5104/");
            client.Timeout = TimeSpan.FromSeconds(5); // đủ nhanh để emergency stop không bị treo khi BookingService chết
        });

        // US-067/068: dashboard dùng StalenessOptions (threshold phút không heartbeat) — worker stale đã đăng ký 1 lần ở trên.
        services.Configure<StalenessOptions>(configuration.GetSection(StalenessOptions.SectionName));
        services.AddSingleton(sp => sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<StalenessOptions>>().Value);

        // US-069/068: publisher-only bus (không consumer) + OutboxDispatcher đẩy LotEmergencyStopped,
        // LotDataMarkedStale/LotDataRecovered... sang NotificationService (at-least-once, EventId chống trùng).
        services.AddRabbitMqEventBus(configuration);
        services.AddHostedService<OutboxDispatcher<ParkingDbContext>>();

        return services;
    }
}
