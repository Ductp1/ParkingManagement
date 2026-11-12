using GateService.Domain.Entities;
using GateService.Domain.Enums;

namespace GateService.Application.Features.LotCapacity;

/// <summary>Counters runtime (T, V, S) đọc để hiển thị.</summary>
public sealed record AdmissionCounters(int Total, int Transient, int Subscriber);

public interface ILotCapacityPolicyStore
{
    Task<LotCapacityConfig?> GetAsync(int parkingLotId, CancellationToken cancellationToken = default);
    Task<AdmissionCounters?> GetCountersAsync(int parkingLotId, CancellationToken cancellationToken = default);
    Task AddAsync(LotCapacityConfig config, CancellationToken cancellationToken = default);
    Task AddAuditAsync(LotCapacityAuditLog log, CancellationToken cancellationToken = default);
    Task SaveAsync(CancellationToken cancellationToken = default);
}

/// <summary>Đếm thuê bao đang hiệu lực (A) — nguồn truth ở BookingService, đọc qua HTTP (lỗi → 0).</summary>
public interface IActiveSubscriberCounter
{
    Task<int> CountActiveAsync(int parkingLotId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Cổng vào/ra nguyên tử (chống race): TryAdmit thực thi UPDATE có điều kiện
/// (total &lt; capacity AND (subscriber OR transient &lt; buffer)); rowsAffected = 0 → từ chối.
/// </summary>
public interface ILotAdmissionGate
{
    Task<(bool Admitted, string? RejectReason)> TryAdmitAsync(
        int parkingLotId, AdmissionType type, CancellationToken cancellationToken = default);

    /// <summary>Trừ đúng bộ đếm theo loại đã ghi lúc vào (luật 6). Bảo vệ total &gt; 0 / transient &gt; 0.</summary>
    Task ReleaseAsync(int parkingLotId, AdmissionType type, CancellationToken cancellationToken = default);
}

/// <summary>Publish CapacityChanged qua SignalR (US-065) tới group bãi xe. Lỗi publish KHÔNG được làm hỏng nghiệp vụ.</summary>
public interface ILotCapacityRealtimePublisher
{
    Task PublishCapacityChangedAsync(int parkingLotId, CancellationToken cancellationToken = default);
}