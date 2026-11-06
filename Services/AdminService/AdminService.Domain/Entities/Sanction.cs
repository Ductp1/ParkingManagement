using AdminService.Domain.Enums;
using ParkingManagement.SharedKernel.Domain;
using ParkingManagement.SharedKernel.Enums;

namespace AdminService.Domain.Entities;

/// <summary>[AdminService] Chế tài SLA 4 cấp áp lên chủ bãi / bãi (UC-41, Nghiệp vụ v3 §4.1, Điều 7 hợp đồng mẫu).</summary>
public class Sanction : BaseEntity
{
    public const int ReasonMaxLength = 1000;
    /// <summary>Điều 7.1 cấp 2: hạ thứ hạng hiển thị tối đa 30 ngày.</summary>
    public const int RankingPenaltyMaxDays = 30;

    /// <summary>→ UserService (không FK).</summary>
    public int OwnerProfileId { get; set; }
    /// <summary>US-097: user sở hữu hồ sơ chủ bãi (đã xác minh qua UserService) – người nhận thông báo. Null với chế tài cũ.</summary>
    public int? OwnerUserId { get; set; }
    /// <summary>null = áp lên toàn bộ chủ bãi.</summary>
    public int? ParkingLotId { get; set; }
    public SanctionLevel Level { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string? EvidenceJson { get; set; }
    public DateTime StartsAtUtc { get; set; }
    /// <summary>null = vĩnh viễn (cấp 4). Cảnh cáo: trùng hạn khắc phục.</summary>
    public DateTime? EndsAtUtc { get; set; }
    /// <summary>US-097: hạn khắc phục của cảnh cáo (cấp 1).</summary>
    public DateTime? CorrectionDueAtUtc { get; set; }
    /// <summary>US-097 (Điều 7.3): hạn chủ bãi khiếu nại – cuối ngày làm việc thứ 5 (ICT) sau ngày áp.</summary>
    public DateTime? AppealDeadlineUtc { get; set; }
    public SanctionStatus Status { get; set; } = SanctionStatus.Active;
    /// <summary>Tiền phạt trừ vào quyết toán / ký quỹ của chủ bãi (nếu có) – dùng để đền bù khách (Answer_2 §10).</summary>
    public decimal? PenaltyAmount { get; set; }
    public int IssuedByUserId { get; set; }
    public DateTime? RevokedAtUtc { get; set; }
    public int? RevokedByUserId { get; set; }
    public string? RevokeReason { get; set; }
    /// <summary>Kết quả báo UserService khi chế tài này khóa / mở khóa chủ bãi (US-096).</summary>
    public SanctionSyncStatus UserServiceSyncStatus { get; set; } = SanctionSyncStatus.NotRequired;
    /// <summary>Lúc UserService xác nhận thao tác hiện tại (khóa khi Active, mở khóa khi Revoked); null = chưa xác nhận.</summary>
    public DateTime? UserServiceSyncedAtUtc { get; set; }
    /// <summary>US-097: trạng thái báo ParkingService (hạ hạng / tạm ngừng niêm yết khi Active, gỡ khi Revoked).</summary>
    public SanctionSyncStatus ParkingSyncStatus { get; set; } = SanctionSyncStatus.NotRequired;
    public DateTime? ParkingSyncedAtUtc { get; set; }
    /// <summary>US-097: trạng thái gửi thông báo cho chủ bãi (áp chế tài khi Active, gỡ khi Revoked).</summary>
    public SanctionSyncStatus NotificationSyncStatus { get; set; } = SanctionSyncStatus.NotRequired;
    public DateTime? NotificationSentAtUtc { get; set; }

    /// <summary>Khóa toàn bộ chủ bãi (cấp 3 có hạn / cấp 4 vĩnh viễn) – đồng bộ sang UserService (US-096).</summary>
    public bool IsOwnerLock => ParkingLotId is null && Level is SanctionLevel.TemporarySuspension or SanctionLevel.PermanentBan;

    /// <summary>
    /// US-096: Chế tài khóa toàn bộ chủ bãi. Có hạn → cấp 3 (tạm khóa tới lockedUntilUtc); không hạn → cấp 4 (vĩnh viễn).
    /// Mới tạo luôn ở trạng thái chờ đồng bộ, vì UserService chỉ được gọi sau khi chế tài đã lưu.
    /// US-097 truyền thêm extras (người nhận, tiền phạt, bằng chứng, hạn khiếu nại) và bật gửi thông báo; US-096 không truyền.
    /// </summary>
    public static Sanction CreateOwnerLock(int ownerProfileId, string reason, int issuedByUserId, DateTime startsAtUtc, DateTime? lockedUntilUtc,
        SanctionExtras? extras = null)
        => new()
        {
            OwnerProfileId = ownerProfileId,
            OwnerUserId = extras?.OwnerUserId,
            ParkingLotId = null,
            Level = lockedUntilUtc is null ? SanctionLevel.PermanentBan : SanctionLevel.TemporarySuspension,
            Reason = reason,
            EvidenceJson = extras?.EvidenceJson,
            StartsAtUtc = startsAtUtc,
            EndsAtUtc = lockedUntilUtc,
            AppealDeadlineUtc = extras?.AppealDeadlineUtc,
            Status = SanctionStatus.Active,
            PenaltyAmount = extras?.PenaltyAmount,
            IssuedByUserId = issuedByUserId,
            UserServiceSyncStatus = SanctionSyncStatus.Pending,
            NotificationSyncStatus = extras is null ? SanctionSyncStatus.NotRequired : SanctionSyncStatus.Pending,
        };

    /// <summary>
    /// US-097: chế tài không khóa tài khoản – cảnh cáo (bãi / chủ bãi), hạ hạng (bãi / chủ bãi), tạm ngừng niêm yết (bãi).
    /// Quy tắc từng cấp đã kiểm ở use case; ở đây chỉ chặn tổ hợp sai để không thể tạo nhầm một owner lock.
    /// </summary>
    public static Sanction Issue(int ownerProfileId, int? parkingLotId, SanctionLevel level, string reason, int issuedByUserId,
        DateTime startsAtUtc, DateTime? endsAtUtc, DateTime? correctionDueAtUtc, SanctionExtras extras)
    {
        if (level == SanctionLevel.PermanentBan || (level == SanctionLevel.TemporarySuspension && parkingLotId is null))
            throw new InvalidOperationException("Khóa chủ bãi (cấp 3 toàn chủ bãi / cấp 4) phải tạo bằng CreateOwnerLock.");

        return new Sanction
        {
            OwnerProfileId = ownerProfileId,
            OwnerUserId = extras.OwnerUserId,
            ParkingLotId = parkingLotId,
            Level = level,
            Reason = reason,
            EvidenceJson = extras.EvidenceJson,
            StartsAtUtc = startsAtUtc,
            EndsAtUtc = level == SanctionLevel.Warning ? correctionDueAtUtc : endsAtUtc,
            CorrectionDueAtUtc = level == SanctionLevel.Warning ? correctionDueAtUtc : null,
            AppealDeadlineUtc = extras.AppealDeadlineUtc,
            Status = SanctionStatus.Active,
            PenaltyAmount = extras.PenaltyAmount,
            IssuedByUserId = issuedByUserId,
            UserServiceSyncStatus = SanctionSyncStatus.NotRequired,
            // Hạ hạng / tạm ngừng niêm yết do ParkingService thi hành; cảnh cáo chỉ cần thông báo.
            ParkingSyncStatus = level == SanctionLevel.Warning ? SanctionSyncStatus.NotRequired : SanctionSyncStatus.Pending,
            NotificationSyncStatus = SanctionSyncStatus.Pending,
        };
    }

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

    /// <summary>
    /// US-097: Admin gỡ chế tài, ghi người / lúc / lý do. Mỗi service đã nhận lệnh áp phải nhận lệnh gỡ → về chờ đồng bộ;
    /// service không liên quan giữ NotRequired.
    /// </summary>
    public void Revoke(string reason, int revokedByUserId, DateTime nowUtc)
    {
        Status = SanctionStatus.Revoked;
        RevokedAtUtc = nowUtc;
        RevokedByUserId = revokedByUserId;
        RevokeReason = reason;
        if (IsOwnerLock || UserServiceSyncStatus != SanctionSyncStatus.NotRequired)
        {
            UserServiceSyncStatus = SanctionSyncStatus.Pending;
            UserServiceSyncedAtUtc = null;
        }
        if (ParkingSyncStatus != SanctionSyncStatus.NotRequired)
        {
            ParkingSyncStatus = SanctionSyncStatus.Pending;
            ParkingSyncedAtUtc = null;
        }
        if (NotificationSyncStatus != SanctionSyncStatus.NotRequired)
        {
            NotificationSyncStatus = SanctionSyncStatus.Pending;
            NotificationSentAtUtc = null;
        }
    }

    /// <summary>
    /// US-097: khóa tạm (cấp 3) được thay bằng khóa vĩnh viễn (cấp 4). Không cần mở khóa bên UserService (khóa mới ghi đè),
    /// chế tài mới gửi thông báo – nên khóa cũ không còn gì phải đồng bộ.
    /// </summary>
    public void Supersede(string reason, int revokedByUserId, DateTime nowUtc)
    {
        Status = SanctionStatus.Revoked;
        RevokedAtUtc = nowUtc;
        RevokedByUserId = revokedByUserId;
        RevokeReason = reason;
        UserServiceSyncStatus = SanctionSyncStatus.NotRequired;
        NotificationSyncStatus = SanctionSyncStatus.NotRequired;
    }

    /// <summary>
    /// US-097: job đóng chế tài tới hạn. ParkingService tự hết hiệu lực theo thời gian → lệnh áp chưa tới không cần gửi nữa;
    /// khóa chủ bãi cần mở khóa bên UserService (job không có token → chờ Admin đồng bộ lại); chủ bãi được báo hết hạn.
    /// </summary>
    public void ExpireOnSchedule()
    {
        Status = SanctionStatus.Expired;
        if (IsOwnerLock)
        {
            UserServiceSyncStatus = SanctionSyncStatus.Pending;
            UserServiceSyncedAtUtc = null;
        }
        if (ParkingSyncStatus is SanctionSyncStatus.Pending or SanctionSyncStatus.Failed)
            ParkingSyncStatus = SanctionSyncStatus.NotRequired;
        if (NotificationSyncStatus != SanctionSyncStatus.NotRequired)
        {
            NotificationSyncStatus = SanctionSyncStatus.Pending;
            NotificationSentAtUtc = null;
        }
    }

    /// <summary>Còn service nào chưa nhận / nhận lỗi lệnh hiện tại.</summary>
    public bool HasPendingSync => IsPendingOrFailed(UserServiceSyncStatus) || IsPendingOrFailed(ParkingSyncStatus)
                                  || IsPendingOrFailed(NotificationSyncStatus);

    public static bool IsPendingOrFailed(SanctionSyncStatus status) => status is SanctionSyncStatus.Pending or SanctionSyncStatus.Failed;

    public void MarkParkingSynced(DateTime syncedAtUtc)
    {
        ParkingSyncStatus = SanctionSyncStatus.Synced;
        ParkingSyncedAtUtc = syncedAtUtc;
    }

    public void MarkParkingSyncFailed() => ParkingSyncStatus = SanctionSyncStatus.Failed;

    public void MarkNotificationSent(DateTime sentAtUtc)
    {
        NotificationSyncStatus = SanctionSyncStatus.Synced;
        NotificationSentAtUtc = sentAtUtc;
    }

    public void MarkNotificationFailed() => NotificationSyncStatus = SanctionSyncStatus.Failed;

    public void MarkSynced(DateTime syncedAtUtc)
    {
        UserServiceSyncStatus = SanctionSyncStatus.Synced;
        UserServiceSyncedAtUtc = syncedAtUtc;
    }

    public void MarkSyncFailed() => UserServiceSyncStatus = SanctionSyncStatus.Failed;
}

/// <summary>US-097: dữ liệu bổ sung khi Admin áp chế tài qua màn Sanctions.</summary>
public sealed record SanctionExtras(int OwnerUserId, decimal? PenaltyAmount, string? EvidenceJson, DateTime AppealDeadlineUtc);
