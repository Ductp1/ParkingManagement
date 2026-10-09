using ParkingManagement.SharedKernel.Enums;

namespace SupportService.Application.Interfaces;

/// <summary>Thông tin booking SupportService cần để kiểm tra khiếu nại (lấy từ BookingService).</summary>
/// <param name="OwnerProfileId">Null nếu BookingService chưa trả về chủ bãi.</param>
/// <param name="LastChangedAtUtc">Thời điểm đổi trạng thái gần nhất (quyết định hủy, check-out...).</param>
public sealed record BookingSnapshot(
    int Id,
    string Code,
    BookingStatus Status,
    int UserId,
    int ParkingLotId,
    int? OwnerProfileId,
    DateTime EndAtUtc,
    DateTime? LastChangedAtUtc,
    decimal PaidAmount);

/// <summary>Port tra cứu booking – Infrastructure cài đặt bằng HTTP tới BookingService.</summary>
public interface IBookingLookup
{
    /// <summary>Null nếu booking không tồn tại.</summary>
    Task<BookingSnapshot?> FindByCodeAsync(string bookingCode, CancellationToken cancellationToken = default);
}
