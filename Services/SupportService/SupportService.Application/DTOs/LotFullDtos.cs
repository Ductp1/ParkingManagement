namespace SupportService.Application.DTOs;

// ===== US-091: Khách đến nơi nhưng bãi hết chỗ =====

/// <summary>AC1: tài xế bấm "Báo sự cố" cho booking đã Confirmed. UserId lấy từ JWT.</summary>
public sealed record ReportLotFullRequestDto(string BookingCode, string? Note = null);

/// <summary>AC2: tài xế chọn 1 bãi trong danh sách gợi ý.</summary>
public sealed record AcceptTransferRequestDto(int ParkingLotId);

/// <summary>1 bãi thay thế trong bán kính 1 km (lưu cùng sự cố để tài xế chỉ chọn được bãi đã gợi ý).</summary>
public sealed record AlternativeLotDto(int ParkingLotId, string Name, string Address, double DistanceKm, int AvailableSlots, bool IsOpenNow);

public sealed record LotFullIncidentDto(
    string Code,
    string Status,
    string Priority,
    string BookingCode,
    int ParkingLotId,
    decimal PaidAmount,
    string? Note,
    int? LotAvailableSlotsAtReport,
    IReadOnlyList<AlternativeLotDto> Alternatives,
    int? NewParkingLotId,
    string? NewParkingLotName,
    int? RefundPercent,
    decimal? VoucherAmount,
    int? VoucherValidDays,
    bool IsSlaViolation,
    DateTime ReportedAtUtc,
    DateTime? DecidedAtUtc);
