using ParkingManagement.SharedKernel.Enums;
using PaymentService.Application.Features.Reports;
using PaymentService.Domain.Entities;
using PaymentService.Infrastructure.Persistence.Queries;

namespace PaymentService.Test;

/// <summary>
/// US-078: port báo cáo doanh thu chạy trong bộ nhớ bằng ĐÚNG hình dạng truy vấn của Infrastructure (OwnerRevenueQueryShapes),
/// nên test kiểm tra được lọc chủ bãi / trạng thái / ranh giới ngày thật, không chỉ logic cộng của use case.
/// </summary>
internal sealed class InMemoryOwnerRevenueQueries(IEnumerable<Payment>? payments = null, IEnumerable<Settlement>? settlements = null)
    : IOwnerRevenueQueries
{
    private readonly List<Payment> _payments = payments?.ToList() ?? [];
    private readonly List<Settlement> _settlements = settlements?.ToList() ?? [];

    public OwnerRevenueFilter? LastFilter { get; private set; }

    public Task<IReadOnlyList<OwnerRevenueGroupRow>> SumByLotMethodPurposeAsync(OwnerRevenueFilter filter, CancellationToken cancellationToken)
        => Run(filter, OwnerRevenueQueryShapes.SumByLotMethodPurpose(_payments.AsQueryable(), filter));

    public Task<IReadOnlyList<OwnerRevenueHourRow>> SumByHourOfDayAsync(OwnerRevenueFilter filter, CancellationToken cancellationToken)
        => Run(filter, OwnerRevenueQueryShapes.SumByHourOfDay(_payments.AsQueryable(), filter));

    public Task<IReadOnlyList<OwnerRefundReasonRow>> SumCompletedRefundsByReasonAsync(OwnerRevenueFilter filter,
        CancellationToken cancellationToken)
        => Run(filter, OwnerRevenueQueryShapes.SumCompletedRefundsByReason(_payments.AsQueryable(), filter));

    public Task<IReadOnlyList<OwnerSettlementRow>> ListSettledPeriodsAsync(OwnerRevenueFilter filter, CancellationToken cancellationToken)
        => Run(filter, OwnerRevenueQueryShapes.SettledPeriods(_settlements.AsQueryable(), filter));

    private Task<IReadOnlyList<T>> Run<T>(OwnerRevenueFilter filter, IQueryable<T> query)
    {
        LastFilter = filter;
        return Task.FromResult<IReadOnlyList<T>>(query.ToList());
    }
}

internal static class OwnerRevenueTestData
{
    public const int OwnerA = 1;
    public const int OwnerB = 2;
    public const int LotA1 = 11;
    public const int LotA2 = 12;
    public const int LotB1 = 21;

    /// <summary>Giao dịch đã thu tiền lúc <paramref name="paidAtUtc"/>.</summary>
    public static Payment Paid(int owner, int lot, decimal amount, DateTime paidAtUtc,
        PaymentMethod method = PaymentMethod.Vnpay, PaymentPurpose purpose = PaymentPurpose.Booking,
        PaymentStatus status = PaymentStatus.Succeeded, decimal refunded = 0)
        => new()
        {
            Code = $"PM-{Guid.NewGuid():N}"[..20], OwnerProfileId = owner, ParkingLotId = lot, Amount = amount, RefundedAmount = refunded,
            Method = method, Purpose = purpose, Status = status, PaidAtUtc = paidAtUtc, BookingId = 1,
        };

    public static DateTime Utc(int year, int month, int day, int hour = 0, int minute = 0, int second = 0)
        => new(year, month, day, hour, minute, second, DateTimeKind.Utc);
}

internal sealed class FixedTimeProvider(DateTime utcNow) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => new(utcNow, TimeSpan.Zero);
}
