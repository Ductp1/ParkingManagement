namespace ParkingService.Application.Features.OperatingHours;

/// <summary>
/// DTO thông tin lịch hoạt động của 1 ngày trong tuần (US-062).
/// </summary>
public sealed record LotOperatingHourItemDto(
    DayOfWeek DayOfWeek,
    int DayOfWeekNumber,
    string DayName,
    TimeOnly OpenTime,
    TimeOnly CloseTime,
    bool IsClosed,
    bool IsOvernight,
    bool Is24Hours,
    bool IsDefault,
    bool IsOverride = false,
    string? Reason = null);

/// <summary>
/// DTO lịch hoạt động cả tuần (7 ngày) của bãi đỗ xe (US-062).
/// </summary>
public sealed record LotOperatingHoursWeeklyDto(
    int ParkingLotId,
    string ParkingLotName,
    TimeOnly DefaultOpenTime,
    TimeOnly DefaultCloseTime,
    IReadOnlyList<LotOperatingHourItemDto> Schedule,
    int? Year = null,
    int? WeekOfYear = null,
    bool IsCustomWeek = false,
    DateOnly? WeekStartDate = null,
    DateOnly? WeekEndDate = null);

/// <summary>
/// DTO cấu hình cho 1 ngày khi gửi request cập nhật.
/// </summary>
public sealed record UpdateLotOperatingHourItemRequest(
    DayOfWeek DayOfWeek,
    TimeOnly OpenTime,
    TimeOnly CloseTime,
    bool IsClosed = false);

/// <summary>
/// Request từ Client cập nhật lịch hoạt động cho bãi đỗ xe (US-062).
/// </summary>
public sealed record UpdateLotOperatingHoursRequest(
    int? OwnerProfileId,
    List<UpdateLotOperatingHourItemRequest> Schedule,
    int? Year = null,
    int? WeekOfYear = null,
    string? Reason = null);

/// <summary>
/// Command cập nhật lịch hoạt động cả tuần cho bãi đỗ xe (US-062).
/// </summary>
public sealed record UpdateLotOperatingHoursCommand(
    int ParkingLotId,
    int? OwnerProfileId,
    List<UpdateLotOperatingHourItemRequest> Schedule,
    int? Year = null,
    int? WeekOfYear = null,
    string? Reason = null);
