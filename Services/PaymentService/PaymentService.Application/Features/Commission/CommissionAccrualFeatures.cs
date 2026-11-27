using Microsoft.Extensions.Logging;
using ParkingManagement.SharedKernel.Enums;
using PaymentService.Domain.Entities;

namespace PaymentService.Application.Features.Commission;

// ===== Chính sách (cấu hình "Commission") =====

/// <summary>
/// US-079: chính sách tính hoa hồng. ExcludedPurposes = mục đích thanh toán không tính hoa hồng (mặc định rỗng = tính cả 5 mục đích).
/// BatchSize = số giao dịch tối đa mỗi lượt (≤ 200 – giới hạn của endpoint AdminService).
/// </summary>
public sealed record CommissionPolicy(IReadOnlyList<PaymentPurpose> ExcludedPurposes, int BatchSize = CommissionPolicy.DefaultBatchSize)
{
    public const int DefaultBatchSize = 200;
    public const int MaxBatchSize = 200;

    public static CommissionPolicy Default { get; } = new([]);

    public bool IsExcluded(PaymentPurpose purpose) => ExcludedPurposes.Contains(purpose);
}

/// <summary>Trạng thái hoa hồng của một giao dịch / một nhóm giao dịch trong báo cáo.</summary>
public static class CommissionStatuses
{
    /// <summary>Đã tính xong (kể cả phần hoàn tiền).</summary>
    public const string Calculated = "Calculated";
    /// <summary>Đang chờ job tính (chưa có Accrual hoặc chưa bù phần hoàn mới) – KHÔNG phải 0.</summary>
    public const string Pending = "Pending";
    /// <summary>Nhóm có cả giao dịch đã tính và giao dịch chờ tính.</summary>
    public const string Partial = "Partial";
    /// <summary>Mục đích thanh toán bị loại khỏi hoa hồng (Commission:ExcludedPurposes).</summary>
    public const string NotApplicable = "NotApplicable";

    /// <summary>Trạng thái của một nhóm: không có giao dịch chờ → Calculated; tất cả chờ → Pending; còn lại Partial.</summary>
    public static string ForGroup(int transactionCount, int pendingCount)
        => pendingCount <= 0 ? Calculated : pendingCount >= transactionCount ? Pending : Partial;
}

// ===== PORT =====

/// <summary>Giao dịch đã thu tiền chưa có Accrual.</summary>
public sealed record CommissionAccrualCandidate(int PaymentId, int ParkingLotId, int OwnerProfileId, decimal Amount, DateTime PaidAtUtc);

/// <summary>Giao dịch đã có Accrual nhưng RefundedAmount vượt mức đã bù (CoveredAmount); ReversedSoFar = tổng |Amount| các Reversal.</summary>
public sealed record CommissionReversalCandidate(int PaymentId, int ParkingLotId, int OwnerProfileId, DateTime PaidAtUtc, decimal RefundedAmount,
    decimal Rate, string RateSource, string? ContractNumber, int? ContractVersion, decimal CoveredAmount, decimal ReversedSoFar);

public sealed record CommissionSaveResult(int Inserted, int Duplicates);

/// <summary>PORT sổ hoa hồng: đọc ứng viên + ghi bút toán (chỉ thêm). Trùng khóa (instance khác đã ghi) → bỏ qua, không lỗi.</summary>
public interface ICommissionLedgerRepository
{
    Task<IReadOnlyList<CommissionAccrualCandidate>> ListAccrualCandidatesAsync(IReadOnlyCollection<PaymentPurpose> excludedPurposes, int take,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<CommissionReversalCandidate>> ListReversalCandidatesAsync(int take, CancellationToken cancellationToken);

    Task<CommissionSaveResult> AddEntriesAsync(IReadOnlyList<CommissionEntry> entries, CancellationToken cancellationToken);

    /// <summary>Khóa một lượt chạy giữa nhiều instance (pg_try_advisory_lock). null = instance khác đang chạy → bỏ lượt này.</summary>
    Task<IAsyncDisposable?> TryAcquireRunLockAsync(CancellationToken cancellationToken);
}

/// <summary>Tỷ lệ do AdminService giải. Rate là PHÂN SỐ 0–1; Source = LotContract | DefaultConfig.</summary>
public sealed record CommissionRateQuote(int ParkingLotId, DateTime AtUtc, decimal Rate, string Source, string? Reason, string? ContractNumber,
    int? ContractVersion);

/// <summary>AdminService lỗi / timeout / chưa cấu hình / trả dữ liệu sai → không ghi gì, lượt sau thử lại.</summary>
public sealed class CommissionRateUnavailableException(string message, Exception? inner = null) : Exception(message, inner);

/// <summary>PORT gọi AdminService POST /internal/v1/commission-rates/resolve (khóa nội bộ của PaymentService).</summary>
public interface ICommissionRateClient
{
    /// <summary>Kết quả cùng thứ tự với <paramref name="items"/>. Lỗi → <see cref="CommissionRateUnavailableException"/>.</summary>
    Task<IReadOnlyList<CommissionRateQuote>> ResolveAsync(IReadOnlyList<(int ParkingLotId, DateTime AtUtc)> items,
        CancellationToken cancellationToken);
}

// ===== USE CASE =====

/// <summary>Kết quả một lượt: Skipped = instance khác đang giữ khóa; RateError = lý do không ghi được Accrual (AdminService).</summary>
public sealed record CommissionRunResult(bool Skipped, int AccrualCandidates, int Accrued, int ReversalCandidates, int Reversed, int Duplicates,
    string? RateError)
{
    public static CommissionRunResult SkippedRun { get; } = new(true, 0, 0, 0, 0, 0, null);
    public int Processed => Accrued + Reversed;
}

public interface IRunCommissionAccrualUseCase
{
    Task<CommissionRunResult> ExecuteAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// US-079: một lượt tính hoa hồng tự động (gọi định kỳ bởi CommissionAccrualJob).
/// <list type="number">
/// <item>Accrual: giao dịch Succeeded / PartiallyRefunded / Refunded có PaidAtUtc, chưa có Accrual (trừ mục đích bị loại) –
///   lấy tỷ lệ của AdminService theo đúng PaidAtUtc, Amount = round(Amount × Rate). AdminService lỗi → cả lô không ghi (không đoán tỷ lệ).</item>
/// <item>Reversal: giao dịch có RefundedAmount vượt mức đã bù – dùng tỷ lệ của Accrual (không gọi AdminService), ghi phần chênh (số âm).</item>
/// </list>
/// Chạy song song nhiều instance an toàn: khóa advisory cho mỗi lượt + index duy nhất làm chốt cuối (trùng → bỏ qua).
/// Không ghi audit: thao tác của hệ thống, sổ chỉ-thêm chính là vết kiểm toán.
/// </summary>
public sealed class RunCommissionAccrualUseCase(ICommissionLedgerRepository ledger, ICommissionRateClient rates, TimeProvider timeProvider,
    ILogger<RunCommissionAccrualUseCase> logger, CommissionPolicy? policy = null) : IRunCommissionAccrualUseCase
{
    private readonly CommissionPolicy policy = policy ?? CommissionPolicy.Default;

    public async Task<CommissionRunResult> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        await using var runLock = await ledger.TryAcquireRunLockAsync(cancellationToken);
        if (runLock is null) return CommissionRunResult.SkippedRun;

        var take = Math.Clamp(policy.BatchSize, 1, CommissionPolicy.MaxBatchSize);
        var duplicates = 0;

        // (1) Accrual
        var accrualCandidates = await ledger.ListAccrualCandidatesAsync(policy.ExcludedPurposes, take, cancellationToken);
        var accrued = 0;
        string? rateError = null;
        if (accrualCandidates.Count > 0)
        {
            try
            {
                var entries = await BuildAccrualsAsync(accrualCandidates, cancellationToken);
                var saved = await ledger.AddEntriesAsync(entries, cancellationToken);
                (accrued, duplicates) = (saved.Inserted, saved.Duplicates);
            }
            catch (CommissionRateUnavailableException ex)
            {
                rateError = ex.Message;
                logger.LogWarning(ex, "US-079: chưa lấy được tỷ lệ hoa hồng cho {Count} giao dịch – không ghi, lượt sau thử lại. {Reason}",
                    accrualCandidates.Count, ex.Message);
            }
        }

        // (2) Reversal – độc lập với AdminService
        var reversalCandidates = await ledger.ListReversalCandidatesAsync(take, cancellationToken);
        var reversed = 0;
        if (reversalCandidates.Count > 0)
        {
            var now = timeProvider.GetUtcNow().UtcDateTime;
            var saved = await ledger.AddEntriesAsync(reversalCandidates.Select(c => CommissionEntry.Reversal(c.PaymentId, c.ParkingLotId,
                c.OwnerProfileId, c.PaidAtUtc, c.Rate, c.RateSource, c.ContractNumber, c.ContractVersion, c.RefundedAmount, c.CoveredAmount,
                c.ReversedSoFar, now)).ToList(), cancellationToken);
            reversed = saved.Inserted;
            duplicates += saved.Duplicates;
        }

        var result = new CommissionRunResult(false, accrualCandidates.Count, accrued, reversalCandidates.Count, reversed, duplicates, rateError);
        if (result.Processed > 0 || duplicates > 0 || rateError is not null)
            logger.LogDebug(
                "US-079: lượt tính hoa hồng – Accrual {Accrued}/{AccrualCandidates}, Reversal {Reversed}/{ReversalCandidates}, trùng bỏ qua {Duplicates}.",
                accrued, accrualCandidates.Count, reversed, reversalCandidates.Count, duplicates);
        return result;
    }

    private async Task<List<CommissionEntry>> BuildAccrualsAsync(IReadOnlyList<CommissionAccrualCandidate> candidates,
        CancellationToken cancellationToken)
    {
        // Gộp các (bãi, thời điểm) trùng: một request cho cả lô.
        var keys = candidates.Select(c => (c.ParkingLotId, AtUtc: DateTime.SpecifyKind(c.PaidAtUtc, DateTimeKind.Utc))).Distinct().ToList();
        var quotes = await rates.ResolveAsync(keys, cancellationToken);
        if (quotes.Count != keys.Count)
            throw new CommissionRateUnavailableException($"AdminService trả {quotes.Count} tỷ lệ cho {keys.Count} yêu cầu.");

        var byKey = new Dictionary<(int, DateTime), CommissionRateQuote>();
        for (var i = 0; i < keys.Count; i++)
        {
            var quote = quotes[i];
            if (quote.ParkingLotId != keys[i].ParkingLotId || quote.Rate is < 0 or > 1 || string.IsNullOrWhiteSpace(quote.Source))
                throw new CommissionRateUnavailableException($"AdminService trả tỷ lệ không hợp lệ cho bãi {keys[i].ParkingLotId}.");
            byKey[keys[i]] = quote;
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        return candidates.Select(c =>
        {
            var quote = byKey[(c.ParkingLotId, DateTime.SpecifyKind(c.PaidAtUtc, DateTimeKind.Utc))];
            var fromContract = quote.Source == "LotContract";
            return CommissionEntry.Accrual(c.PaymentId, c.ParkingLotId, c.OwnerProfileId, c.Amount, c.PaidAtUtc, quote.Rate, quote.Source,
                fromContract ? quote.ContractNumber : null, fromContract ? quote.ContractVersion : null, now);
        }).ToList();
    }
}
