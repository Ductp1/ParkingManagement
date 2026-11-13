using Microsoft.Extensions.Logging;
using ParkingManagement.SharedKernel.Contracts;
using ParkingManagement.SharedKernel.Exceptions;
using ParkingService.Application.Abstractions;
using ParkingService.Domain;
using ParkingService.Domain.Entities;

namespace ParkingService.Application.Features.LotHeartbeat;

/// <summary>
/// USE CASE US-068: thiết bị/controller/chủ bãi báo dữ liệu còn sống.
/// Refresh heartbeat, tự phục hồi stale và phát LotDataRecovered qua outbox nếu bãi vừa thoát stale.
/// </summary>
public sealed class RecordLotHeartbeatUseCase(
    ILotStalenessRepository stalenessRepository,
    LotHeartbeatSettings settings,
    TimeProvider clock,
    ILogger<RecordLotHeartbeatUseCase> logger) : IRecordLotHeartbeatUseCase
{
    public async Task<LotHeartbeatResultDto> ExecuteAsync(int lotId, CancellationToken cancellationToken = default)
    {
        if (lotId <= 0)
            throw new ValidationException("Mã bãi đỗ (lotId) không hợp lệ.");

        var lot = await stalenessRepository.GetByIdForUpdateAsync(lotId, cancellationToken)
            ?? throw new NotFoundException("Bãi đỗ xe", lotId);

        var nowUtc = clock.GetUtcNow().UtcDateTime;
        var recovered = lot.RecordHeartbeat(nowUtc);

        var events = new List<IntegrationEvent>();
        if (recovered)
            events.Add(new LotDataRecovered(lot.Id, lot.Name, lot.OwnerProfileId, nowUtc));

        await stalenessRepository.SaveWithEventsAsync(events, cancellationToken);

        logger.LogInformation(
            "US-068 Heartbeat bãi {LotId} lúc {At} (staleRecovered={Recovered}, ngưỡng {StaleAfterMinutes} phút)",
            lot.Id, nowUtc, recovered, settings.StaleAfterMinutes);

        return new LotHeartbeatResultDto(lot.Id, nowUtc, lot.IsDataStale, recovered);
    }
}

/// <summary>
/// USE CASE US-068: đọc độ tươi dữ liệu bãi theo thời gian thực (LastHeartbeatAtUtc + cờ worker).
/// Driver/owner dùng để hiển thị cảnh báo "dữ liệu có thể không chính xác".
/// </summary>
public sealed class GetLotFreshnessUseCase(
    IParkingLotRepository parkingLotRepository,
    LotHeartbeatSettings settings,
    TimeProvider clock) : IGetLotFreshnessUseCase
{
    public async Task<LotFreshnessDto> ExecuteAsync(int lotId, CancellationToken cancellationToken = default)
    {
        if (lotId <= 0)
            throw new ValidationException("Mã bãi đỗ (lotId) không hợp lệ.");

        var lot = await parkingLotRepository.GetByIdAsync(lotId, cancellationToken)
            ?? throw new NotFoundException("Bãi đỗ xe", lotId);

        var nowUtc = clock.GetUtcNow().UtcDateTime;
        var stale = lot.IsDataStale || StalenessEvaluator.IsStale(lot.LastHeartbeatAtUtc, settings.StaleAfter, nowUtc);
        var status = lot.LastHeartbeatAtUtc is null ? "NeverReported" : stale ? "Stale" : "Fresh";

        return new LotFreshnessDto(lot.Id, lot.Name, status, stale, lot.LastHeartbeatAtUtc, settings.StaleAfterMinutes);
    }
}
