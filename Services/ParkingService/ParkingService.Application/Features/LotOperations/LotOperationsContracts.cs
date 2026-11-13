using ParkingManagement.SharedKernel.Contracts;
using ParkingService.Domain.Entities;

namespace ParkingService.Application.Features.LotOperations;

/// <summary>
/// US-067/068/069: cổng persistence cho Lot Operations (EF Core implement ở Infrastructure).
/// GhiLot + EmergencyStopLog + Outbox nằm trong MỘT ParkingDbContext scope → 1 SaveChanges atomic.
/// </summary>
public interface ILotOperationsRepository
{
    Task<ParkingLot?> GetLotAsync(int lotId, CancellationToken cancellationToken = default);

    /// <summary>US-067: đếm số slot theo State (VD {"Available":42,"Occupied":50}) — join Slot→Floor→Zone.</summary>
    Task<Dictionary<string, int>> CountSlotsByStateAsync(int lotId, CancellationToken cancellationToken = default);

    Task AddEmergencyLogAsync(EmergencyStopLog log, CancellationToken cancellationToken = default);

    Task<List<EmergencyStopLog>> GetEmergencyLogsAsync(int lotId, int take, CancellationToken cancellationToken = default);

    /// <summary>US-068: danh sách bãi đang Active (worker stale chỉ quét bãi này).</summary>
    Task<List<ParkingLot>> GetActiveLotsAsync(CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// US-069: hàng đợi sự kiện tích hợp. Production ghi vào OutboxMessages (cùng scope với repository),
/// OutboxDispatcher của ServiceDefaults đọc và đẩy RabbitMQ at-least-once.
/// </summary>
public interface ILotEventPublisher
{
    void Enqueue(IntegrationEvent integrationEvent);
}

/// <summary>
/// US-067/069: tra cứu booking của bãi từ BookingService qua HTTP nội bộ.
/// Caller phải tự degrade (dashboard về 0, emergency stop vẫn chạy) khi BookingService chết.
/// </summary>
public interface ILotBookingLookup
{
    /// <summary>Booking bị ảnh hưởng khi đóng bãi khẩn cấp (chưa check-in, chưa kết thúc).</summary>
    Task<IReadOnlyList<EmergencyAffectedBooking>> GetAffectedBookingsAsync(int lotId, CancellationToken cancellationToken = default);

    /// <summary>(Active = đang trong bãi, Upcoming = chưa check-in) cho dashboard.</summary>
    Task<(int Active, int Upcoming)> GetBookingCountsAsync(int lotId, CancellationToken cancellationToken = default);
}

/// <summary>Cấu hình heartbeat/stale (appsettings → "Staleness").</summary>
public sealed class StalenessOptions
{
    public const string SectionName = "Staleness";

    /// <summary>Bãi quá X phút không nhận heartbeat → đánh dấu stale (US-068, mặc định 3 phút).</summary>
    public int StaleAfterMinutes { get; set; } = 3;

    /// <summary>Worker quét chu kỳ bao nhiêu giây (mặc định 30s).</summary>
    public int PollSeconds { get; set; } = 30;
}

/// <summary>US-068: logic thuần xác định bãi có cần chuyển sang stale (pure, dễ unit-test).</summary>
public static class StalenessEvaluator
{
    /// <summary>
    /// Chưa stale VÀ (chưa từng có heartbeat HOẶC khoảng cách heartbeat > ngưỡng) → cần chuyển stale.
    /// Đã stale thì không transition lần 2 (worker chỉ phát LotDataMarkedStale đúng 1 lần).
    /// </summary>
    public static bool NeedsTransitionToStale(DateTime? lastHeartbeatAtUtc, bool isStale, DateTime nowUtc, TimeSpan staleAfter)
        => !isStale && (lastHeartbeatAtUtc is null || nowUtc - lastHeartbeatAtUtc.Value > staleAfter);
}

/// <summary>
/// US-069: trạng thái vận hành rút gọn cho service nội bộ — BookingService pull trước khi tạo
/// booking, GateService pull trước khi cho xe vào (fail-open khi ParkingService chết — caller xử lý).
/// </summary>
public interface IGetOperationalStatusUseCase
{
    /// <summary>null khi bãi không tồn tại (caller tự quyết định hành vi).</summary>
    Task<LotOperationalStatusDto?> ExecuteAsync(int lotId, CancellationToken cancellationToken = default);
}