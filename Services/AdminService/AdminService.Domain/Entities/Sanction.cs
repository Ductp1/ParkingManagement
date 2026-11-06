using AdminService.Domain.Enums;
using ParkingManagement.SharedKernel.Domain;
using ParkingManagement.SharedKernel.Enums;

namespace AdminService.Domain.Entities;

/// <summary>[AdminService] Chế tài SLA 4 cấp áp lên chủ bãi / bãi (UC-41, Nghiệp vụ v3 §4.1).</summary>
public class Sanction : BaseEntity
{
    /// <summary>→ UserService (không FK).</summary>
    public int OwnerProfileId { get; set; }
    /// <summary>null = áp lên toàn bộ chủ bãi.</summary>
    public int? ParkingLotId { get; set; }
    public SanctionLevel Level { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string? EvidenceJson { get; set; }
    public DateTime StartsAtUtc { get; set; }
    /// <summary>null = vĩnh viễn (cấp 4).</summary>
    public DateTime? EndsAtUtc { get; set; }
    public SanctionStatus Status { get; set; } = SanctionStatus.Active;
    /// <summary>Tiền phạt trừ vào quyết toán / ký quỹ của chủ bãi (nếu có) – dùng để đền bù khách (Answer_2 §10).</summary>
    public decimal? PenaltyAmount { get; set; }
    public int IssuedByUserId { get; set; }
    /// <summary>Kết quả báo UserService khi chế tài này khóa / mở khóa chủ bãi (US-096).</summary>
    public SanctionSyncStatus UserServiceSyncStatus { get; set; } = SanctionSyncStatus.NotRequired;
    /// <summary>Lúc UserService xác nhận thao tác hiện tại (khóa khi Active, mở khóa khi Revoked); null = chưa xác nhận.</summary>
    public DateTime? UserServiceSyncedAtUtc { get; set; }

    /// <summary>
    /// US-096: Chế tài khóa toàn bộ chủ bãi. Có hạn → cấp 3 (tạm khóa tới lockedUntilUtc); không hạn → cấp 4 (vĩnh viễn).
    /// Mới tạo luôn ở trạng thái chờ đồng bộ, vì UserService chỉ được gọi sau khi chế tài đã lưu.
    /// </summary>
    public static Sanction CreateOwnerLock(int ownerProfileId, string reason, int issuedByUserId, DateTime startsAtUtc, DateTime? lockedUntilUtc)
        => new()
        {
            OwnerProfileId = ownerProfileId,
            ParkingLotId = null,
            Level = lockedUntilUtc is null ? SanctionLevel.PermanentBan : SanctionLevel.TemporarySuspension,
            Reason = reason,
            StartsAtUtc = startsAtUtc,
            EndsAtUtc = lockedUntilUtc,
            Status = SanctionStatus.Active,
            IssuedByUserId = issuedByUserId,
            UserServiceSyncStatus = SanctionSyncStatus.Pending,
        };

    /// <summary>Còn hiệu lực = đang Active và chưa tới hạn kết thúc (không hạn thì luôn còn).</summary>
    public bool IsInEffect(DateTime nowUtc) => Status == SanctionStatus.Active && (EndsAtUtc is null || EndsAtUtc > nowUtc);

    /// <summary>Đóng chế tài đã quá hạn mà chưa được gỡ.</summary>
    public void Expire() => Status = SanctionStatus.Expired;

    /// <summary>US-096: Admin gỡ chế tài khóa. Lệnh mở khóa chưa tới UserService nên quay về trạng thái chờ đồng bộ.</summary>
    public void Revoke()
    {
        Status = SanctionStatus.Revoked;
        UserServiceSyncStatus = SanctionSyncStatus.Pending;
        UserServiceSyncedAtUtc = null;      // mốc cũ là của lệnh khóa, không còn đúng cho lệnh mở khóa
    }

    public void MarkSynced(DateTime syncedAtUtc)
    {
        UserServiceSyncStatus = SanctionSyncStatus.Synced;
        UserServiceSyncedAtUtc = syncedAtUtc;
    }

    public void MarkSyncFailed() => UserServiceSyncStatus = SanctionSyncStatus.Failed;
}
