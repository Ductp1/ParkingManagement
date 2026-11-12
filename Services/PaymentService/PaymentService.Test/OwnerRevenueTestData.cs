using System.Reflection;
using ParkingManagement.SharedKernel.Contracts;
using ParkingManagement.SharedKernel.Domain;
using ParkingManagement.SharedKernel.Enums;
using PaymentService.Application.Features.Reports;
using PaymentService.Domain.Entities;
using PaymentService.Infrastructure.Persistence.Queries;

namespace PaymentService.Test;

/// <summary>
/// US-078: port báo cáo doanh thu chạy trong bộ nhớ bằng ĐÚNG hình dạng truy vấn của Infrastructure (OwnerRevenueQueryShapes),
/// nên test kiểm tra được lọc chủ bãi / trạng thái / ranh giới ngày thật, không chỉ logic cộng của use case.
/// </summary>
internal sealed class InMemoryOwnerRevenueQueries(IEnumerable<Payment>? payments = null, IEnumerable<Settlement>? settlements = null,
    IEnumerable<CommissionEntry>? commissionEntries = null) : IOwnerRevenueQueries
{
    private readonly List<Payment> _payments = payments?.ToList() ?? [];
    private readonly List<Settlement> _settlements = settlements?.ToList() ?? [];
    private readonly List<CommissionEntry> _entries = commissionEntries?.ToList() ?? [];

    public Task<IReadOnlyList<OwnerCommissionLotRow>> SumCommissionByLotAsync(OwnerRevenueFilter filter, CancellationToken cancellationToken)
        => Run(filter, OwnerRevenueQueryShapes.SumCommissionByLot(_entries.AsQueryable(), filter));

    public Task<IReadOnlyList<OwnerCommissionDayRow>> SumCommissionByVietnamDayAsync(OwnerRevenueFilter filter, CancellationToken cancellationToken)
        => Run(filter, OwnerRevenueQueryShapes.SumCommissionByVietnamDay(_entries.AsQueryable(), filter));

    public Task<IReadOnlyList<OwnerCommissionDayHourRow>> SumCommissionByVietnamDayHourAsync(OwnerRevenueFilter filter,
        CancellationToken cancellationToken)
        => Run(filter, OwnerRevenueQueryShapes.SumCommissionByVietnamDayHour(_entries.AsQueryable(), filter));

    public Task<IReadOnlyList<OwnerCommissionPendingRow>> CountCommissionPendingAsync(OwnerRevenueFilter filter,
        IReadOnlyCollection<PaymentPurpose> excludedPurposes, CancellationToken cancellationToken)
        => Run(filter, OwnerRevenueQueryShapes.CountCommissionPending(_payments.AsQueryable(), _entries.AsQueryable(), filter, excludedPurposes));

    public Task<IReadOnlyList<PaymentCommissionRow>> ListCommissionByPaymentAsync(IReadOnlyCollection<int> paymentIds,
        CancellationToken cancellationToken)
        => Task.FromResult(OwnerRevenueQueryShapes.ToPaymentCommission(
            OwnerRevenueQueryShapes.EntriesOfPayments(_entries.AsQueryable(), paymentIds).ToList()));

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

    public Task<IReadOnlyList<OwnerRevenueDayRow>> SumByVietnamDayAsync(OwnerRevenueFilter filter, CancellationToken cancellationToken)
        => Run(filter, OwnerRevenueQueryShapes.SumByVietnamDay(_payments.AsQueryable(), filter));

    public Task<IReadOnlyList<OwnerRevenueDayHourRow>> SumByVietnamDayHourAsync(OwnerRevenueFilter filter, CancellationToken cancellationToken)
        => Run(filter, OwnerRevenueQueryShapes.SumByVietnamDayHour(_payments.AsQueryable(), filter));

    public Task<PagedResult<OwnerRevenueTransactionRow>> ListTransactionsAsync(OwnerRevenueTransactionFilter filter, int page, int pageSize,
        CancellationToken cancellationToken)
    {
        LastFilter = filter.Range;
        LastTransactionFilter = filter;
        var query = OwnerRevenueQueryShapes.FilterTransactions(_payments.AsQueryable(), filter);
        var items = OwnerRevenueQueryShapes.ToTransactionRows(query.Skip((page - 1) * pageSize).Take(pageSize)).ToList();
        return Task.FromResult(new PagedResult<OwnerRevenueTransactionRow>(items, page, pageSize, query.Count()));
    }

    public Task<IReadOnlyList<OwnerRevenueTransactionRow>> ListTransactionsForExportAsync(OwnerRevenueTransactionFilter filter, int maxRows,
        CancellationToken cancellationToken)
    {
        LastTransactionFilter = filter;
        return Run(filter.Range, OwnerRevenueQueryShapes.ToTransactionRows(
            OwnerRevenueQueryShapes.FilterTransactions(_payments.AsQueryable(), filter).Take(maxRows)));
    }

    public OwnerRevenueTransactionFilter? LastTransactionFilter { get; private set; }

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

    private static readonly PropertyInfo IdProperty = typeof(BaseEntity).GetProperty(nameof(BaseEntity.Id))!;

    /// <summary>Id do database cấp – test gán tay để liên kết bút toán hoa hồng theo PaymentId.</summary>
    public static T WithId<T>(this T entity, int id) where T : BaseEntity
    {
        IdProperty.SetValue(entity, id);
        return entity;
    }

    /// <summary>US-079: Accrual của giao dịch với tỷ lệ (phân số) cho sẵn.</summary>
    public static CommissionEntry Accrual(Payment payment, decimal rate, string source = "DefaultConfig", string? contractNumber = null)
        => CommissionEntry.Accrual(payment.Id, payment.ParkingLotId, payment.OwnerProfileId, payment.Amount, payment.PaidAtUtc!.Value, rate, source,
            contractNumber, contractNumber is null ? null : 1, payment.PaidAtUtc!.Value);

    /// <summary>US-079: Reversal bù tới mức <paramref name="refundedAmount"/> (lũy kế).</summary>
    public static CommissionEntry Reversal(Payment payment, decimal rate, decimal refundedAmount, decimal coveredSoFar = 0, decimal reversedSoFar = 0,
        string source = "DefaultConfig")
        => CommissionEntry.Reversal(payment.Id, payment.ParkingLotId, payment.OwnerProfileId, payment.PaidAtUtc!.Value, rate, source, null, null,
            refundedAmount, coveredSoFar, reversedSoFar, payment.PaidAtUtc!.Value);
}

internal sealed class FixedTimeProvider(DateTime utcNow) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => new(utcNow, TimeSpan.Zero);
}
