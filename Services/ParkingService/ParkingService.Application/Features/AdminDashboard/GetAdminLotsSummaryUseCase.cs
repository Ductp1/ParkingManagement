using ParkingService.Application.Abstractions;

namespace ParkingService.Application.Features.AdminDashboard;

/// <summary>US-081: danh sách bãi cho bảng lấp đầy của Admin dashboard (AdminService ghép với số xe đang đỗ của GateService).</summary>
public sealed record AdminLotsSummaryDto(DateTime AsOfUtc, IReadOnlyList<AdminLotSummaryRow> Lots);

public interface IGetAdminLotsSummaryUseCase
{
    Task<AdminLotsSummaryDto> ExecuteAsync(CancellationToken cancellationToken = default);
}

/// <summary>US-081 (UC-45): bãi + quận + trạng thái + tổng chỗ + cờ Emergency Stop / tạm dừng đặt ngay – chỉ đọc, chỉ Admin.</summary>
public sealed class GetAdminLotsSummaryUseCase(IAdminLotSummaryQueries queries, TimeProvider timeProvider) : IGetAdminLotsSummaryUseCase
{
    public async Task<AdminLotsSummaryDto> ExecuteAsync(CancellationToken cancellationToken = default)
        => new(timeProvider.GetUtcNow().UtcDateTime, await queries.ListAsync(cancellationToken));
}
