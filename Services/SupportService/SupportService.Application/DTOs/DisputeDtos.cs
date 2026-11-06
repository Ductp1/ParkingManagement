using SupportService.Domain.Rules;

namespace SupportService.Application.DTOs;

/// <summary>Body của POST /api/v1/complaints/{code}/rulings – chủ bãi phán quyết tranh chấp (US-090 AC2).</summary>
/// <param name="RefundAmount">Bắt buộc khi ForceRefund; không vượt số tiền tài xế đã trả.</param>
/// <param name="NewSlotCode">Bắt buộc khi MoveToSlot (chỉ với khiếu nại SlotOccupied): chỗ trống mới, khác chỗ đã đặt.</param>
/// <param name="ViolatorPlate">Khiếu nại SlotOccupied: biển số xe đậu sai chỗ → lập biên bản phạt theo mức phạt của bãi.</param>
public sealed record DisputeRulingRequestDto(DisputeDecision Decision, string Reason, decimal? RefundAmount = null,
    string? NewSlotCode = null, string? ViolatorPlate = null);

/// <summary>Body của POST /api/v1/complaints/{code}/dispute-messages – chủ bãi nhắn cho tài xế trong lúc xử lý.</summary>
public sealed record DisputeMessageRequestDto(string Message, IReadOnlyList<string>? AttachmentUrls = null);

/// <summary>Body của PUT /api/v1/complaints/fine-rates/{parkingLotId} – chủ bãi đặt mức phạt đậu sai chỗ.</summary>
public sealed record LotFineRateRequestDto(decimal WrongSlotFine);

public sealed record LotFineRateDto(int ParkingLotId, int OwnerProfileId, decimal WrongSlotFine, DateTime UpdatedAtUtc);

/// <summary>Biên bản xe đậu sai chỗ.</summary>
public sealed record ParkingViolationDto(string Code, string ComplaintCode, int ParkingLotId, string PlateNumber, string? SlotCode,
    decimal FineAmount, DateTime IssuedAtUtc);

/// <summary>AC3: SLA xử lý của chủ bãi.</summary>
public sealed record DisputeSlaDto(
    DateTime StartedAtUtc,
    DateTime FirstResponseDueAtUtc,
    DateTime? FirstRespondedAtUtc,
    bool FirstResponseBreached,
    DateTime ResolutionTargetAtUtc,
    DateTime ResolutionDueAtUtc,
    DateTime? ResolvedAtUtc,
    bool ResolutionBreached);

/// <summary>Phán quyết của chủ bãi (không sửa được).</summary>
public sealed record DisputeRulingDto(string Decision, string Outcome, decimal? RefundAmount, string? NewSlotCode, string Reason,
    int DecidedByUserId, DateTime DecidedAtUtc);

/// <summary>Tin nhắn trong hồ sơ, gồm tin nhắn hệ thống (không gồm ghi chú nội bộ).</summary>
public sealed record DisputeMessageDto(int Id, string SenderParty, int? SenderUserId, string Message,
    IReadOnlyList<string> AttachmentUrls, DateTime CreatedAtUtc);

/// <summary>AC1: lịch sử booking từ BookingService.</summary>
public sealed record BookingStatusChangeDto(string? FromStatus, string ToStatus, int? ChangedByUserId, string? Reason, DateTime AtUtc);

public sealed record BookingEvidenceDto(int Id, string Code, string Status, string PlateNumber, string ParkingLotName, string? SlotCode,
    DateTime StartAtUtc, DateTime EndAtUtc, decimal TotalAmount, decimal PaidAmount, IReadOnlyList<BookingStatusChangeDto> History);

/// <summary>AC1: log check-in/out + cách nhận diện ở cổng (camera LPR / quét QR / nhập tay, độ tin cậy OCR) từ GateService.</summary>
public sealed record GateSessionEvidenceDto(string Code, string Status, string PlateDisplay, DateTime EntryAtUtc, DateTime? ExitAtUtc,
    int MinutesParked, string CheckInMethod, double? EntryOcrConfidence, decimal? Fee);

/// <summary>
/// AC1: hồ sơ tranh chấp đầy đủ cho chủ bãi. <see cref="CanRule"/> = chủ bãi còn phán quyết được không.
/// Nguồn ngoài (BookingService, GateService) không phản hồi thì hồ sơ vẫn trả về, tên nguồn nằm trong <see cref="UnavailableSources"/>.
/// </summary>
public sealed record DisputeCaseFileDto(
    int Id,
    string Code,
    string Status,
    string Category,
    string Priority,
    int UserId,
    int ParkingLotId,
    int? OwnerProfileId,
    string? BookingCode,
    string Description,
    IReadOnlyList<string> EvidenceUrls,
    decimal? RequestedAmount,
    string? OwnerResponse,
    DateTime? OwnerResponseDueAtUtc,
    DateTime? OwnerRespondedAtUtc,
    bool PayoutHeld,
    bool CanRule,
    DisputeSlaDto Sla,
    IReadOnlyList<DisputeMessageDto> Timeline,
    DisputeRulingDto? Ruling,
    IReadOnlyList<ParkingViolationDto> Violations,
    BookingEvidenceDto? Booking,
    IReadOnlyList<GateSessionEvidenceDto> GateSessions,
    IReadOnlyList<string> UnavailableSources,
    DateTime CreatedAtUtc);

/// <summary>Một dòng trong hàng chờ tranh chấp của chủ bãi (AC3).</summary>
public sealed record DisputeQueueItemDto(string Code, string Status, string Category, string Priority, int ParkingLotId,
    string? BookingCode, decimal? RequestedAmount, DateTime CreatedAtUtc, DisputeSlaDto Sla);

/// <summary>AC2: tiền lệ – phán quyết đã có, không kèm thông tin cá nhân.</summary>
public sealed record DisputePrecedentDto(string ComplaintCode, string Category, string Decision, decimal? RefundAmount, string Reason,
    DateTime DecidedAtUtc);
