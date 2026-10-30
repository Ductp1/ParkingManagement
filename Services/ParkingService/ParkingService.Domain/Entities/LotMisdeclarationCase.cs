using ParkingManagement.SharedKernel.Domain;
using ParkingManagement.SharedKernel.Enums;
using ParkingManagement.SharedKernel.Exceptions;
using ParkingService.Domain.Enums;

namespace ParkingService.Domain.Entities;

/// <summary>
/// [ParkingService] Case xử lý bãi khai báo sai sau khi đã Active (US-056, UC-39 E1). Sai sót nhẹ → cảnh báo + hạn sửa (AC3);
/// nghi khai sai địa chỉ / ảnh giả → tạm khóa bãi + yêu cầu giải trình 24h (AC1). Mỗi bãi tối đa 1 case đang mở.
/// ParkingService là nơi DUY NHẤT đổi ParkingLot.Status; chế tài cấp bãi (US-097) phải gọi endpoint của ParkingService.
/// </summary>
public class LotMisdeclarationCase : BaseEntity
{
    /// <summary>Hạn giải trình cố định khi tạm khóa (US-056 AC1).</summary>
    public static readonly TimeSpan ResponseWindow = TimeSpan.FromHours(24);
    public const int MinCorrectionDays = 1;
    public const int MaxCorrectionDays = 30;
    public const int TextMaxLength = 1000;

    /// <summary>Bảng chuyển trạng thái hợp lệ – mọi thao tác đi qua <see cref="MoveTo"/>, sai → 409.</summary>
    private static readonly IReadOnlyDictionary<MisdeclarationCaseStatus, MisdeclarationCaseStatus[]> Transitions =
        new Dictionary<MisdeclarationCaseStatus, MisdeclarationCaseStatus[]>
        {
            [MisdeclarationCaseStatus.Warned] = [MisdeclarationCaseStatus.Suspended, MisdeclarationCaseStatus.Resolved, MisdeclarationCaseStatus.Banned],
            [MisdeclarationCaseStatus.Suspended] = [MisdeclarationCaseStatus.Resolved, MisdeclarationCaseStatus.Banned],
            [MisdeclarationCaseStatus.Resolved] = [],
            [MisdeclarationCaseStatus.Banned] = [],
        };

    public int ParkingLotId { get; private set; }
    /// <summary>Chủ bãi tại thời điểm mở case (OwnerProfile bên UserService, không FK).</summary>
    public int OwnerProfileId { get; private set; }
    public MisdeclarationSeverity Severity { get; private set; }
    public MisdeclarationCategory Category { get; private set; }
    public MisdeclarationCaseStatus Status { get; private set; }
    /// <summary>Nội dung khai sai Admin phát hiện (gửi cho chủ bãi).</summary>
    public string Finding { get; private set; } = string.Empty;
    /// <summary>Hạn sửa (cảnh báo) hoặc hạn giải trình (tạm khóa). Quá hạn chỉ hiển thị, không tự xử lý.</summary>
    public DateTime DueAtUtc { get; private set; }
    public int OpenedByUserId { get; private set; }
    public DateTime OpenedAtUtc { get; private set; }
    /// <summary>Lý do chuyển cảnh báo → tạm khóa (null nếu mở thẳng ở mức tạm khóa).</summary>
    public string? EscalationNote { get; private set; }
    public DateTime? EscalatedAtUtc { get; private set; }
    public int? ClosedByUserId { get; private set; }
    public DateTime? ClosedAtUtc { get; private set; }
    /// <summary>Kết luận khi đóng case (đã sửa / giải trình hợp lệ, hoặc lý do cấm).</summary>
    public string? ResolutionNote { get; private set; }

    /// <summary>Optimistic concurrency: hai Admin thao tác cùng case → người sau nhận 409.</summary>
    public byte[] RowVersion { get; set; } = [];

    public ParkingLot ParkingLot { get; private set; } = null!;

    // Constructor rỗng cho EF Core.
    private LotMisdeclarationCase() { }

    public bool IsOpen => Status is MisdeclarationCaseStatus.Warned or MisdeclarationCaseStatus.Suspended;

    /// <summary>Case đang mở đã quá hạn sửa / giải trình (US-056: chỉ gắn cờ, Admin tự quyết định).</summary>
    public bool IsOverdue(DateTime nowUtc) => IsOpen && DueAtUtc < nowUtc;

    public static bool CanMove(MisdeclarationCaseStatus from, MisdeclarationCaseStatus to)
        => Transitions.TryGetValue(from, out var targets) && targets.Contains(to);

    /// <summary>AC3: sai sót nhẹ → cảnh báo, bãi vẫn Active, hạn sửa = now + correctionDays (1–30).</summary>
    public static LotMisdeclarationCase Warn(ParkingLot lot, MisdeclarationCategory category, string finding, int correctionDays,
        int adminUserId, DateTime nowUtc)
    {
        if (correctionDays is < MinCorrectionDays or > MaxCorrectionDays)
            throw new ValidationException($"Hạn sửa phải từ {MinCorrectionDays} đến {MaxCorrectionDays} ngày.");
        EnsureLotActive(lot);
        return Open(lot, MisdeclarationSeverity.Minor, MisdeclarationCaseStatus.Warned, category, finding,
            nowUtc.AddDays(correctionDays), adminUserId, nowUtc);
    }

    /// <summary>AC1: nghi khai sai địa chỉ / ảnh giả → bãi Active → Suspended, hạn giải trình 24h.</summary>
    public static LotMisdeclarationCase Suspend(ParkingLot lot, MisdeclarationCategory category, string finding, int adminUserId,
        DateTime nowUtc)
    {
        EnsureLotActive(lot);
        var created = Open(lot, MisdeclarationSeverity.Suspected, MisdeclarationCaseStatus.Suspended, category, finding,
            nowUtc + ResponseWindow, adminUserId, nowUtc);
        lot.SuspendListing();
        return created;
    }

    /// <summary>Warned → Suspended (thường khi quá hạn sửa): tạm khóa bãi, hạn giải trình 24h tính lại từ lúc chuyển.</summary>
    public void Escalate(int adminUserId, string note, DateTime nowUtc)
    {
        EnsureAdmin(adminUserId);
        EnsureLot();
        var text = Required(note, "Lý do tạm khóa");
        MoveTo(MisdeclarationCaseStatus.Suspended);
        ParkingLot.SuspendListing();
        Severity = MisdeclarationSeverity.Suspected;
        EscalationNote = text;
        EscalatedAtUtc = nowUtc;
        DueAtUtc = nowUtc + ResponseWindow;
    }

    /// <summary>
    /// Warned / Suspended → Resolved: chủ bãi đã sửa đúng / giải trình hợp lệ. Bãi đang bị tạm khóa → khôi phục Active.
    /// Trả true nếu bãi được khôi phục.
    /// </summary>
    public bool Resolve(int adminUserId, string note, DateTime nowUtc)
    {
        EnsureAdmin(adminUserId);
        EnsureLot();
        var text = Required(note, "Kết luận xử lý");
        var wasSuspended = Status == MisdeclarationCaseStatus.Suspended;
        MoveTo(MisdeclarationCaseStatus.Resolved);
        if (wasSuspended) ParkingLot.Reinstate();
        Close(adminUserId, text, nowUtc);
        return wasSuspended;
    }

    private static LotMisdeclarationCase Open(ParkingLot lot, MisdeclarationSeverity severity, MisdeclarationCaseStatus status,
        MisdeclarationCategory category, string finding, DateTime dueAtUtc, int adminUserId, DateTime nowUtc)
    {
        EnsureAdmin(adminUserId);
        if (!Enum.IsDefined(category)) throw new ValidationException("Nội dung khai sai không hợp lệ.");
        return new LotMisdeclarationCase
        {
            ParkingLotId = lot.Id,
            ParkingLot = lot,
            OwnerProfileId = lot.OwnerProfileId,
            Severity = severity,
            Category = category,
            Status = status,
            Finding = Required(finding, "Nội dung khai sai"),
            DueAtUtc = dueAtUtc,
            OpenedByUserId = adminUserId,
            OpenedAtUtc = nowUtc,
        };
    }

    private void Close(int adminUserId, string note, DateTime nowUtc)
    {
        ClosedByUserId = adminUserId;
        ClosedAtUtc = nowUtc;
        ResolutionNote = note;
    }

    private void MoveTo(MisdeclarationCaseStatus target)
    {
        if (!CanMove(Status, target))
            throw new ConflictException($"Không thể chuyển case khai báo sai từ trạng thái {Status} sang {target}.");
        Status = target;
    }

    private void EnsureLot()
    {
        if (ParkingLot is null || ParkingLot.Id != ParkingLotId)
            throw new InvalidOperationException("Case khai báo sai phải được nạp kèm bãi đỗ.");
    }

    private static void EnsureLotActive(ParkingLot lot)
    {
        if (lot.Status != ParkingLotStatus.Active)
            throw new ConflictException($"Chỉ xử lý khai báo sai cho bãi đang hoạt động (hiện tại: {lot.Status}). " +
                                        "Bãi đang thẩm định dùng từ chối / yêu cầu bổ sung KYB.");
    }

    private static void EnsureAdmin(int adminUserId)
    {
        if (adminUserId <= 0) throw new ArgumentOutOfRangeException(nameof(adminUserId), "Admin không hợp lệ.");
    }

    private static string Required(string? value, string field)
    {
        var text = string.IsNullOrWhiteSpace(value) ? throw new ValidationException($"{field} không được để trống.") : value.Trim();
        if (text.Length > TextMaxLength) throw new ValidationException($"{field} tối đa {TextMaxLength} ký tự.");
        return text;
    }
}
