namespace ParkingService.Application.Features.LotOperations;

/// <summary>US-068: kết quả ghi nhận heartbeat.</summary>
public sealed record HeartbeatResult(DateTime ReceivedAtUtc, bool RecoveredFromStale);

/// <summary>US-069: kết quả đóng bãi khẩn cấp (AuditLogId = "already-stopped" khi bãi đã dừng từ trước;
/// EventId để UI tra cứu tracking trạng thái gửi đa kênh US-086 qua GET /notifications/delivery-status).</summary>
public sealed record EmergencyStopResult(string Status, int AffectedBookings, string? AuditLogId, Guid? EventId = null);

/// <summary>US-069: kết quả gỡ đóng bãi (AuditLogId = "not-suspended" khi bãi không đóng).</summary>
public sealed record LiftEmergencyStopResult(string Status, string? AuditLogId, Guid? EventId = null);

/// <summary>US-067: dòng nhật ký Emergency Stop cho dashboard.</summary>
public sealed record EmergencyStopLogDto(
    int Id, int ParkingLotId, string Action, int ActorUserId, string ActorRole,
    string Reason, int AffectedBookings, DateTime CreatedAtUtc);

/// <summary>
/// US-067: tổng quan vận hành real-time của 1 bãi cho dashboard chủ bãi.
/// Ghép: occupancy (Slots + ParkingLot), booking counts (BookingService) và độ tươi dữ liệu (heartbeat US-068).
/// </summary>
public sealed record OperationsOverviewDto(
    int ParkingLotId,
    string Name,
    string Status,
    bool EmergencyStopped,
    string? EmergencyStopReason,
    DateTime? EmergencyStoppedAtUtc,
    int TotalSlots,
    int OccupiedSlots,
    int AvailableSlots,
    int ActiveBookings,
    int UpcomingBookings,
    bool IsDataStale,
    DateTime? LastHeartbeatAtUtc,
    int StaleThresholdMinutes,
    string SlotBreakdownJson);