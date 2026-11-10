using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ParkingManagement.ServiceDefaults;
using ParkingManagement.ServiceDefaults.Messaging;
using ParkingManagement.ServiceDefaults.Persistence;
using ParkingService.Application.Abstractions;
using ParkingService.Application.Features.KybMisdeclaration;
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
        services.AddScoped<ILayoutVersionRepository, LayoutVersionRepository>();
        services.AddScoped<ILotCapacityConfigRepository, LotCapacityConfigRepository>();
        services.AddScoped<ILotDetailsRepository, LotDetailsRepository>();           // US-064
        services.AddScoped<ILotOperatingHourRepository, LotOperatingHourRepository>();
        services.AddScoped<IKybApplicationRepository, KybApplicationRepository>();
        services.AddScoped<IKybReviewRepository, KybReviewRepository>();       // US-055 (ghi)
        services.AddScoped<IAdminKybQueries, AdminKybQueries>();               // US-055 (đọc)
        services.AddScoped<IAdminLotSummaryQueries, AdminLotSummaryQueries>(); // US-081 (đọc)
        services.AddScoped<ILotMisdeclarationRepository, LotMisdeclarationRepository>(); // US-056 (ghi)
        services.AddScoped<ILotMisdeclarationQueries, LotMisdeclarationQueries>();       // US-056 (đọc)
        // US-056: hạn sửa mặc định của sai sót nhẹ ("KybMisdeclaration:MinorCorrectionDays", 1–30, mặc định 7) – sai cấu hình → lỗi lúc khởi động.
        services.AddSingleton(new KybMisdeclarationSettings(
            configuration.GetValue<int?>($"{KybMisdeclarationSettings.SectionName}:MinorCorrectionDays")
            ?? KybMisdeclarationSettings.DefaultMinorCorrectionDays));
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

        // US-068: worker nền quét heartbeat → đánh dấu stale + phát LotDataMarkedStale.
        services.Configure<StalenessOptions>(configuration.GetSection(StalenessOptions.SectionName));
        // Use case dashboard nhận StalenessOptions trực tiếp (threshold minutes) → đăng ký cả instance.
        services.AddSingleton(sp => sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<StalenessOptions>>().Value);
        services.AddHostedService<StalenessMonitorWorker>();

        // US-069/068: publisher-only bus (không consumer) + OutboxDispatcher đẩy LotEmergencyStopped,
        // LotDataMarkedStale/LotDataRecovered... sang NotificationService (at-least-once, EventId chống trùng).
        services.AddRabbitMqEventBus(configuration);
        services.AddHostedService<OutboxDispatcher<ParkingDbContext>>();

        return services;
    }
}
