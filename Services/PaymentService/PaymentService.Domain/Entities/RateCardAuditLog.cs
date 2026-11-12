using ParkingManagement.SharedKernel.Domain;

namespace PaymentService.Domain.Entities;

/// <summary>
/// Thực thể lưu nhật ký kiểm toán thay đổi bảng giá (US-072 AC3).
/// Ghi rõ: ai đổi, lúc nào, giá cũ/mới, phần trăm tăng và cờ cảnh báo duyệt.
/// </summary>
public class RateCardAuditLog : BaseEntity
{
    /// <summary>Mã bảng giá được áp dụng thay đổi.</summary>
    public int RateCardId { get; set; }

    /// <summary>Mã bãi đỗ xe sở hữu bảng giá.</summary>
    public int ParkingLotId { get; set; }

    /// <summary>Mã người dùng thực hiện thao tác (Chủ bãi xe hoặc Admin).</summary>
    public int ChangedByUserId { get; set; }

    /// <summary>Hành động thực hiện (Create, Update, PriceIncreaseWarning).</summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>Chuỗi JSON lưu vết dữ liệu cấu hình giá cũ.</summary>
    public string? OldValueJson { get; set; }

    /// <summary>Chuỗi JSON lưu vết dữ liệu cấu hình giá mới.</summary>
    public string NewValueJson { get; set; } = string.Empty;

    /// <summary>Tỷ lệ phần trăm tăng giá cao nhất được phát hiện so với bảng giá trước đó (ví dụ: 35.5%).</summary>
    public decimal MaxPriceIncreasePercentage { get; set; }

    /// <summary>Đánh dấu true nếu mức tăng giá > 30% cần Admin / Partner Ops phê duyệt thủ công (AC2).</summary>
    public bool RequiresAdminApproval { get; set; }
}

