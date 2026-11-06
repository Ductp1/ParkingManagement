using ParkingService.Domain.Entities;
using ParkingManagement.SharedKernel.Contracts;
using ParkingManagement.SharedKernel.Enums;
using ParkingManagement.SharedKernel.Exceptions;

namespace ParkingService.Application.Features.LotOperations;

// ===== DTOs =====
public sealed record LotOperationsOverviewDto(
    int LotId, string LotName, string Status, bool EmergencyStopped, string? EmergencyStopReason,
    int TotalSlots, int OccupiedSlots, int AvailableSlots, string? SlotBreakdownJson,
    int ActiveBookings, int UpcomingBookings,
    DateTime? LastHeartbeatAtUtc, bool IsDataStale, int StaleThresholdMinutes,
    DateTime? EmergencyStoppedAtUtc);

public sealed record HeartbeatResultDto(int LotId, DateTime ReceivedAtUtc, bool RecoveredFromStale);

public sealed record EmergencyStopResultDto(int LotId, string Status, int AffectedBookings, string AuditLogId);

/// <summary>Audit log emergency stop (US-069: ai, khi nào, lý do + hành động stop/lift).</summary>
public class EmergencyStopLog : ParkingManagement.SharedKernel.Domain.BaseEntity
{
    public int ParkingLotId { get; set; }
    /// <summary>"Stop" hoặc "Lift".</summary>
    public string Action { get; set; } = string.Empty;
    public int ActorUserId { get; set; }
    public string? ActorRole { get; set; }
    public string Reason { get; set; } = string.Empty;
    public int AffectedBookings { get; set; }
}

/// <summary>Port persistence cho LotOperations (Infrastructure EF implement).</summary>
public interface ILotOperationsRepository
{
    Task<ParkingLot?> GetLotAsync(int lotId, CancellationToken cancellationToken = default);
    /// <summary>Số slot theo từng trạng thái (key = SlotState, value = count).</summary>
    Task<Dictionary<string, int>> CountSlotsByStateAsync(int lotId, CancellationToken cancellationToken = default);
    Task AddEmergencyLogAsync(EmergencyStopLog log, CancellationToken cancellationToken = default);
    Task<List<EmergencyStopLog>> GetEmergencyLogsAsync(int lotId, int take, CancellationToken cancellationToken = default);
    Task<List<ParkingLot>> GetActiveLotsAsync(CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

/// <summary>US-069: tra cứu booking đang diễn ra/sắp tới của bãi (HTTP sang BookingService).</summary>
public interface ILotBookingLookup
{
    Task<IReadOnlyList<EmergencyAffectedBooking>> GetAffectedBookingsAsync(int lotId, CancellationToken cancellationToken = default);
    Task<(int Active, int Upcoming)> GetBookingCountsAsync(int lotId, CancellationToken cancellationToken = default);
}

/// <summary>US-068/069: phát integration event qua transactional outbox của ParkingDbContext.</summary>
public interface ILotEventPublisher
{
    void Enqueue(IntegrationEvent integrationEvent);
}

/// <summary>US-068: đánh giá stale cho 1 lot — pure function để test.</summary>
public interface IRecordHeartbeatUseCase
{
    Task<HeartbeatResultDto> ExecuteAsync(int lotId, CancellationToken cancellationToken = default);
}

/// <summary>US-068: thiết bị/controller gửi heartbeat → cập nhật LastHeartbeatAtUtc; phục hồi stale nếu cần.</summary>
public sealed class RecordHeartbeatUseCase(ILotOperationsRepository repository, TimeProvider timeProvider) : IRecordHeartbeatUseCase
{
    public async Task<HeartbeatResultDto> ExecuteAsync(int lotId, CancellationToken cancellationToken = default)
    {
        var lot = await repository.GetLotAsync(lotId, cancellationToken)
                  ?? throw new NotFoundException("ParkingLot", lotId);
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var recovered = lot.RecordHeartbeat(now);
        await repository.SaveChangesAsync(cancellationToken);
        return new HeartbeatResultDto(lotId, now, recovered);
    }
}

public interface IGetOperationsOverviewUseCase
{
    Task<LotOperationsOverviewDto> ExecuteAsync(int lotId, CancellationToken cancellationToken = default);
}

/// <summary>US-067/068: tổng quan vận hành — occupancy, booking, freshness. Owner chỉ xem được bãi của mình (controller check).</summary>
public sealed class GetOperationsOverviewUseCase(
    ILotOperationsRepository repository,
    ILotBookingLookup bookingLookup,
    StalenessOptions staleness) : IGetOperationsOverviewUseCase
{
    public async Task<LotOperationsOverviewDto> ExecuteAsync(int lotId, CancellationToken cancellationToken = default)
    {
        var lot = await repository.GetLotAsync(lotId, cancellationToken)
                  ?? throw new NotFoundException("ParkingLot", lotId);
        var byState = await repository.CountSlotsByStateAsync(lotId, cancellationToken);
        var (active, upcoming) = await bookingLookup.GetBookingCountsAsync(lotId, cancellationToken);

        return new LotOperationsOverviewDto(
            lot.Id, lot.Name, lot.Status.ToString(),
            lot.Status == ParkingLotStatus.Suspended, lot.EmergencyStopReason,
            lot.TotalSlots, lot.TotalSlots - lot.AvailableSlots, lot.AvailableSlots,
            System.Text.Json.JsonSerializer.Serialize(byState),
            active, upcoming,
            lot.LastHeartbeatAtUtc, lot.IsDataStale, Math.Max(1, staleness.StaleAfterMinutes),
            lot.EmergencyStoppedAtUtc);
    }
}

/// <summary>US-068: đánh giá stale cho 1 lot — pure function để test.</summary>
public static class StalenessEvaluator
{
    /// <summary>Trả true nếu lot quá hạn heartbeat và chưa ở trạng thái stale (cần chuyển trạng thái).</summary>
    public static bool NeedsTransitionToStale(DateTime? lastHeartbeatAtUtc, bool isStale, DateTime nowUtc, TimeSpan threshold)
    {
        if (isStale) return false;
        return lastHeartbeatAtUtc is null || nowUtc - lastHeartbeatAtUtc.Value > threshold;
    }
}

/// <summary>
/// Cấu hình US-067/068 (appsettings → "LotHeartbeat"). Nằm ở Application để Overview dùng chung
/// ngưỡng hiển thị với worker; Infrastructure bind từ configuration.
/// </summary>
public sealed class StalenessOptions
{
    public const string SectionName = "LotHeartbeat";
    /// <summary>Quá bao nhiêu phút không heartbeat thì coi là stale (mặc định 3).</summary>
    public int StaleAfterMinutes { get; set; } = 3;
    /// <summary>Chu kỳ đánh giá của worker (giây).</summary>
    public int PollSeconds { get; set; } = 30;
}

public interface IEmergencyStopUseCase
{
    Task<EmergencyStopResultDto> ExecuteAsync(int lotId, int actorUserId, string actorRole, string reason, bool confirm, CancellationToken cancellationToken = default);
}

/// <summary>
/// US-069: đóng bãi khẩn cấp. Yêu cầu confirm=true (chống bấm nhầm); chặn booking mới (BookingService
/// check lot status) + chặn check-in vãng lai (GateService cache qua event); fan-out US-086 qua event
/// LotEmergencyStopped kèm danh sách booking bị ảnh hưởng; ghi audit log EmergencyStopLog.
/// In-flight an toàn: xe đang trong bãi vẫn check-out bình thường (check-out không check lot status).
/// </summary>
public sealed class EmergencyStopUseCase(
    ILotOperationsRepository repository,
    ILotBookingLookup bookingLookup,
    ILotEventPublisher events,
    TimeProvider timeProvider) : IEmergencyStopUseCase
{
    public async Task<EmergencyStopResultDto> ExecuteAsync(int lotId, int actorUserId, string actorRole,
        string reason, bool confirm, CancellationToken cancellationToken = default)
    {
        if (!confirm) throw new ValidationException("Cần xác nhận (confirm=true) để đóng bãi khẩn cấp.");
        if (string.IsNullOrWhiteSpace(reason) || reason.Length < 5)
            throw new ValidationException("Lý do đóng bãi bắt buộc, tối thiểu 5 ký tự.");

        var lot = await repository.GetLotAsync(lotId, cancellationToken)
                  ?? throw new NotFoundException("ParkingLot", lotId);
        var now = timeProvider.GetUtcNow().UtcDateTime;

        // Idempotent: bãi đã đóng khẩn cấp từ trước → không log lại, không phát event (retry/an-đúp bấm an toàn).
        if (lot.Status == ParkingLotStatus.Suspended)
            return new EmergencyStopResultDto(lotId, lot.Status.ToString(), 0, "already-stopped");

        var affected = await bookingLookup.GetAffectedBookingsAsync(lotId, cancellationToken);
        lot.ApplyEmergencyStop(reason, now);

        var log = new EmergencyStopLog
        {
            ParkingLotId = lotId, Action = "Stop", ActorUserId = actorUserId, ActorRole = actorRole,
            Reason = reason, AffectedBookings = affected.Count,
        };
        await repository.AddEmergencyLogAsync(log, cancellationToken);

        events.Enqueue(new LotEmergencyStopped(lotId, lot.Name, lot.OwnerProfileId, actorUserId, reason, now, affected));
        await repository.SaveChangesAsync(cancellationToken);
        return new EmergencyStopResultDto(lotId, lot.Status.ToString(), affected.Count, log.Id.ToString());
    }
}

public interface ILiftEmergencyStopUseCase
{
    Task<EmergencyStopResultDto> ExecuteAsync(int lotId, int actorUserId, string actorRole, string reason, CancellationToken cancellationToken = default);
}

/// <summary>US-069: gỡ đóng bãi khẩn cấp — bãi Active trở lại, phát LotEmergencyLifted (GateService mở check-in).</summary>
public sealed class LiftEmergencyStopUseCase(
    ILotOperationsRepository repository,
    ILotEventPublisher events,
    TimeProvider timeProvider) : ILiftEmergencyStopUseCase
{
    public async Task<EmergencyStopResultDto> ExecuteAsync(int lotId, int actorUserId, string actorRole,
        string reason, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(reason)) throw new ValidationException("Lý do gỡ đóng bãi bắt buộc.");

        var lot = await repository.GetLotAsync(lotId, cancellationToken)
                  ?? throw new NotFoundException("ParkingLot", lotId);
        var now = timeProvider.GetUtcNow().UtcDateTime;

        // Idempotent: bãi không ở trạng thái Suspended → không log lại, không phát event.
        if (lot.Status != ParkingLotStatus.Suspended)
            return new EmergencyStopResultDto(lotId, lot.Status.ToString(), 0, "not-suspended");

        lot.LiftEmergencyStop(now);

        var log = new EmergencyStopLog
        {
            ParkingLotId = lotId, Action = "Lift", ActorUserId = actorUserId, ActorRole = actorRole,
            Reason = reason, AffectedBookings = 0,
        };
        await repository.AddEmergencyLogAsync(log, cancellationToken);
        events.Enqueue(new LotEmergencyLifted(lotId, lot.Name, lot.OwnerProfileId, actorUserId, reason, now));
        await repository.SaveChangesAsync(cancellationToken);
        return new EmergencyStopResultDto(lotId, lot.Status.ToString(), 0, log.Id.ToString());
    }
}
