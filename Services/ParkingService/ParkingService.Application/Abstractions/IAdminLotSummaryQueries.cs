using ParkingManagement.SharedKernel.Enums;

namespace ParkingService.Application.Abstractions;

/// <summary>Một bãi (chưa xóa) cho Admin dashboard (US-081): thông tin công khai + cờ vận hành, không có chủ bãi / liên hệ.</summary>
public sealed record AdminLotSummaryRow(int ParkingLotId, string Name, string City, string? District, ParkingLotStatus Status,
    IntegrationTier IntegrationTier, int TotalSlots, bool IsEmergencyStopped, bool InstantBookingPaused);

/// <summary>PORT đọc (CQRS-lite) cho Admin dashboard (US-081): AsNoTracking + Select ở database.</summary>
public interface IAdminLotSummaryQueries
{
    /// <summary>Mọi bãi chưa xóa mềm, sắp theo tên.</summary>
    Task<IReadOnlyList<AdminLotSummaryRow>> ListAsync(CancellationToken cancellationToken = default);
}
