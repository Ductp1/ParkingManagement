namespace AdminService.Domain.Enums;

/// <summary>
/// [AdminService] Trạng thái đồng bộ một chế tài sang UserService (US-096: khóa / mở khóa chủ bãi).
/// Chế tài ghi vào pm_admin trước rồi mới gọi UserService, nên cần biết lời gọi đó đã thành công hay chưa.
/// Chế tài đang Active = đồng bộ thao tác khóa; chế tài đã Revoked = đồng bộ thao tác mở khóa.
/// </summary>
public enum SanctionSyncStatus
{
    /// <summary>Chế tài không cần báo UserService (cảnh báo, tụt hạng, chế tài cấp bãi...).</summary>
    NotRequired = 0,
    /// <summary>Đã ghi chế tài, chưa có kết quả từ UserService.</summary>
    Pending = 1,
    /// <summary>UserService đã áp dụng.</summary>
    Synced = 2,
    /// <summary>Gọi UserService thất bại – gọi lại thao tác khóa / mở khóa để thử lại.</summary>
    Failed = 3
}
