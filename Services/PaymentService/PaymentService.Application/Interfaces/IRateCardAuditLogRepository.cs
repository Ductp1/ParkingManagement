using PaymentService.Domain.Entities;

namespace PaymentService.Application.Interfaces;

/// <summary>
/// Cổng lưu trữ và tra cứu lịch sử kiểm toán thay đổi bảng giá (US-072).
/// </summary>
public interface IRateCardAuditLogRepository
{
    /// <summary>Thêm mới một bản ghi nhật ký kiểm toán thay đổi giá.</summary>
    Task AddAsync(RateCardAuditLog auditLog, CancellationToken cancellationToken);

    /// <summary>Lấy toàn bộ lịch sử thay đổi giá của một bãi xe.</summary>
    Task<IReadOnlyList<RateCardAuditLog>> GetByParkingLotIdAsync(int parkingLotId, CancellationToken cancellationToken);
}

