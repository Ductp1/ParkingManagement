using ParkingManagement.SharedKernel.Enums;
using PaymentService.Application.Features.Commission;
using PaymentService.Domain.Entities;
using PaymentService.Infrastructure.Persistence.Queries;

namespace PaymentService.Test;

// US-079: test double dùng chung cho job hoa hồng và báo cáo Admin.

/// <summary>
/// Sổ hoa hồng trong bộ nhớ chạy ĐÚNG hình dạng truy vấn của Infrastructure (CommissionQueryShapes) và mô phỏng hai index duy nhất
/// (một Accrual / giao dịch; một Reversal / mức RefundedAmountCovered) – để test chống ghi trùng như PostgreSQL.
/// </summary>
internal sealed class InMemoryCommissionLedger(IEnumerable<Payment> payments, IEnumerable<CommissionEntry>? entries = null)
    : ICommissionLedgerRepository
{
    public List<Payment> Payments { get; } = payments.ToList();
    public List<CommissionEntry> Entries { get; } = entries?.ToList() ?? [];
    public bool LockAvailable { get; set; } = true;
    public int LocksReleased { get; private set; }
    /// <summary>Chạy ngay trước khi ghi – mô phỏng instance khác ghi chen vào giữa lúc đọc và lúc ghi.</summary>
    public Action<InMemoryCommissionLedger>? BeforeAdd { get; set; }

    public Task<IReadOnlyList<CommissionAccrualCandidate>> ListAccrualCandidatesAsync(IReadOnlyCollection<PaymentPurpose> excludedPurposes,
        int take, CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<CommissionAccrualCandidate>>(
            CommissionQueryShapes.AccrualCandidates(Payments.AsQueryable(), Entries.AsQueryable(), excludedPurposes, take).ToList());

    public Task<IReadOnlyList<CommissionReversalCandidate>> ListReversalCandidatesAsync(int take, CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<CommissionReversalCandidate>>(
            CommissionQueryShapes.ReversalCandidates(Payments.AsQueryable(), Entries.AsQueryable(), take).ToList());

    public Task<CommissionSaveResult> AddEntriesAsync(IReadOnlyList<CommissionEntry> entries, CancellationToken cancellationToken)
    {
        BeforeAdd?.Invoke(this);
        BeforeAdd = null;
        var inserted = 0;
        foreach (var entry in entries)
        {
            if (!TryInsert(entry)) continue;
            inserted++;
        }
        return Task.FromResult(new CommissionSaveResult(inserted, entries.Count - inserted));
    }

    /// <summary>false = vi phạm index duy nhất.</summary>
    public bool TryInsert(CommissionEntry entry)
    {
        var duplicate = entry.EntryType == CommissionEntryType.Accrual
            ? Entries.Any(e => e.PaymentId == entry.PaymentId && e.EntryType == CommissionEntryType.Accrual)
            : Entries.Any(e => e.PaymentId == entry.PaymentId && e.EntryType == CommissionEntryType.Reversal
                               && e.RefundedAmountCovered == entry.RefundedAmountCovered);
        if (duplicate) return false;
        Entries.Add(entry.WithId(Entries.Count + 1));
        return true;
    }

    public Task<IAsyncDisposable?> TryAcquireRunLockAsync(CancellationToken cancellationToken)
        => Task.FromResult<IAsyncDisposable?>(LockAvailable ? new Release(this) : null);

    public decimal CommissionOf(int paymentId) => Entries.Where(e => e.PaymentId == paymentId).Sum(e => e.Amount);

    private sealed class Release(InMemoryCommissionLedger owner) : IAsyncDisposable
    {
        public ValueTask DisposeAsync()
        {
            owner.LocksReleased++;
            return ValueTask.CompletedTask;
        }
    }
}

/// <summary>AdminService giả: tỷ lệ theo bãi (mặc định 10%, nguồn DefaultConfig), ghi lại các yêu cầu; có thể ném lỗi.</summary>
internal sealed class FakeCommissionRateClient : ICommissionRateClient
{
    public Dictionary<int, (decimal Rate, string Source, string? ContractNumber, int? Version)> Lots { get; } = [];
    public List<IReadOnlyList<(int ParkingLotId, DateTime AtUtc)>> Requests { get; } = [];
    public Exception? Failure { get; set; }
    public Func<IReadOnlyList<CommissionRateQuote>, IReadOnlyList<CommissionRateQuote>>? Tamper { get; set; }

    public Task<IReadOnlyList<CommissionRateQuote>> ResolveAsync(IReadOnlyList<(int ParkingLotId, DateTime AtUtc)> items,
        CancellationToken cancellationToken)
    {
        Requests.Add(items);
        if (Failure is not null) throw Failure;
        IReadOnlyList<CommissionRateQuote> quotes = [.. items.Select(i => Lots.TryGetValue(i.ParkingLotId, out var lot)
            ? new CommissionRateQuote(i.ParkingLotId, i.AtUtc, lot.Rate, lot.Source, null, lot.ContractNumber, lot.Version)
            : new CommissionRateQuote(i.ParkingLotId, i.AtUtc, 0.10m, "DefaultConfig", "NoSignedContract", null, null))];
        return Task.FromResult(Tamper?.Invoke(quotes) ?? quotes);
    }
}

/// <summary>Báo cáo hoa hồng toàn sàn trong bộ nhớ, cùng hình dạng truy vấn với Infrastructure (AdminCommissionQueryShapes).</summary>
internal sealed class InMemoryAdminCommissionQueries(IEnumerable<Payment> payments, IEnumerable<CommissionEntry>? entries = null)
    : IAdminCommissionQueries
{
    private readonly List<Payment> _payments = payments.ToList();
    private readonly List<CommissionEntry> _entries = entries?.ToList() ?? [];

    public Task<IReadOnlyList<AdminRevenueLotRow>> SumRevenueByLotAsync(DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken)
        => Done(AdminCommissionQueryShapes.SumRevenueByLot(_payments.AsQueryable(), fromUtc, toUtc));

    public Task<IReadOnlyList<AdminCommissionLotSourceRow>> SumCommissionByLotSourceAsync(DateTime fromUtc, DateTime toUtc,
        CancellationToken cancellationToken)
        => Done(AdminCommissionQueryShapes.SumCommissionByLotSource(_entries.AsQueryable(), fromUtc, toUtc));

    public Task<IReadOnlyList<AdminPendingLotDayRow>> CountPendingByLotDayAsync(DateTime fromUtc, DateTime toUtc,
        IReadOnlyCollection<PaymentPurpose> excludedPurposes, CancellationToken cancellationToken)
        => Done(AdminCommissionQueryShapes.CountPendingByLotDay(_payments.AsQueryable(), _entries.AsQueryable(), fromUtc, toUtc, excludedPurposes));

    public Task<IReadOnlyList<AdminRevenueDayRow>> SumRevenueByDayAsync(DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken)
        => Done(AdminCommissionQueryShapes.SumRevenueByDay(_payments.AsQueryable(), fromUtc, toUtc));

    public Task<IReadOnlyList<AdminCommissionDayRow>> SumCommissionByDayAsync(DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken)
        => Done(AdminCommissionQueryShapes.SumCommissionByDay(_entries.AsQueryable(), fromUtc, toUtc));

    private static Task<IReadOnlyList<T>> Done<T>(IQueryable<T> query) => Task.FromResult<IReadOnlyList<T>>(query.ToList());
}
