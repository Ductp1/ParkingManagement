using GateService.Domain.Entities;

namespace GateService.Application.Features.LotCapacity;

public interface IGetLotCapacityConfigUseCase
{
    Task<LotCapacityConfigDto> ExecuteAsync(int parkingLotId, CancellationToken cancellationToken = default);
}

/// <summary>GET /api/v1/lots/{lotId}/capacity-config — cấu hình + runtime (T, V, A) + số chỗ trống theo luật 5.</summary>
public sealed class GetLotCapacityConfigUseCase(
    ILotCapacityPolicyStore store,
    IActiveSubscriberCounter subscribers) : IGetLotCapacityConfigUseCase
{
    public async Task<LotCapacityConfigDto> ExecuteAsync(int parkingLotId, CancellationToken cancellationToken = default)
    {
        var policy = await store.GetAsync(parkingLotId, cancellationToken);
        if (policy is null)
        {
            // Chưa cấu hình → trả snapshot "configured = false" để FE hiển thị form trống, không 404.
            return new LotCapacityConfigDto(false, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, null);
        }

        var counters = await store.GetCountersAsync(parkingLotId, cancellationToken) ?? new AdmissionCounters(0, 0, 0);
        var activeSubscribers = await subscribers.CountActiveAsync(parkingLotId, cancellationToken);

        return BuildSnapshot(policy, counters, activeSubscribers);
    }

    internal static LotCapacityConfigDto BuildSnapshot(LotCapacityConfig policy, AdmissionCounters counters, int activeSubscribers)
    {
        var free = Math.Max(0, policy.Capacity - counters.Total);
        return new LotCapacityConfigDto(
            Configured: true,
            Capacity: policy.Capacity,
            TransientBuffer: policy.TransientBuffer,
            SubscriberQuota: policy.SubscriberQuota,
            TotalParked: counters.Total,
            TransientParked: counters.Transient,
            SubscriberParked: counters.Subscriber,
            ActiveSubscribers: activeSubscribers,
            // Luật 5: vãng lai còn max(0, min(C - T, B - V)); thuê bao còn max(0, C - T).
            AvailableForTransient: Math.Max(0, Math.Min(free, Math.Max(0, policy.TransientBuffer - counters.Transient))),
            AvailableForSubscriber: free,
            Version: policy.Version,
            UpdatedAtUtc: policy.UpdatedAtUtc);
    }
}