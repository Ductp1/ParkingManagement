using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ParkingManagement.ServiceDefaults;
using ParkingManagement.ServiceDefaults.Persistence;
using ParkingService.Application.Abstractions;
using ParkingService.Application.Features.KybMisdeclaration;
using ParkingService.Infrastructure.Persistence;
using ParkingService.Infrastructure.Persistence.Queries;
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
        services.AddScoped<ILayoutVersionRepository, LayoutVersionRepository>();
        services.AddScoped<IKybApplicationRepository, KybApplicationRepository>();
        services.AddScoped<IKybReviewRepository, KybReviewRepository>();       // US-055 (ghi)
        services.AddScoped<IAdminKybQueries, AdminKybQueries>();               // US-055 (đọc)
        services.AddScoped<ILotMisdeclarationRepository, LotMisdeclarationRepository>(); // US-056 (ghi)
        services.AddScoped<ILotMisdeclarationQueries, LotMisdeclarationQueries>();       // US-056 (đọc)
        // US-056: hạn sửa mặc định của sai sót nhẹ ("KybMisdeclaration:MinorCorrectionDays", 1–30, mặc định 7) – sai cấu hình → lỗi lúc khởi động.
        services.AddSingleton(new KybMisdeclarationSettings(
            configuration.GetValue<int?>($"{KybMisdeclarationSettings.SectionName}:MinorCorrectionDays")
            ?? KybMisdeclarationSettings.DefaultMinorCorrectionDays));
        services.AddScoped<IDataSeeder<ParkingDbContext>, ParkingDataSeeder>();
        return services;
    }
}
