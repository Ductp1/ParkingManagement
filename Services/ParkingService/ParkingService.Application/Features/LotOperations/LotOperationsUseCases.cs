using ParkingService.Domain.Entities;
using ParkingManagement.SharedKernel.Contracts;
using ParkingManagement.SharedKernel.Exceptions;

namespace ParkingService.Application.Features.LotOperations;

// ===== DTOs =====
public sealed record HeartbeatResultDto(int LotId, DateTime ReceivedAtUtc, bool RecoveredFromStale);

/// <summary>Port persistence cho LotOperations (Infrastructure EF implement).</summary>
public interface ILotOperationsRepository
{
    Task<ParkingLot?> GetLotAsync(int lotId, CancellationToken cancellationToken = default);
    Task<List<ParkingLot>> GetActiveLotsAsync(CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

/// <summary>US-068: phát integration event qua transactional outbox của ParkingDbContext.</summary>
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
