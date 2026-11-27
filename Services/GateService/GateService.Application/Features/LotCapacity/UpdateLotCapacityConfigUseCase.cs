using GateService.Domain.Entities;
using Microsoft.Extensions.Logging;
using ParkingManagement.SharedKernel.Exceptions;

namespace GateService.Application.Features.LotCapacity;

public interface IUpdateLotCapacityConfigUseCase
{
    Task<UpdateLotCapacityResult> ExecuteAsync(UpdateLotCapacityCommand command, CancellationToken cancellationToken = default);
}

/// <summary>
/// PUT /api/v1/lots/{lotId}/capacity-config (Admin):
/// validate luật 1 → 409 version → luật 7a (C &lt; T: từ chối kèm số xe đang đỗ) → luật 7b/c
/// (B &lt; V, Q &lt; A: lưu + warnings; khách mới bị chặn tự nhiên bởi điều kiện nguyên tử) → audit → publish (best-effort).
/// </summary>
public sealed class UpdateLotCapacityConfigUseCase(
    ILotCapacityPolicyStore store,
    IActiveSubscriberCounter subscribers,
    ILotCapacityRealtimePublisher publisher,
    TimeProvider timeProvider,
    ILogger<UpdateLotCapacityConfigUseCase> logger) : IUpdateLotCapacityConfigUseCase
{
    public async Task<UpdateLotCapacityResult> ExecuteAsync(UpdateLotCapacityCommand command, CancellationToken cancellationToken = default)
    {
        // 1) Validate luật 1: lỗi từng ô (thiếu → sai → vượt tổng) trước, lỗi tổng hợp sau.
        var outcome = LotCapacityValidator.Validate(command.Capacity, command.TransientBuffer, command.SubscriberQuota);

        // 2) Optimistic concurrency: version cũ → 409 CONFIG_VERSION_CONFLICT.
        var policy = await store.GetAsync(command.ParkingLotId, cancellationToken);
        var isNew = policy is null;
        if (!isNew && policy!.Version != command.Version)
            throw new StaleCapacityConfigVersionException(command.Version, policy.Version);

        // 3) Runtime counters + A (luật 7).
        var counters = await store.GetCountersAsync(command.ParkingLotId, cancellationToken) ?? new AdmissionCounters(0, 0, 0);
        var activeSubscribers = await subscribers.CountActiveAsync(command.ParkingLotId, cancellationToken);

        // Gom lỗi: từng ô trước; luật 7a (C mới < T → từ chối kèm số xe) là lỗi của ô "capacity";
        // lỗi tổng hợp chỉ hiện khi không còn lỗi nào của từng ô.
        var fieldErrors = new Dictionary<string, CapacityFieldError[]>(outcome.Errors);
        CapacityFieldError? summary = outcome.Summary;

        // Luật 7a: C mới < T hiện tại → lỗi của ô "capacity" (từ chối, kèm số xe đang đỗ);
        // lỗi từng ô ưu tiên → ẩn lỗi tổng hợp.
        if (!outcome.HasErrors && command.Capacity!.Value < counters.Total)
        {
            fieldErrors["capacity"] = [new CapacityFieldError(LotCapacityValidator.CodeCapacityBelowOccupancy,
                $"Bãi đang có {counters.Total} xe đang đỗ. Không thể giảm tổng số chỗ xuống {command.Capacity.Value} — hãy chọn từ {counters.Total} trở lên.")];
            summary = null;
        }

        if (fieldErrors.Count > 0 || summary is not null)
            throw new CapacityValidationException(fieldErrors, summary);

        var newCapacity = command.Capacity!.Value;
        var newBuffer = command.TransientBuffer!.Value;
        var newQuota = command.SubscriberQuota!.Value;

        // 4) Luật 7b/c: B < V / Q < A → cho lưu kèm cảnh báo; khách mới bị chặn tự nhiên
        //    bởi điều kiện nguyên tử (V < B / A < Q) cho tới khi counters tụt xuống.
        var warnings = new List<string>();
        if (newBuffer < counters.Transient)
            warnings.Add($"Vùng đệm mới (B = {newBuffer}) nhỏ hơn số xe vãng lai đang đỗ (V = {counters.Transient}): " +
                         "xe vãng lai mới sẽ bị chặn cho tới khi V < B. Xe đang đỗ không bị ảnh hưởng.");
        if (newQuota < activeSubscribers)
            warnings.Add($"Hạn mức mới (Q = {newQuota}) nhỏ hơn số thuê bao đang hiệu lực (A = {activeSubscribers}): " +
                         "đăng ký thuê bao mới sẽ bị chặn cho tới khi A < Q. Thuê bao hiện có không bị hủy.");

        var oldCapacity = policy?.Capacity ?? 0;
        var oldBuffer = policy?.TransientBuffer ?? 0;
        var oldQuota = policy?.SubscriberQuota ?? 0;
        var oldVersion = policy?.Version ?? 0;

        if (isNew)
        {
            policy = new LotCapacityConfig
            {
                ParkingLotId = command.ParkingLotId,
                Capacity = newCapacity,
                TransientBuffer = newBuffer,
                SubscriberQuota = newQuota,
                Version = 1,
                LastChangedByUserId = command.ChangedByUserId,
            };
            await store.AddAsync(policy, cancellationToken);
        }
        else
        {
            policy!.Capacity = newCapacity;
            policy.TransientBuffer = newBuffer;
            policy.SubscriberQuota = newQuota;
            policy.Version += 1;
            policy.LastChangedByUserId = command.ChangedByUserId;
        }

        await store.AddAuditAsync(new LotCapacityAuditLog
        {
            ParkingLotId = command.ParkingLotId,
            ChangedByUserId = command.ChangedByUserId,
            ChangedAtUtc = timeProvider.GetUtcNow().UtcDateTime,
            OldCapacity = oldCapacity, OldTransientBuffer = oldBuffer, OldSubscriberQuota = oldQuota,
            NewCapacity = policy.Capacity, NewTransientBuffer = policy.TransientBuffer, NewSubscriberQuota = policy.SubscriberQuota,
            OldVersion = oldVersion, NewVersion = policy.Version,
            WarningsJson = warnings.Count > 0 ? System.Text.Json.JsonSerializer.Serialize(warnings) : null,
        });
        await store.SaveAsync(cancellationToken);

        // 5) Publish CapacityChanged (US-065) — lỗi publish KHÔNG làm hỏng nghiệp vụ chính.
        try
        {
            await publisher.PublishCapacityChangedAsync(command.ParkingLotId, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "US-066: publish CapacityChanged thất bại cho lot {LotId} — bỏ qua.", command.ParkingLotId);
        }

        logger.LogInformation("US-066: lot {LotId} cập nhật C/B/Q = {C}/{B}/{Q} (version {V}) bởi user {U}, warnings = {W}",
            command.ParkingLotId, policy.Capacity, policy.TransientBuffer, policy.SubscriberQuota,
            policy.Version, command.ChangedByUserId, warnings.Count);

        var snapshot = GetLotCapacityConfigUseCase.BuildSnapshot(policy, counters, activeSubscribers);
        return new UpdateLotCapacityResult(true, warnings, policy.Version, snapshot);
    }
}