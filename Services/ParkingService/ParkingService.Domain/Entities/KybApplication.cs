using ParkingManagement.SharedKernel.Domain;
using ParkingManagement.SharedKernel.Enums;
using ParkingManagement.SharedKernel.Exceptions;

namespace ParkingService.Domain.Entities;

/// <summary>[ParkingService] Hồ sơ thẩm định KYB 4 bước (Nghiệp vụ v3 §3.4). UC-26, UC-39. Module: TV5.</summary>
public class KybApplication : BaseEntity
{
    /// <summary>Bãi trên ngưỡng này là Tier 1: bắt buộc khảo sát thực địa trước khi duyệt (US-054, US-055 AC4).</summary>
    public const int FieldSurveySlotThreshold = 50;

    /// <summary>
    /// Bảng chuyển trạng thái hợp lệ. Phía chủ bãi (US-054) nộp/nháp; phía Admin (US-055) duyệt.
    /// Mọi thao tác Admin đi qua <see cref="MoveTo"/> – một chỗ kiểm tra duy nhất, sai → 409.
    /// </summary>
    private static readonly IReadOnlyDictionary<KybStatus, KybStatus[]> Transitions = new Dictionary<KybStatus, KybStatus[]>
    {
        [KybStatus.Draft] = [KybStatus.Submitted],
        [KybStatus.Submitted] = [KybStatus.UnderReview],
        [KybStatus.UnderReview] = [KybStatus.Approved, KybStatus.Rejected, KybStatus.NeedsMoreInfo],
        [KybStatus.NeedsMoreInfo] = [KybStatus.Draft, KybStatus.Submitted],
        [KybStatus.Rejected] = [KybStatus.Draft, KybStatus.Submitted],
        [KybStatus.Approved] = [],
    };

    public int ParkingLotId { get; set; }
    public KybStatus Status { get; set; } = KybStatus.Draft;

    // Bước 1 – Pháp lý
    public string? BusinessLicenseUrl { get; set; }
    // Bước 2 – Địa điểm (ảnh thực tế + GPS EXIF)
    public string? SitePhotoUrlsJson { get; set; }
    public double? PhotoLatitude { get; set; }
    public double? PhotoLongitude { get; set; }
    // Bước 3 – PCCC
    public string? FireSafetyCertificateUrl { get; set; }
    // Bước 4 – Khảo sát thực địa (bắt buộc với bãi > 50 chỗ)
    public bool FieldSurveyRequired { get; set; }
    public DateTime? FieldSurveyAtUtc { get; set; }
    public string? FieldSurveyNote { get; set; }
    /// <summary>Admin xác nhận đã khảo sát thực địa (US-055 AC4). Null = chưa khảo sát.</summary>
    public DateTime? FieldSurveyCompletedAtUtc { get; set; }

    public DateTime? SubmittedAtUtc { get; set; }
    /// <summary>Admin đang/đã thẩm định (gán khi bắt đầu xem xét, cập nhật khi quyết định).</summary>
    public int? ReviewedByUserId { get; set; }
    public DateTime? ReviewedAtUtc { get; set; }
    public string? RejectReason { get; set; }
    /// <summary>Ghi chú của Admin: nội dung yêu cầu bổ sung (NeedsMoreInfo) hoặc ghi chú khi duyệt.</summary>
    public string? ReviewNote { get; set; }

    /// <summary>Optimistic concurrency (ServiceDbContext đóng dấu mỗi lần lưu): hai Admin thao tác cùng lúc → người sau nhận 409.</summary>
    public byte[] RowVersion { get; set; } = [];

    public ParkingLot ParkingLot { get; set; } = null!;

    public static bool IsFieldSurveyMandatory(int totalSlots) => totalSlots > FieldSurveySlotThreshold;

    public static bool CanMove(KybStatus from, KybStatus to) => Transitions.TryGetValue(from, out var targets) && targets.Contains(to);

    // ===================== Thao tác Admin (US-055) =====================

    /// <summary>Submitted → UnderReview; người thẩm định = Admin trong token.</summary>
    public void StartReview(int adminUserId)
    {
        EnsureAdmin(adminUserId);
        MoveTo(KybStatus.UnderReview);
        ReviewedByUserId = adminUserId;
        ReviewedAtUtc = null;
        ReviewNote = null;
    }

    /// <summary>Lên (hoặc dời) lịch khảo sát thực địa khi đang thẩm định; xóa kết quả khảo sát cũ.</summary>
    public void ScheduleFieldSurvey(DateTime surveyAtUtc, string? note, DateTime nowUtc)
    {
        EnsureStatus(KybStatus.UnderReview, "lên lịch khảo sát thực địa");
        if (surveyAtUtc <= nowUtc)
            throw new ValidationException("Thời gian khảo sát thực địa phải ở trong tương lai.");
        FieldSurveyRequired = true;
        FieldSurveyAtUtc = surveyAtUtc;
        FieldSurveyNote = SurveyNote(note, required: false);
        FieldSurveyCompletedAtUtc = null;
    }

    /// <summary>Ghi nhận đã khảo sát thực địa (sau giờ hẹn) kèm kết quả.</summary>
    public void RecordFieldSurveyResult(string note, DateTime nowUtc)
    {
        EnsureStatus(KybStatus.UnderReview, "ghi nhận kết quả khảo sát");
        if (FieldSurveyAtUtc is null)
            throw new ConflictException("Hồ sơ chưa có lịch khảo sát thực địa.");
        if (FieldSurveyAtUtc > nowUtc)
            throw new ConflictException("Chưa tới giờ khảo sát thực địa đã hẹn.");
        FieldSurveyNote = SurveyNote(note, required: true);
        FieldSurveyCompletedAtUtc = nowUtc;
    }

    /// <summary>
    /// UnderReview → Approved và bãi PendingApproval → Active (US-055 AC2).
    /// Bãi Tier 1 phải đã khảo sát thực địa (AC4); bãi phải có ít nhất 1 ô đỗ đang hoạt động (sơ đồ).
    /// </summary>
    public void Approve(int adminUserId, ParkingLot lot, int activeSlotCount, string? note, DateTime nowUtc)
    {
        EnsureAdmin(adminUserId);
        if (lot.Id != ParkingLotId) throw new ArgumentException("Bãi đỗ không khớp hồ sơ KYB.", nameof(lot));
        EnsureStatus(KybStatus.UnderReview, "duyệt");
        if (IsFieldSurveyMandatory(lot.TotalSlots) && FieldSurveyCompletedAtUtc is null)
            throw new ConflictException($"Bãi trên {FieldSurveySlotThreshold} chỗ phải được khảo sát thực địa trước khi duyệt.");
        if (activeSlotCount <= 0)
            throw new ConflictException("Bãi chưa có sơ đồ ô đỗ đang hoạt động, chưa thể duyệt.");

        lot.Activate();
        MoveTo(KybStatus.Approved);
        ReviewedByUserId = adminUserId;
        ReviewedAtUtc = nowUtc;
        ReviewNote = Normalize(note);
        RejectReason = null;
    }

    /// <summary>UnderReview → Rejected, lý do bắt buộc; bãi giữ PendingApproval, chủ bãi được nộp lại (US-054).</summary>
    public void Reject(int adminUserId, string reason, DateTime nowUtc)
    {
        EnsureAdmin(adminUserId);
        MoveTo(KybStatus.Rejected);
        RejectReason = Required(reason, "Lý do từ chối");
        ReviewedByUserId = adminUserId;
        ReviewedAtUtc = nowUtc;
    }

    /// <summary>UnderReview → NeedsMoreInfo (US-055 AC3), nội dung cần bổ sung bắt buộc.</summary>
    public void RequestMoreInfo(int adminUserId, string note, DateTime nowUtc)
    {
        EnsureAdmin(adminUserId);
        MoveTo(KybStatus.NeedsMoreInfo);
        ReviewNote = Required(note, "Nội dung cần bổ sung");
        ReviewedByUserId = adminUserId;
        ReviewedAtUtc = nowUtc;
    }

    private void MoveTo(KybStatus target)
    {
        if (!CanMove(Status, target))
            throw new ConflictException($"Không thể chuyển hồ sơ KYB từ trạng thái {Status} sang {target}.");
        Status = target;
    }

    private void EnsureStatus(KybStatus expected, string action)
    {
        if (Status != expected)
            throw new ConflictException($"Chỉ được {action} khi hồ sơ KYB ở trạng thái {expected} (hiện tại: {Status}).");
    }

    private static void EnsureAdmin(int adminUserId)
    {
        if (adminUserId <= 0) throw new ArgumentOutOfRangeException(nameof(adminUserId), "Admin không hợp lệ.");
    }

    private static string Required(string? value, string field)
    {
        var text = Normalize(value) ?? throw new ValidationException($"{field} không được để trống.");
        if (text.Length > 1000) throw new ValidationException($"{field} tối đa 1000 ký tự.");
        return text;
    }

    private static string? SurveyNote(string? value, bool required)
    {
        var text = Normalize(value);
        if (required && text is null) throw new ValidationException("Kết quả khảo sát thực địa không được để trống.");
        if (text is { Length: > 2000 }) throw new ValidationException("Ghi chú khảo sát tối đa 2000 ký tự.");
        return text;
    }

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
