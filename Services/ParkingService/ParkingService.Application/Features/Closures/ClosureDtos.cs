using ParkingManagement.SharedKernel.Enums;

namespace ParkingService.Application.Features.Closures;

/// <summary>
/// Request DTO tạo lịch tạm đóng một phần hoặc toàn bộ bãi xe (US-063).
/// </summary>
public sealed record CreateClosureScheduleRequest(
    ClosureTargetType TargetType,
    int TargetId,
    DateTime StartsAtUtc,
    DateTime? EndsAtUtc,
    string Reason,
    bool IsEmergency = false);

/// <summary>
/// Command tạo lịch tạm đóng (US-063).
/// </summary>
public sealed record CreateClosureScheduleCommand(
    int ParkingLotId,
    ClosureTargetType TargetType,
    int TargetId,
    DateTime StartsAtUtc,
    DateTime? EndsAtUtc,
    string Reason,
    bool IsEmergency = false,
    int UserId = 0);

/// <summary>
/// DTO trả về chi tiết lịch tạm đóng (US-063).
/// </summary>
public sealed record ClosureScheduleDto(
    int Id,
    int ParkingLotId,
    string TargetType,
    int TargetId,
    string TargetName,
    DateTime StartsAtUtc,
    DateTime? EndsAtUtc,
    string Reason,
    bool IsEmergency,
    bool AffectedBookingsNotified,
    int CreatedByUserId,
    DateTime? CancelledAtUtc,
    string Status,
    int AffectedSlotsCount,
    DateTime CreatedAtUtc);

/// <summary>
/// Request DTO kiểm tra trước mức độ ảnh hưởng của lịch đóng (US-063).
/// </summary>
public sealed record ClosureImpactCheckRequest(
    ClosureTargetType TargetType,
    int TargetId,
    DateTime StartsAtUtc,
    DateTime? EndsAtUtc,
    bool IsEmergency = false);

/// <summary>
/// Command kiểm tra trước mức độ ảnh hưởng (US-063).
/// </summary>
public sealed record ClosureImpactCheckCommand(
    int ParkingLotId,
    ClosureTargetType TargetType,
    int TargetId,
    DateTime StartsAtUtc,
    DateTime? EndsAtUtc,
    bool IsEmergency = false);

/// <summary>
/// DTO kết quả kiểm tra trước tác động an toàn của lịch đóng (US-063).
/// </summary>
public sealed record ClosureImpactCheckResultDto(
    bool CanSafelyClose,
    int TotalSlotsInTarget,
    int AvailableSlotsCount,
    int OccupiedSlotsCount,
    int ReservedSlotsCount,
    IReadOnlyList<string> AtRiskSlotCodes,
    IReadOnlyList<string> Blockers,
    IReadOnlyList<string> Warnings);

/// <summary>
/// Command hủy lịch tạm đóng / mở lại trước hạn (US-063).
/// </summary>
public sealed record CancelClosureScheduleCommand(
    int ParkingLotId,
    int ClosureScheduleId,
    string? Reason = null,
    int UserId = 0);
