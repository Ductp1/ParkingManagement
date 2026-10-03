namespace ParkingManagement.SharedKernel.Enums;

/// <summary>
/// Trạng thái của bãi đỗ trên marketplace (theo quy trình KYB – Nghiệp vụ v3 §3.4).
/// </summary>
public enum ParkingLotStatus
{
    /// <summary>Chủ bãi đã đăng ký, chờ Admin duyệt KYB – chưa công khai.</summary>
    PendingApproval = 0,

    /// <summary>Đã duyệt, hiển thị công khai và nhận booking.</summary>
    Active = 1,

    /// <summary>Bị tạm dừng listing (chế tài SLA cấp 3) hoặc Emergency Stop.</summary>
    Suspended = 2
}
