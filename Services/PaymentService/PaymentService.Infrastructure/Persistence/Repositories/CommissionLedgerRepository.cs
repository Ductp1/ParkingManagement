using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
using ParkingManagement.SharedKernel.Enums;
using PaymentService.Application.Features.Commission;
using PaymentService.Domain.Entities;
using PaymentService.Infrastructure.Persistence.Queries;

namespace PaymentService.Infrastructure.Persistence.Repositories;

/// <summary>
/// US-079: đọc ứng viên (AsNoTracking) và ghi sổ hoa hồng. Ghi cả lô trong 1 SaveChanges; trùng index duy nhất (instance khác vừa ghi)
/// → ghi lại từng dòng, dòng trùng bỏ qua. Khóa lượt chạy = pg_try_advisory_lock trên chính kết nối của DbContext (giữ mở tới khi nhả).
/// </summary>
public sealed class CommissionLedgerRepository(PaymentDbContext db, ILogger<CommissionLedgerRepository> logger) : ICommissionLedgerRepository
{
    /// <summary>Khóa advisory riêng của job hoa hồng (US-079).</summary>
    public const long RunLockKey = 7_900_790_079;

    public async Task<IReadOnlyList<CommissionAccrualCandidate>> ListAccrualCandidatesAsync(IReadOnlyCollection<PaymentPurpose> excludedPurposes,
        int take, CancellationToken cancellationToken)
        => await CommissionQueryShapes.AccrualCandidates(db.Payments.AsNoTracking(), db.CommissionEntries.AsNoTracking(), excludedPurposes, take)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<CommissionReversalCandidate>> ListReversalCandidatesAsync(int take, CancellationToken cancellationToken)
        => await CommissionQueryShapes.ReversalCandidates(db.Payments.AsNoTracking(), db.CommissionEntries.AsNoTracking(), take)
            .ToListAsync(cancellationToken);

    public async Task<CommissionSaveResult> AddEntriesAsync(IReadOnlyList<CommissionEntry> entries, CancellationToken cancellationToken)
    {
        if (entries.Count == 0) return new CommissionSaveResult(0, 0);
        try
        {
            db.CommissionEntries.AddRange(entries);
            await db.SaveChangesAsync(cancellationToken);
            return new CommissionSaveResult(entries.Count, 0);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            db.ChangeTracker.Clear();
            logger.LogInformation("US-079: lô hoa hồng trùng với bút toán đã có – ghi lại từng dòng.");
        }

        var inserted = 0;
        foreach (var entry in entries)
        {
            try
            {
                db.CommissionEntries.Add(entry);
                await db.SaveChangesAsync(cancellationToken);
                inserted++;
            }
            catch (DbUpdateException ex) when (IsUniqueViolation(ex))
            {
                db.ChangeTracker.Clear();
            }
        }
        return new CommissionSaveResult(inserted, entries.Count - inserted);
    }

    public async Task<IAsyncDisposable?> TryAcquireRunLockAsync(CancellationToken cancellationToken)
    {
        await db.Database.OpenConnectionAsync(cancellationToken);
        var acquired = await db.Database.SqlQuery<bool>($"SELECT pg_try_advisory_lock({RunLockKey}) AS \"Value\"")
            .SingleAsync(cancellationToken);
        if (acquired) return new RunLock(db);

        await db.Database.CloseConnectionAsync();
        return null;
    }

    /// <summary>23505 = unique_violation của PostgreSQL.</summary>
    public static bool IsUniqueViolation(DbUpdateException ex) => ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };

    private sealed class RunLock(PaymentDbContext db) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            try
            {
                await db.Database.ExecuteSqlAsync($"SELECT pg_advisory_unlock({RunLockKey})");
            }
            finally
            {
                // Trả kết nối về pool: Npgsql reset phiên (DISCARD ALL) nên khóa cũng được nhả kể cả khi lệnh unlock lỗi.
                await db.Database.CloseConnectionAsync();
            }
        }
    }
}
