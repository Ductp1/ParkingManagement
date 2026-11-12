using PaymentService.Domain.Entities;

namespace PaymentService.Application.Interfaces;

/// <summary>
/// Cổng truy xuất và lưu trữ dữ liệu mã khuyến mãi và lịch sử áp dụng mã (US-073).
/// </summary>
public interface IPromotionRepository
{
    /// <summary>Tìm mã khuyến mãi theo chuỗi code duy nhất (chuẩn hóa in hoa).</summary>
    Task<Promotion?> GetByCodeAsync(string code, CancellationToken cancellationToken = default);

    /// <summary>Tìm mã khuyến mãi theo khóa chính ID.</summary>
    Task<Promotion?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>Tra cứu danh sách mã khuyến mãi theo bãi xe hoặc toàn sàn.</summary>
    Task<IReadOnlyList<Promotion>> ListAsync(int? parkingLotId = null, bool? isActiveOnly = null, CancellationToken cancellationToken = default);

    /// <summary>Thêm mới một mã khuyến mãi vào cơ sở dữ liệu.</summary>
    Task AddAsync(Promotion promotion, CancellationToken cancellationToken = default);

    /// <summary>Cập nhật thông tin mã khuyến mãi trong cơ sở dữ liệu.</summary>
    Task UpdateAsync(Promotion promotion, CancellationToken cancellationToken = default);

    /// <summary>Kiểm tra xem mã code đã tồn tại trong hệ thống hay chưa (loại trừ ID đang sửa nếu có).</summary>
    Task<bool> ExistsByCodeAsync(string code, int? excludeId = null, CancellationToken cancellationToken = default);

    /// <summary>Ghi nhận một lượt sử dụng mã khuyến mãi khi thanh toán thành công.</summary>
    Task AddRedemptionAsync(PromotionRedemption redemption, CancellationToken cancellationToken = default);
}

