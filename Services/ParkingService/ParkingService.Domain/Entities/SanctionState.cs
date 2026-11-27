using System.Linq.Expressions;
using ParkingManagement.SharedKernel.Domain;
using ParkingManagement.SharedKernel.Exceptions;
using ParkingService.Domain.Enums;

namespace ParkingService.Domain.Entities;

/// <summary>
/// [ParkingService] Bản sao hiệu lực của một chế tài SLA (US-097) do AdminService đẩy sang (PUT idempotent theo SanctionId).
/// Không đổi ParkingLot.Status: hiệu lực tính theo thời gian (StartsAt ≤ now &lt; EndsAt, chưa gỡ) nên hết hạn không cần gọi lại.
/// ParkingLotId = null → áp cho mọi bãi của chủ bãi (cấp 2 theo owner, kể cả bãi tạo sau).
/// </summary>
public class SanctionState : BaseEntity
{
    /// <summary>Id chế tài bên AdminService (không FK) – khóa idempotent.</summary>
    public int SanctionId { get; private set; }
    /// <summary>Chủ bãi bị chế tài (OwnerProfile bên UserService, không FK).</summary>
    public int OwnerProfileId { get; private set; }
    public int? ParkingLotId { get; private set; }
    public SanctionStateKind Kind { get; private set; }
    public DateTime StartsAtUtc { get; private set; }
    /// <summary>null = không thời hạn.</summary>
    public DateTime? EndsAtUtc { get; private set; }
    /// <summary>Admin đẩy trạng thái lần cuối (lấy từ JWT).</summary>
    public int AppliedByUserId { get; private set; }
    /// <summary>Đã gỡ trước hạn (chế tài bị Revoke bên AdminService). Gỡ rồi thì không áp lại.</summary>
    public DateTime? LiftedAtUtc { get; private set; }
    public int? LiftedByUserId { get; private set; }

    // Constructor rỗng cho EF Core.
    private SanctionState() { }

    public static SanctionState Create(int sanctionId, int ownerProfileId, int? parkingLotId, SanctionStateKind kind,
        DateTime startsAtUtc, DateTime? endsAtUtc, int appliedByUserId)
        => new()
        {
            SanctionId = sanctionId,
            OwnerProfileId = ownerProfileId,
            ParkingLotId = parkingLotId,
            Kind = kind,
            StartsAtUtc = startsAtUtc,
            EndsAtUtc = endsAtUtc,
            AppliedByUserId = appliedByUserId,
        };

    /// <summary>
    /// PUT lặp lại (retry) cho cùng chế tài: chỉ cập nhật thời gian. Đổi chủ bãi / bãi / loại, hoặc áp lại chế tài đã gỡ → 409.
    /// </summary>
    public void Reapply(int ownerProfileId, int? parkingLotId, SanctionStateKind kind, DateTime startsAtUtc, DateTime? endsAtUtc,
        int appliedByUserId)
    {
        if (LiftedAtUtc is not null)
            throw new ConflictException($"Chế tài #{SanctionId} đã được gỡ, không thể áp lại.");
        if (OwnerProfileId != ownerProfileId || ParkingLotId != parkingLotId || Kind != kind)
            throw new ConflictException($"Chế tài #{SanctionId} đã được áp với chủ bãi / bãi / loại khác.");

        StartsAtUtc = startsAtUtc;
        EndsAtUtc = endsAtUtc;
        AppliedByUserId = appliedByUserId;
    }

    /// <summary>Gỡ (idempotent): gọi lại khi đã gỡ thì giữ mốc gỡ đầu tiên.</summary>
    public void Lift(int liftedByUserId, DateTime nowUtc)
    {
        if (LiftedAtUtc is not null) return;
        LiftedAtUtc = nowUtc;
        LiftedByUserId = liftedByUserId;
    }

    public bool IsInEffect(DateTime nowUtc) => InEffectAt(nowUtc).Compile()(this);

    /// <summary>Biểu thức hiệu lực dùng chung cho EF (dịch sang SQL) và kiểm tra in-memory.</summary>
    public static Expression<Func<SanctionState, bool>> InEffectAt(DateTime nowUtc)
        => s => s.LiftedAtUtc == null && s.StartsAtUtc <= nowUtc && (s.EndsAtUtc == null || s.EndsAtUtc > nowUtc);
}
