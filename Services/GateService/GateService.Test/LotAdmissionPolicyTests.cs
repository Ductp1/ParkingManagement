using GateService.Application.Features.LotCapacity;
using GateService.Domain.Enums;

namespace GateService.Test;

/// <summary>
/// US-066 (luật 2-3) – quyết định cho vào thuần:
/// vãng lai bị chặn khi V = B (dù T &lt; C); thuê bao vẫn vào khi vãng lai hết buffer;
/// bãi đầy (T = C) từ chối cả hai; vãng lai vào được ở V = B - 1.
/// (Authority chống race là UPDATE nguyên tử trong LotAdmissionGate – có test DB riêng.)
/// </summary>
public class LotAdmissionPolicyTests
{
    private const int Capacity = 10;
    private const int Buffer = 3;

    [Fact]
    public void Transient_is_rejected_when_transient_buffer_is_full_even_if_lot_not_full()
    {
        // T = 5 < C = 10 nhưng V = 3 = B → vãng lai bị từ chối.
        var (allowed, reason) = LotAdmissionPolicy.Decide(5, 3, Capacity, Buffer, AdmissionType.Transient);

        Assert.False(allowed);
        Assert.Equal(AdmissionRejectReasons.TransientBufferFull, reason);
    }

    [Fact]
    public void Subscriber_still_enters_when_transient_buffer_is_full()
    {
        var (allowed, reason) = LotAdmissionPolicy.Decide(5, 3, Capacity, Buffer, AdmissionType.Subscriber);

        Assert.True(allowed);
        Assert.Null(reason);
    }

    [Fact]
    public void Both_types_are_rejected_when_lot_is_full()
    {
        var transient = LotAdmissionPolicy.Decide(10, 3, Capacity, Buffer, AdmissionType.Transient);
        var subscriber = LotAdmissionPolicy.Decide(10, 3, Capacity, Buffer, AdmissionType.Subscriber);

        Assert.False(transient.Allowed);
        Assert.False(subscriber.Allowed);
        Assert.Equal(AdmissionRejectReasons.LotFull, transient.RejectReason);
        Assert.Equal(AdmissionRejectReasons.LotFull, subscriber.RejectReason);
    }

    [Fact]
    public void Transient_enters_at_buffer_minus_one()
    {
        // V = B - 1 = 2 → vãng lai vào được (chỗ cuối của vùng đệm).
        var (allowed, reason) = LotAdmissionPolicy.Decide(5, 2, Capacity, Buffer, AdmissionType.Transient);

        Assert.True(allowed);
        Assert.Null(reason);
    }

    [Fact]
    public void Zero_buffer_blocks_all_transients_from_the_start()
    {
        // Biên B = 0: không nhận vãng lai nào (V = 0 đã >= B = 0).
        var (allowed, reason) = LotAdmissionPolicy.Decide(0, 0, Capacity, 0, AdmissionType.Transient);

        Assert.False(allowed);
        Assert.Equal(AdmissionRejectReasons.TransientBufferFull, reason);
    }
}