using System.Text.Json;
using ParkingManagement.SharedKernel.Contracts;
using ParkingManagement.SharedKernel.Domain;
using ParkingManagement.SharedKernel.Enums;
using ParkingManagement.SharedKernel.Exceptions;
using PaymentService.Application.Features.Commission;
using PaymentService.Domain.Entities;
using PaymentService.Domain.Services;

namespace PaymentService.Application.Features.Settlements;

/// <summary>
/// US-080: phần dùng chung của job sinh kỳ, Admin sinh kỳ và Admin tính lại kỳ Draft (không phải use case):
/// lịch kỳ, xác minh booking đã kết thúc (AC1 "chỉ booking COMPLETED"), gom dữ liệu → <see cref="SettlementCalculator"/> → ghi.
/// </summary>
public sealed class SettlementBuilder(ISettlementGenerationRepository repository, ISettlementCycleClient cycles, IBookingStatusClient bookings,
    CommissionPolicy commissionPolicy, SettlementOptions options)
{
    public const string AuditSource = "PaymentService";
    public const string AuditEntity = "Settlement";

    /// <summary>Booking ở các trạng thái này đã kết thúc – tiền của nó được quyết toán (hoàn tiền nếu có đi theo dòng Refund).</summary>
    public static readonly IReadOnlySet<string> TerminalBookingStatuses = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        nameof(BookingStatus.Completed), nameof(BookingStatus.NoShow), nameof(BookingStatus.Cancelled),
        nameof(BookingStatus.CancelledNoRefund), nameof(BookingStatus.Expired), nameof(BookingStatus.Rejected),
    };

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    // ===================== Lịch kỳ =====================

    /// <summary>
    /// Bổ sung lịch tới kỳ gần nhất ĐÃ KẾT THÚC và trả kỳ đó (null = chưa có kỳ nào kết thúc).
    /// Lần đầu: kỳ chứa ngày hôm qua (ICT) nếu đã kết thúc, ngược lại kỳ liền trước. Sau đó mỗi kỳ bắt đầu ngay sau kỳ trước,
    /// chu kỳ = SETTLEMENT_CYCLE có hiệu lực lúc 00:00 ICT ngày đầu kỳ. AdminService lỗi → DependencyUnavailableException.
    /// </summary>
    public async Task<SettlementPeriod?> EnsurePeriodsAsync(DateTime nowUtc, CancellationToken cancellationToken)
    {
        var today = SettlementCalendar.TodayIct(nowUtc);
        var latest = await repository.GetLatestPeriodAsync(cancellationToken);
        if (latest is null)
        {
            var yesterday = today.AddDays(-1);
            var quote = await cycles.ResolveAsync(SettlementCalendar.StartOfDayUtc(yesterday), cancellationToken);
            var cycle = ParseCycle(quote);
            var start = SettlementCalendar.BlockStart(cycle, yesterday, options.BiweeklyAnchor);
            if (SettlementCalendar.BlockEnd(cycle, yesterday, options.BiweeklyAnchor) >= today)
                start = SettlementCalendar.BlockStart(cycle, start.AddDays(-1), options.BiweeklyAnchor);
            latest = await repository.AddPeriodAsync(
                SettlementPeriod.Create(start, cycle, quote.EffectiveFromUtc, options.BiweeklyAnchor, nowUtc), cancellationToken);
        }

        for (var i = 0; i < options.MaxCatchUpPeriods; i++)
        {
            var next = latest.PeriodEnd.AddDays(1);
            if (next >= today) break;                                       // kỳ kế bắt đầu hôm nay → chưa thể kết thúc
            var quote = await cycles.ResolveAsync(SettlementCalendar.StartOfDayUtc(next), cancellationToken);
            var period = SettlementPeriod.Create(next, ParseCycle(quote), quote.EffectiveFromUtc, options.BiweeklyAnchor, nowUtc);
            if (!SettlementCalendar.HasEnded(period.PeriodEnd, nowUtc)) break;
            latest = await repository.AddPeriodAsync(period, cancellationToken);
        }
        return SettlementCalendar.HasEnded(latest.PeriodEnd, nowUtc) ? latest : null;
    }

    private static SettlementCycle ParseCycle(SettlementCycleQuote quote)
    {
        try
        {
            return SettlementCalendar.Parse(quote.Value);
        }
        catch (FormatException ex)
        {
            throw new DependencyUnavailableException($"AdminService trả SETTLEMENT_CYCLE không hợp lệ: {ex.Message}");
        }
    }

    // ===================== Sinh / tính lại cho một chủ bãi =====================

    /// <summary>
    /// Sinh kỳ <paramref name="period"/> cho một chủ bãi. <paramref name="holds"/> = null khi không có JWT Admin (job) → kỳ bị chặn HoldsNotLoaded.
    /// <paramref name="regenerateDraft"/> = true (Admin): kỳ Draft đã có được tính lại; job thì bỏ qua để không mất khoản giữ Admin đã nạp.
    /// </summary>
    public async Task<OwnerSettlementOutcomeDto> GenerateForOwnerAsync(int ownerProfileId, SettlementPeriod period, DateTime nowUtc, int? userId,
        IReadOnlyList<PayoutHoldItem>? holds, bool regenerateDraft, CancellationToken cancellationToken)
    {
        var state = await repository.GetOwnerStateAsync(ownerProfileId, period.PeriodStart, cancellationToken);
        if (state.HasLater) return Skipped(ownerProfileId, SettlementOutcomes.LaterSettlementExists);
        if (state.HasOpenEarlier) return Skipped(ownerProfileId, SettlementOutcomes.OpenEarlierSettlement);
        if (state.ExistingId is { } existingId)
        {
            if (state.ExistingStatus != SettlementStatus.Draft) return Skipped(ownerProfileId, SettlementOutcomes.NotDraft, existingId);
            if (!regenerateDraft) return Skipped(ownerProfileId, SettlementOutcomes.AlreadyGenerated, existingId);
            var draft = await repository.FindSettlementAsync(existingId, cancellationToken);
            if (draft is null) return Skipped(ownerProfileId, SettlementOutcomes.Conflict, existingId);
            try
            {
                return await RegenerateAsync(draft, draft.RowVersion, nowUtc, userId, holds, cancellationToken);
            }
            catch (ConflictException)
            {
                // Admin khác vừa gửi duyệt / tính lại kỳ này – không làm hỏng cả lô, báo lại cho chủ bãi đó.
                return Skipped(ownerProfileId, SettlementOutcomes.Conflict, existingId);
            }
        }

        var computation = await ComputeAsync(ownerProfileId, period, nowUtc, holds, null, cancellationToken);
        if (!computation.HasContent) return Skipped(ownerProfileId, SettlementOutcomes.NothingToSettle);

        var settlement = Settlement.NewDraft(period, ownerProfileId);
        settlement.ApplyComputation(computation, nowUtc, userId, holds is null ? null : nowUtc);
        try
        {
            await repository.AddAsync(settlement, VoucherAdjustments(ownerProfileId, computation, nowUtc), computation.AttachedAdjustmentIds,
                Audit(settlement, "Generated", userId, null, nowUtc), cancellationToken);
        }
        catch (SettlementWriteConflictException)
        {
            return Skipped(ownerProfileId, SettlementOutcomes.Conflict);
        }
        return Outcome(settlement, SettlementOutcomes.Created);
    }

    /// <summary>Tính lại kỳ Draft (Admin). Kỳ không còn Draft → 409; RowVersion lệch → 409.</summary>
    public async Task<OwnerSettlementOutcomeDto> RegenerateAsync(Settlement draft, byte[] expectedRowVersion, DateTime nowUtc, int? userId,
        IReadOnlyList<PayoutHoldItem>? holds, CancellationToken cancellationToken)
    {
        if (draft.Status != SettlementStatus.Draft)
            throw new ConflictException($"Kỳ quyết toán {draft.Code} đang ở trạng thái {draft.Status}, chỉ kỳ Draft được tính lại.");
        if (!expectedRowVersion.AsSpan().SequenceEqual(draft.RowVersion))
            throw new ConflictException("Dữ liệu kỳ quyết toán đã thay đổi, hãy tải lại.");

        var period = draft.Period ?? SettlementPeriod.Rehydrate(draft.PeriodStart, draft.PeriodEnd);
        var computation = await ComputeAsync(draft.OwnerProfileId, period, nowUtc, holds, draft.Id, cancellationToken);
        var before = Totals(draft);
        try
        {
            var saved = await repository.ReplaceDraftAsync(draft.Id, expectedRowVersion,
                tracked => tracked.ApplyComputation(computation, nowUtc, userId, holds is null ? null : nowUtc),
                VoucherAdjustments(draft.OwnerProfileId, computation, nowUtc), computation.AttachedAdjustmentIds,
                tracked => Audit(tracked, "Regenerated", userId, before, nowUtc), cancellationToken);
            return Outcome(saved, SettlementOutcomes.Regenerated);
        }
        catch (SettlementWriteConflictException)
        {
            throw new ConflictException($"Dữ liệu kỳ {draft.Code} vừa được ghi bởi thao tác khác, hãy tải lại.");
        }
    }

    /// <summary>Gom dữ liệu + xác minh booking + tính (không ghi).</summary>
    public async Task<SettlementComputation> ComputeAsync(int ownerProfileId, SettlementPeriod period, DateTime nowUtc,
        IReadOnlyList<PayoutHoldItem>? holds, int? excludeSettlementId, CancellationToken cancellationToken)
    {
        var cutoff = period.CutoffUtc;
        var snapshot = await repository.LoadSnapshotAsync(ownerProfileId, cutoff, nowUtc, excludeSettlementId, cancellationToken);

        var bookingCodes = snapshot.Payments.Where(NeedsBookingCheck).Select(p => p.BookingCode!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var statuses = bookingCodes.Count == 0
            ? new Dictionary<string, BookingStatusInfo>()
            : await bookings.GetStatusesAsync(bookingCodes, cancellationToken);

        var payments = snapshot.Payments.Select(p => Recognize(p, statuses, cutoff)).OfType<SettlementPaymentInput>().ToList();
        var included = payments.Select(p => p.PaymentId).ToHashSet();
        var refunds = snapshot.Refunds.Where(r => r.PaymentSettled || included.Contains(r.PaymentId))
            .Select(r => new SettlementRefundInput(r.RefundId, r.PaymentId, r.Amount, r.ProcessedAtUtc)).ToList();

        var openHolds = holds?.Where(h => h.OwnerProfileId == ownerProfileId)
            .Select(h => new OpenHoldInput(h.ComplaintCode, h.ParkingLotId, h.Amount)).ToList();

        return SettlementCalculator.Compute(new SettlementInput(cutoff, nowUtc, payments, snapshot.SettledPayments, refunds, snapshot.Accruals,
            snapshot.PriorRefunds, commissionPolicy.ExcludedPurposes, openHolds, snapshot.HeldElsewhere,
            [.. snapshot.Vouchers.Select(v => new VoucherInput(v.Code, v.Amount, v.IssuedByUserId, v.Status, v.UpdatedAtUtc))],
            snapshot.VoucherCharges, snapshot.Adjustments, snapshot.CarryOver));
    }

    /// <summary>Booking / Extension cần BookingService xác minh; thu tại cổng (WalkIn / Overstay) và vé tháng ghi nhận lúc thu tiền.</summary>
    public static bool RequiresBooking(PaymentPurpose purpose) => purpose is PaymentPurpose.Booking or PaymentPurpose.Extension;

    private static bool NeedsBookingCheck(UnsettledPaymentRow p) => RequiresBooking(p.Purpose) && !string.IsNullOrWhiteSpace(p.BookingCode);

    /// <summary>
    /// Giao dịch vào kỳ khi đã "hoàn tất" trước mốc chốt: thu tại cổng / vé tháng → lúc thu; booking → lúc booking chuyển sang trạng thái
    /// kết thúc. Booking chưa kết thúc / kết thúc sau mốc chốt → null (chờ kỳ sau). Không xác minh được → vào kỳ kèm lý do chặn.
    /// </summary>
    public static SettlementPaymentInput? Recognize(UnsettledPaymentRow p, IReadOnlyDictionary<string, BookingStatusInfo> statuses,
        DateTime cutoffUtc)
    {
        SettlementPaymentInput Input(DateTime at, string? blocked = null)
            => new(p.PaymentId, p.Code, p.BookingCode, p.ParkingLotId, p.Purpose, p.Method, p.Amount, at, blocked);

        if (!RequiresBooking(p.Purpose)) return Input(p.PaidAtUtc);
        if (string.IsNullOrWhiteSpace(p.BookingCode)) return Input(p.PaidAtUtc, SettlementBlockReasons.BookingCodeMissing);

        var key = statuses.Keys.FirstOrDefault(k => string.Equals(k, p.BookingCode.Trim(), StringComparison.OrdinalIgnoreCase));
        var status = key is null ? BookingStatusInfo.NotFound : statuses[key];
        if (!status.Found) return Input(p.PaidAtUtc, SettlementBlockReasons.BookingNotFound);
        if (status.Status is null || !TerminalBookingStatuses.Contains(status.Status)) return null;
        if (status.StatusSinceUtc is not { } since) return Input(p.PaidAtUtc, SettlementBlockReasons.BookingUnverifiable);
        var sinceUtc = DateTime.SpecifyKind(since, DateTimeKind.Utc);
        return sinceUtc < cutoffUtc ? Input(sinceUtc) : null;
    }

    // ===================== Ghi =====================

    public static IReadOnlyList<FinancialAdjustment> VoucherAdjustments(int ownerProfileId, SettlementComputation computation, DateTime nowUtc)
        => [.. computation.VoucherAdjustments.Select(v => v.IsReversal
            ? FinancialAdjustment.ReverseCompensationVoucher(ownerProfileId, v.VoucherCode, v.Amount, v.ReversesAdjustmentId, v.IssuedByUserId, nowUtc)
            : FinancialAdjustment.ForCompensationVoucher(ownerProfileId, v.VoucherCode, -v.Amount, v.IssuedByUserId, nowUtc))];

    /// <summary>AuditRecorded vào outbox cùng SaveChanges với kỳ (T-802). EntityId = mã kỳ (kỳ mới chưa có Id trước khi lưu).</summary>
    public static OutboxMessage Audit(Settlement settlement, string action, int? userId, object? before, DateTime nowUtc)
    {
        var evt = new AuditRecorded(AuditSource, userId, $"{AuditEntity}.{action}", AuditEntity, settlement.Code,
            before is null ? null : JsonSerializer.Serialize(before, Json), JsonSerializer.Serialize(Totals(settlement), Json), null)
        {
            OccurredAtUtc = nowUtc,
        };
        return new OutboxMessage
        {
            Id = evt.EventId,
            EventType = nameof(AuditRecorded),
            OccurredAtUtc = nowUtc,
            PayloadJson = JsonSerializer.Serialize(evt, Json),
        };
    }

    /// <summary>Ảnh chụp số tổng của kỳ cho audit (không có dữ liệu khách).</summary>
    public static object Totals(Settlement s) => new
    {
        s.Code, s.OwnerProfileId, s.PeriodStart, s.PeriodEnd, Status = s.Status.ToString(), s.GrossAmount, s.RefundAmount,
        s.OwnerCollectedAmount, s.CommissionAmount, s.HeldAmount, s.AdjustmentAmount, s.CarryOverAmount, s.NetPayout, s.IsBlocked,
        s.BlockedReasons, LineCount = s.Lines.Count,
    };

    public static OwnerSettlementOutcomeDto Outcome(Settlement s, string outcome)
        => new(s.OwnerProfileId, outcome, null, s.Id, s.Code, s.NetPayout, s.IsBlocked, SplitReasons(s.BlockedReasons),
            Convert.ToBase64String(s.RowVersion));

    public static IReadOnlyList<string> SplitReasons(string? reasons)
        => string.IsNullOrEmpty(reasons) ? [] : reasons.Split(',', StringSplitOptions.RemoveEmptyEntries);

    private static OwnerSettlementOutcomeDto Skipped(int ownerProfileId, string reason, int? settlementId = null)
        => new(ownerProfileId, SettlementOutcomes.Skipped, reason, settlementId, null, null, null, [], null);
}
