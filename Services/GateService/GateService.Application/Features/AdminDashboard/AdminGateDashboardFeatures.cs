using Microsoft.Extensions.DependencyInjection;
using ParkingManagement.SharedKernel.Enums;

namespace GateService.Application.Features.AdminDashboard;

// ===== DTO =====

/// <summary>US-081: xe đang trong một bãi. Capacity = LotCapacityConfigs.Capacity (US-066), null khi bãi chưa cấu hình sức chứa.</summary>
public sealed record AdminLotOccupancyRowDto(int ParkingLotId, int ActiveSessions, int WalkIn, int Booked, int? Capacity);

/// <summary>
/// US-081: phiên gửi xe đang hoạt động toàn sàn (Status = Active), tách khách vãng lai / có booking.
/// Abnormal = phiên bất thường chưa xử lý, đếm riêng – không cộng vào Total. Không trả biển số / người dùng.
/// </summary>
public sealed record AdminGateOccupancyDto(DateTime AsOfUtc, int Total, int WalkIn, int Booked, int Abnormal,
    IReadOnlyList<AdminLotOccupancyRowDto> Lots);

public sealed record AdminGateDeviceTypeRowDto(GateDeviceType DeviceType, int Online, int Offline, int Maintenance)
{
    public int Total => Online + Offline + Maintenance;
}

/// <summary>US-081: thiết bị cổng đang dùng (IsActive) theo loại × trạng thái.</summary>
public sealed record AdminGateDevicesDto(DateTime AsOfUtc, int Online, int Offline, int Maintenance,
    IReadOnlyList<AdminGateDeviceTypeRowDto> ByType)
{
    public int Total => Online + Offline + Maintenance;
}

// ===== PORT (đọc) =====

public sealed record ActiveSessionGroupRow(int ParkingLotId, bool IsWalkIn, int Count);

public sealed record LotCapacityRow(int ParkingLotId, int Capacity);

public sealed record GateDeviceGroupRow(GateDeviceType DeviceType, GateDeviceStatus Status, int Count);

/// <summary>Số liệu Admin dashboard trong pm_gate – đếm / gộp ở database (AsNoTracking, GroupBy, Count), không tải bản ghi về.</summary>
public interface IAdminGateDashboardQueries
{
    Task<IReadOnlyList<ActiveSessionGroupRow>> CountActiveSessionsAsync(CancellationToken cancellationToken);
    Task<int> CountAbnormalSessionsAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<LotCapacityRow>> ListCapacitiesAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<GateDeviceGroupRow>> CountActiveDevicesAsync(CancellationToken cancellationToken);
}

// ===== USE CASE =====

public interface IGetAdminGateOccupancyUseCase
{
    Task<AdminGateOccupancyDto> ExecuteAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// US-081 (UC-45): lấp đầy toàn sàn cho Admin. Bãi có cấu hình sức chứa nhưng chưa có xe vẫn xuất hiện (0 thật);
/// bãi có xe nhưng chưa cấu hình thì Capacity = null (AdminService tự lấy TotalSlots của ParkingService làm dự phòng).
/// </summary>
public sealed class GetAdminGateOccupancyUseCase(IAdminGateDashboardQueries queries, TimeProvider timeProvider) : IGetAdminGateOccupancyUseCase
{
    public async Task<AdminGateOccupancyDto> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var sessions = await queries.CountActiveSessionsAsync(cancellationToken);
        var abnormal = await queries.CountAbnormalSessionsAsync(cancellationToken);
        var capacities = (await queries.ListCapacitiesAsync(cancellationToken)).ToDictionary(c => c.ParkingLotId, c => c.Capacity);

        var lots = sessions.Select(s => s.ParkingLotId).Concat(capacities.Keys).Distinct()
            .Select(lotId =>
            {
                var walkIn = sessions.Where(s => s.ParkingLotId == lotId && s.IsWalkIn).Sum(s => s.Count);
                var booked = sessions.Where(s => s.ParkingLotId == lotId && !s.IsWalkIn).Sum(s => s.Count);
                return new AdminLotOccupancyRowDto(lotId, walkIn + booked, walkIn, booked,
                    capacities.TryGetValue(lotId, out var capacity) ? capacity : null);
            })
            .OrderBy(l => l.ParkingLotId)
            .ToList();

        return new AdminGateOccupancyDto(timeProvider.GetUtcNow().UtcDateTime, lots.Sum(l => l.ActiveSessions), lots.Sum(l => l.WalkIn),
            lots.Sum(l => l.Booked), abnormal, lots);
    }
}

public interface IGetAdminGateDevicesUseCase
{
    Task<AdminGateDevicesDto> ExecuteAsync(CancellationToken cancellationToken = default);
}

/// <summary>US-081 (UC-45): ma trận thiết bị cổng theo loại × trạng thái; loại chưa có thiết bị nào thì không có dòng.</summary>
public sealed class GetAdminGateDevicesUseCase(IAdminGateDashboardQueries queries, TimeProvider timeProvider) : IGetAdminGateDevicesUseCase
{
    public async Task<AdminGateDevicesDto> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var groups = await queries.CountActiveDevicesAsync(cancellationToken);
        int Count(GateDeviceType type, GateDeviceStatus status)
            => groups.Where(g => g.DeviceType == type && g.Status == status).Sum(g => g.Count);

        var byType = groups.Select(g => g.DeviceType).Distinct().Order()
            .Select(t => new AdminGateDeviceTypeRowDto(t, Count(t, GateDeviceStatus.Online), Count(t, GateDeviceStatus.Offline),
                Count(t, GateDeviceStatus.Maintenance)))
            .ToList();

        return new AdminGateDevicesDto(timeProvider.GetUtcNow().UtcDateTime, byType.Sum(r => r.Online), byType.Sum(r => r.Offline),
            byType.Sum(r => r.Maintenance), byType);
    }
}

// ===== ĐĂNG KÝ =====

public static class AdminGateDashboardRegistration
{
    /// <summary>US-081: use case đọc cho Admin dashboard (gọi 1 dòng từ DependencyInjection của Application).</summary>
    public static IServiceCollection AddAdminGateDashboard(this IServiceCollection services)
    {
        services.AddScoped<IGetAdminGateOccupancyUseCase, GetAdminGateOccupancyUseCase>();
        services.AddScoped<IGetAdminGateDevicesUseCase, GetAdminGateDevicesUseCase>();
        return services;
    }
}
