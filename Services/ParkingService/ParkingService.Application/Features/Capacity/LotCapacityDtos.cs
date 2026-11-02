using ParkingManagement.SharedKernel.Enums;

namespace ParkingService.Application.Features.Capacity;

/// <summary>
/// DTO thông tin chi tiết cấu hình sức chứa & vùng đệm khách vãng lai của bãi xe (UI-26).
/// </summary>
public sealed record LotCapacityConfigDto(
    int ParkingLotId,
    string ParkingLotName,
    int TotalSlots,
    IntegrationTier IntegrationTier,
    int OnlineQuota,
    int WalkInBufferPercent,
    int CalculatedWalkInBufferSlots,
    int MaxAllowedOnlineQuota,
    int OnlineLockThreshold,
    bool InstantBookingPaused,
    bool IsEmergencyStopped,
    string? EmergencyReason,
    DateTime? UpdatedAtUtc);

/// <summary>
/// Request DTO từ client gửi lên API cập nhật cấu hình sức chứa (UI-26).
/// </summary>
public sealed record UpdateLotCapacityConfigRequest(
    int? OwnerProfileId,
    int OnlineQuota,
    int WalkInBufferPercent,
    int OnlineLockThreshold,
    bool InstantBookingPaused = false);

/// <summary>
/// Command cập nhật cấu hình sức chứa & vùng đệm khách vãng lai (UI-26).
/// </summary>
public sealed record UpdateLotCapacityConfigCommand(
    int ParkingLotId,
    int? OwnerProfileId,
    int OnlineQuota,
    int WalkInBufferPercent,
    int OnlineLockThreshold,
    bool InstantBookingPaused = false);
