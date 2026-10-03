using ParkingManagement.SharedKernel.Domain;
using ParkingManagement.SharedKernel.Enums;

namespace UserService.Domain.Entities;

/// <summary>
/// [UserService] Yêu cầu của người dùng về dữ liệu cá nhân theo Nghị định 13/2023: xem, sửa, xóa, rút lại đồng ý (US-007, UC-05).
/// Xóa = ẩn danh hóa hồ sơ; dữ liệu giao dịch vẫn giữ 7 năm theo quy định kế toán/thuế.
/// </summary>
public class DataSubjectRequest : BaseEntity
{
    public int UserId { get; set; }
    public DataRequestType RequestType { get; set; }
    public DataRequestStatus Status { get; set; } = DataRequestStatus.Submitted;
    public string? Reason { get; set; }
    /// <summary>Hạn xử lý theo quy định (mặc định 72 giờ).</summary>
    public DateTime DueAtUtc { get; set; }
    public int? HandledByUserId { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public string? ResultNote { get; set; }
    /// <summary>Đường dẫn file dữ liệu đã xuất (với yêu cầu Access/Export).</summary>
    public string? ExportFileUrl { get; set; }

    public User User { get; set; } = null!;
}
