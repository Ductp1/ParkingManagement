using PaymentService.Domain.Entities;

namespace PaymentService.Application.Interfaces;

/// <summary>
/// Cổng lưu trữ và quản lý bảng giá bãi xe (RateCard) và các quy tắc block giá (RateRule) (US-070, US-071).
/// </summary>
public interface IRateCardManagementRepository
{
    /// <summary>
    /// Lấy danh sách toàn bộ bảng giá của một bãi xe kèm các quy tắc chi tiết.
    /// </summary>
    Task<IReadOnlyList<RateCard>> GetByParkingLotIdAsync(int parkingLotId, CancellationToken cancellationToken);

    /// <summary>
    /// Tìm một bảng giá theo khóa chính Id kèm các quy tắc chi tiết.
    /// </summary>
    Task<RateCard?> GetByIdAsync(int id, CancellationToken cancellationToken);

    /// <summary>
    /// Lấy bảng giá đang có hiệu lực của bãi xe để đóng thời hạn khi có bảng giá mới.
    /// </summary>
    Task<RateCard?> GetCurrentActiveAsync(int parkingLotId, DateTime atUtc, CancellationToken cancellationToken);

    /// <summary>
    /// Thêm một bảng giá mới cùng toàn bộ các quy tắc con vào cơ sở dữ liệu.
    /// </summary>
    Task AddAsync(RateCard rateCard, CancellationToken cancellationToken);

    /// <summary>
    /// Cập nhật thông tin bảng giá và lưu các thay đổi xuống cơ sở dữ liệu.
    /// </summary>
    Task UpdateAsync(RateCard rateCard, CancellationToken cancellationToken);
}

