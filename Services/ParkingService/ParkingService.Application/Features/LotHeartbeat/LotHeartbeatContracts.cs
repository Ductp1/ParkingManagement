using ParkingManagement.SharedKernel.Exceptions;

namespace ParkingService.Application.Features.LotHeartbeat;

/// <summary>
/// Cấu hình "LotHeartbeat" (appsettings, US-068): ngưỡng stale và chu kỳ quét của worker.
/// Sai cấu hình → lỗi lúc khởi động (khác KybMisdeclarationSettings dùng cùng phong cách).
/// </summary>
public sealed record LotHeartbeatSettings
{
    public const string SectionName = "LotHeartbeat";
    public const int DefaultStaleAfterMinutes = 3;
    public const int DefaultPollSeconds = 30;
    public const int MinStaleAfterMinutes = 1;
    public const int MaxStaleAfterMinutes = 120;
    public const int MinPollSeconds = 5;
    public const int MaxPollSeconds = 3600;

    /// <summary>Quá khoảng này không nhận heartbeat → dữ liệu bãi bị coi là stale (AC: mặc định 3 phút).</summary>
    public int StaleAfterMinutes { get; }

    /// <summary>Chu kỳ StalenessMonitorWorker quét các bãi Active (mặc định 30 giây).</summary>
    public int PollSeconds { get; }

    public LotHeartbeatSettings(int staleAfterMinutes = DefaultStaleAfterMinutes, int pollSeconds = DefaultPollSeconds)
    {
        if (staleAfterMinutes is < MinStaleAfterMinutes or > MaxStaleAfterMinutes)
            throw new ValidationException($"{SectionName}:StaleAfterMinutes phải từ {MinStaleAfterMinutes} đến {MaxStaleAfterMinutes} phút.");
        if (pollSeconds is < MinPollSeconds or > MaxPollSeconds)
            throw new ValidationException($"{SectionName}:PollSeconds phải từ {MinPollSeconds} đến {MaxPollSeconds} giây.");

        StaleAfterMinutes = staleAfterMinutes;
        PollSeconds = pollSeconds;
    }

    /// <summary>Ngưỡng stale dạng TimeSpan cho StalenessEvaluator.</summary>
    public TimeSpan StaleAfter => TimeSpan.FromMinutes(StaleAfterMinutes);
}

/// <summary>
/// Độ tươi dữ liệu của một bãi xe (US-068). Status:
/// "NeverReported" (chưa từng có heartbeat) | "Fresh" | "Stale".
/// </summary>
public sealed record LotFreshnessDto(
    int ParkingLotId,
    string LotName,
    string Status,
    bool IsDataStale,
    DateTime? LastHeartbeatAtUtc,
    int StaleAfterMinutes);

/// <summary>Kết quả ghi nhận heartbeat (US-068). Recovered = true khi bãi vừa thoát stale.</summary>
public sealed record LotHeartbeatResultDto(
    int ParkingLotId,
    DateTime LastHeartbeatAtUtc,
    bool IsDataStale,
    bool Recovered);

public interface IRecordLotHeartbeatUseCase
{
    /// <summary>POST /api/v1/parking-lots/{lotId}/operations/heartbeat – ghi nhận heartbeat (US-068).</summary>
    Task<LotHeartbeatResultDto> ExecuteAsync(int lotId, CancellationToken cancellationToken = default);
}

public interface IGetLotFreshnessUseCase
{
    /// <summary>GET /api/v1/parking-lots/{lotId}/operations/status – độ tươi dữ liệu hiện tại (US-068).</summary>
    Task<LotFreshnessDto> ExecuteAsync(int lotId, CancellationToken cancellationToken = default);
}
