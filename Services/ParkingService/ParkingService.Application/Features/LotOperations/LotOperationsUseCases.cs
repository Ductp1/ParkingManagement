using ParkingManagement.SharedKernel.Contracts;
using ParkingManagement.SharedKernel.Enums;
using ParkingManagement.SharedKernel.Exceptions;
using ParkingService.Domain.Entities;

namespace ParkingService.Application.Features.LotOperations;

// =====================================================================================
// US-068: RecordHeartbeatUseCase – thiết bị/bãi gọi POST /parking-lots/{id}/heartbeat
// =====================================================================================

public interface IRecordHeartbeatUseCase
{
    Task<HeartbeatResult> ExecuteAsync(int lotId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Ghi nhận heartbeat: cập nhật timestamp, tự phục hồi nếu bãi đang stale
/// (phát LotDataRecovered qua publisher khi có — tham số optional để giữ ctor tối giản).
/// </summary>
public sealed class RecordHeartbeatUseCase(
    ILotOperationsRepository repository,
    TimeProvider timeProvider,
    ILotEventPublisher? eventPublisher = null) : IRecordHeartbeatUseCase
{
    public async Task<HeartbeatResult> ExecuteAsync(int lotId, CancellationToken cancellationToken = default)
    {
        var lot = await repository.GetLotAsync(lotId, cancellationToken)
            ?? throw new NotFoundException("Bãi đỗ xe", lotId);

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var recovered = lot.RecordHeartbeat(now);
        if (recovered)
            eventPublisher?.Enqueue(new LotDataRecovered(lot.Id, lot.Name, lot.OwnerProfileId, now));

        await repository.SaveChangesAsync(cancellationToken);
        return new HeartbeatResult(now, recovered);
    }
}

// =====================================================================================
// US-067: GetOperationsOverviewUseCase – GET /parking-lots/{id}/dashboard
// =====================================================================================

public interface IGetOperationsOverviewUseCase
{
    Task<OperationsOverviewDto> ExecuteAsync(int lotId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Tổng quan vận hành: slot counts + booking counts + emergency state + freshness (stale).
/// BookingService chết → booking counts về 0, phần còn lại vẫn hiển thị (degrade an toàn).
/// </summary>
public sealed class GetOperationsOverviewUseCase(
    ILotOperationsRepository repository,
    ILotBookingLookup bookingLookup,
    StalenessOptions options) : IGetOperationsOverviewUseCase
{
    public async Task<OperationsOverviewDto> ExecuteAsync(int lotId, CancellationToken cancellationToken = default)
    {
        var lot = await repository.GetLotAsync(lotId, cancellationToken)
            ?? throw new NotFoundException("Bãi đỗ xe", lotId);

        var slotBreakdown = await repository.CountSlotsByStateAsync(lotId, cancellationToken);

        int activeBookings;
        int upcomingBookings;
        try
        {
            (activeBookings, upcomingBookings) = await bookingLookup.GetBookingCountsAsync(lotId, cancellationToken);
        }
        catch
        {
            // BookingService không phản hồi: dashboard vẫn chạy, ô booking hiện 0.
            activeBookings = 0;
            upcomingBookings = 0;
        }

        return new OperationsOverviewDto(
            ParkingLotId: lot.Id,
            Name: lot.Name,
            Status: lot.Status.ToString(),
            EmergencyStopped: lot.Status == ParkingLotStatus.Suspended,
            EmergencyStopReason: lot.EmergencyStopReason,
            EmergencyStoppedAtUtc: lot.EmergencyStoppedAtUtc,
            TotalSlots: lot.TotalSlots,
            OccupiedSlots: Math.Max(0, lot.TotalSlots - lot.AvailableSlots),
            AvailableSlots: lot.AvailableSlots,
            ActiveBookings: activeBookings,
            UpcomingBookings: upcomingBookings,
            IsDataStale: lot.IsDataStale,
            LastHeartbeatAtUtc: lot.LastHeartbeatAtUtc,
            StaleThresholdMinutes: options.StaleAfterMinutes,
            SlotBreakdownJson: System.Text.Json.JsonSerializer.Serialize(slotBreakdown));
    }
}

// =====================================================================================
// US-069: EmergencyStopUseCase – POST /parking-lots/{id}/emergency-stop
// =====================================================================================

public interface IEmergencyStopUseCase
{
    Task<EmergencyStopResult> ExecuteAsync(int lotId, int actorUserId, string actorRole, string reason,
        bool confirm, CancellationToken cancellationToken = default);
}

/// <summary>
/// Đóng bãi khẩn cấp: bắt buộc confirm=true + lý do ≥ 5 ký tự; idempotent với bãi đã dừng.
/// Ghi audit EmergencyStopLog (Action=Stop) + phát LotEmergencyStopped kèm danh sách booking
/// bị ảnh hưởng → NotificationService (US-086) fan-out đa kênh cho từng khách.
/// Lot + log + outbox flush trong MỘT SaveChanges (atomic).
/// </summary>
public sealed class EmergencyStopUseCase(
    ILotOperationsRepository repository,
    ILotBookingLookup bookingLookup,
    ILotEventPublisher eventPublisher,
    TimeProvider timeProvider) : IEmergencyStopUseCase
{
    public async Task<EmergencyStopResult> ExecuteAsync(int lotId, int actorUserId, string actorRole, string reason,
        bool confirm, CancellationToken cancellationToken = default)
    {
        if (!confirm)
            throw new ValidationException("Đóng bãi khẩn cấp phải được xác nhận rõ ràng (confirm=true).");
        if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length < 5)
            throw new ValidationException("Lý do đóng bãi khẩn cấp phải có ít nhất 5 ký tự để phục vụ điều tra.");

        var lot = await repository.GetLotAsync(lotId, cancellationToken)
            ?? throw new NotFoundException("Bãi đỗ xe", lotId);

        if (lot.Status == ParkingLotStatus.Suspended)
            return new EmergencyStopResult(lot.Status.ToString(), 0, "already-stopped");

        IReadOnlyList<EmergencyAffectedBooking> affected;
        try
        {
            affected = await bookingLookup.GetAffectedBookingsAsync(lotId, cancellationToken);
        }
        catch
        {
            // BookingService chết: vẫn đóng bãi (an toàn trên hết) — fan-out thiếu danh sách, log ở monitoring.
            affected = [];
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        lot.ApplyEmergencyStop(reason, now);

        await repository.AddEmergencyLogAsync(new EmergencyStopLog
        {
            ParkingLotId = lot.Id,
            Action = "Stop",
            ActorUserId = actorUserId,
            ActorRole = actorRole,
            Reason = reason.Trim(),
            AffectedBookings = affected.Count,
        }, cancellationToken);

        var evt = new LotEmergencyStopped(
            lot.Id, lot.Name, lot.OwnerProfileId, actorUserId, reason.Trim(), now, affected);
        eventPublisher.Enqueue(evt);

        await repository.SaveChangesAsync(cancellationToken);

        return new EmergencyStopResult(lot.Status.ToString(), affected.Count, $"stop:{now:O}", evt.EventId);
    }
}

// =====================================================================================
// US-069: LiftEmergencyStopUseCase – POST /parking-lots/{id}/emergency-resume
// =====================================================================================

public interface ILiftEmergencyStopUseCase
{
    Task<LiftEmergencyStopResult> ExecuteAsync(int lotId, int actorUserId, string actorRole, string reason,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Gỡ đóng bãi khẩn cấp: bắt buộc lý do; no-op khi bãi không Suspended.
/// Ghi audit (Action=Lift) + phát LotEmergencyLifted → NotificationService báo chủ bãi/khách.
/// </summary>
public sealed class LiftEmergencyStopUseCase(
    ILotOperationsRepository repository,
    ILotEventPublisher eventPublisher,
    TimeProvider timeProvider) : ILiftEmergencyStopUseCase
{
    public async Task<LiftEmergencyStopResult> ExecuteAsync(int lotId, int actorUserId, string actorRole, string reason,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new ValidationException("Bắt buộc ghi rõ lý do gỡ đóng bãi khẩn cấp.");

        var lot = await repository.GetLotAsync(lotId, cancellationToken)
            ?? throw new NotFoundException("Bãi đỗ xe", lotId);

        if (lot.Status != ParkingLotStatus.Suspended)
            return new LiftEmergencyStopResult(lot.Status.ToString(), "not-suspended");

        var now = timeProvider.GetUtcNow().UtcDateTime;
        lot.LiftEmergencyStop(now);

        await repository.AddEmergencyLogAsync(new EmergencyStopLog
        {
            ParkingLotId = lot.Id,
            Action = "Lift",
            ActorUserId = actorUserId,
            ActorRole = actorRole,
            Reason = reason.Trim(),
            AffectedBookings = 0,
        }, cancellationToken);

        var evt = new LotEmergencyLifted(
            lot.Id, lot.Name, lot.OwnerProfileId, actorUserId, reason.Trim(), now);
        eventPublisher.Enqueue(evt);

        await repository.SaveChangesAsync(cancellationToken);
        return new LiftEmergencyStopResult(lot.Status.ToString(), $"lift:{now:O}", evt.EventId);
    }
}

// =====================================================================================
// US-069: GetEmergencyLogsUseCase – GET /parking-lots/{id}/emergency-logs (audit history)
// =====================================================================================

public interface IGetEmergencyLogsUseCase
{
    Task<IReadOnlyList<EmergencyStopLogDto>> ExecuteAsync(int lotId, int take = 20, CancellationToken cancellationToken = default);
}

public sealed class GetEmergencyLogsUseCase(ILotOperationsRepository repository) : IGetEmergencyLogsUseCase
{
    public async Task<IReadOnlyList<EmergencyStopLogDto>> ExecuteAsync(int lotId, int take = 20, CancellationToken cancellationToken = default)
    {
        var logs = await repository.GetEmergencyLogsAsync(lotId, Math.Clamp(take, 1, 100), cancellationToken);
        return logs.Select(l => new EmergencyStopLogDto(
            l.Id, l.ParkingLotId, l.Action, l.ActorUserId, l.ActorRole, l.Reason, l.AffectedBookings, l.CreatedAtUtc)).ToList();
    }
}