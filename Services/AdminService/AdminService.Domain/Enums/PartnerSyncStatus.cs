namespace AdminService.Domain.Enums;

/// <summary>
/// [AdminService] Kết quả một việc phụ chạy sau khi hồ sơ đối tác đã lưu (US-116): gỡ tài khoản chủ bãi chưa kích hoạt bên UserService
/// khi hủy hồ sơ, hoặc gửi email yêu cầu bổ sung. Hồ sơ lưu trước; việc phụ lỗi thì để Failed + lý do để Admin thử lại.
/// </summary>
public enum PartnerSyncStatus
{
    /// <summary>Không có việc phụ (hồ sơ chưa cấp tài khoản / chưa yêu cầu bổ sung).</summary>
    NotRequired = 0,
    Pending = 1,
    Synced = 2,
    Failed = 3,
}
