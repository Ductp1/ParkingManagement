using ParkingManagement.SharedKernel.Contracts;
using ParkingManagement.SharedKernel.Enums;
using ParkingManagement.SharedKernel.Exceptions;
using PaymentService.Domain.Entities;

namespace PaymentService.Application.Features.Reports;

// ===== DTO =====
/// <summary>US-078: tổng doanh thu trong khoảng lọc. Net = Gross − Refund (tiền hoàn tính vào kỳ thanh toán gốc).</summary>
public sealed record OwnerRevenueTotalsDto(decimal GrossRevenue, decimal RefundAmount, decimal NetRevenue, int TransactionCount);

public sealed record OwnerRevenueByLotDto(int ParkingLotId, decimal GrossRevenue, decimal RefundAmount, decimal NetRevenue,
    int TransactionCount);

/// <summary>Breakdown theo một giá trị enum (Method: Vnpay/Momo/VietQr/Cash; Purpose: Booking/Extension/Overstay/WalkIn/MonthlyPass).</summary>
public sealed record OwnerRevenueByKeyDto(string Key, decimal GrossRevenue, decimal RefundAmount, decimal NetRevenue, int TransactionCount);

/// <summary>Doanh thu theo giờ trong ngày (0–23, giờ Việt Nam) của các giao dịch trong khoảng lọc.</summary>
public sealed record OwnerRevenueByHourDto(int Hour, decimal GrossRevenue, decimal NetRevenue, int TransactionCount);

/// <summary>Hoàn tiền đã hoàn tất theo lý do (CustomerCancel ≈ tiền hoàn do khách hủy) của các giao dịch trong khoảng lọc.</summary>
public sealed record OwnerRefundByReasonDto(string Reason, int RefundCount, decimal Amount);

/// <summary>
/// Hoa hồng / khoản nhận về lấy từ kỳ quyết toán đã duyệt (Approved/Paid – US-080) nằm trọn trong khoảng lọc.
/// Chưa có kỳ nào → SettlementStatus "NotSettled", Commission/Payout = null (không ước tính – US-079 tính hoa hồng).
/// Lọc theo bãi → Commission cộng từ SettlementLine của bãi, Payout = null (kỳ quyết toán tính theo chủ bãi, không theo bãi).
/// </summary>
public sealed record OwnerSettlementSummaryDto(string SettlementStatus, int SettledPeriodCount, DateOnly? SettledFrom, DateOnly? SettledTo,
    decimal? CommissionAmount, decimal? PayoutAmount);

/// <summary>US-078 AC1: báo cáo doanh thu của chủ bãi đang đăng nhập. From bao gồm, To loại trừ (ngày theo giờ Việt Nam).</summary>
public sealed record OwnerRevenueSummaryDto(DateOnly From, DateOnly To, int? ParkingLotId, string Currency, string RefundBasis,
    OwnerRevenueTotalsDto Totals, IReadOnlyList<OwnerRevenueByLotDto> ByLot, IReadOnlyList<OwnerRevenueByKeyDto> ByMethod,
    IReadOnlyList<OwnerRevenueByKeyDto> ByPurpose, IReadOnlyList<OwnerRevenueByHourDto> ByHourOfDay,
    IReadOnlyList<OwnerRefundByReasonDto> ByRefundReason, OwnerSettlementSummaryDto Settlement);

/// <summary>Tham số lọc nhận từ query string (chưa kiểm tra). Không có ownerProfileId: chủ bãi luôn lấy từ token.</summary>
public sealed record OwnerRevenueQuery(DateOnly? From = null, DateOnly? To = null, int? ParkingLotId = null);

/// <summary>Bộ lọc đã kiểm tra: FromUtc ≤ PaidAtUtc &lt; ToUtc, luôn kèm OwnerProfileId của token.</summary>
public sealed record OwnerRevenueFilter(int OwnerProfileId, int? ParkingLotId, DateOnly From, DateOnly To)
{
    public DateTime FromUtc => OwnerRevenueRules.StartOfIctDayUtc(From);
    public DateTime ToUtc => OwnerRevenueRules.StartOfIctDayUtc(To);
}

// ===== PORT (đọc) – dòng đã GROUP BY / SUM ở database =====
public sealed record OwnerRevenueGroupRow(int ParkingLotId, PaymentMethod Method, PaymentPurpose Purpose, decimal GrossAmount,
    decimal RefundedAmount, int TransactionCount);

public sealed record OwnerRevenueHourRow(int HourOfDay, decimal GrossAmount, decimal RefundedAmount, int TransactionCount);

public sealed record OwnerRefundReasonRow(RefundReason Reason, int RefundCount, decimal Amount);

/// <summary>Một kỳ quyết toán đã duyệt; CommissionAmount là của cả kỳ hoặc chỉ các dòng của bãi đang lọc.</summary>
public sealed record OwnerSettlementRow(DateOnly PeriodStart, DateOnly PeriodEnd, decimal CommissionAmount, decimal NetPayout);

/// <summary>Doanh thu của một ngày theo lịch Việt Nam (năm/tháng/ngày của PaidAtUtc + 7h).</summary>
public sealed record OwnerRevenueDayRow(int Year, int Month, int Day, decimal GrossAmount, decimal RefundedAmount, int TransactionCount);

/// <summary>Doanh thu của một giờ trong một ngày theo lịch Việt Nam.</summary>
public sealed record OwnerRevenueDayHourRow(int Year, int Month, int Day, int HourOfDay, decimal GrossAmount, decimal RefundedAmount,
    int TransactionCount);

public interface IOwnerRevenueQueries
{
    /// <summary>Doanh thu theo (bãi, phương thức, mục đích) – GROUP BY ở database.</summary>
    Task<IReadOnlyList<OwnerRevenueGroupRow>> SumByLotMethodPurposeAsync(OwnerRevenueFilter filter, CancellationToken cancellationToken);

    /// <summary>Doanh thu theo giờ trong ngày (giờ Việt Nam) – GROUP BY ở database.</summary>
    Task<IReadOnlyList<OwnerRevenueHourRow>> SumByHourOfDayAsync(OwnerRevenueFilter filter, CancellationToken cancellationToken);

    /// <summary>Refund Completed của các giao dịch trong bộ lọc, theo lý do – GROUP BY ở database.</summary>
    Task<IReadOnlyList<OwnerRefundReasonRow>> SumCompletedRefundsByReasonAsync(OwnerRevenueFilter filter, CancellationToken cancellationToken);

    /// <summary>Kỳ quyết toán Approved/Paid của chủ bãi nằm trọn trong khoảng lọc (tối đa ~53 kỳ tuần / 366 ngày).</summary>
    Task<IReadOnlyList<OwnerSettlementRow>> ListSettledPeriodsAsync(OwnerRevenueFilter filter, CancellationToken cancellationToken);

    /// <summary>Doanh thu theo ngày Việt Nam (tối đa 366 dòng) – GROUP BY ở database; tuần/tháng gộp tiếp từ các dòng này.</summary>
    Task<IReadOnlyList<OwnerRevenueDayRow>> SumByVietnamDayAsync(OwnerRevenueFilter filter, CancellationToken cancellationToken);

    /// <summary>Doanh thu theo (ngày, giờ) Việt Nam (tối đa 7 × 24 dòng) – GROUP BY ở database.</summary>
    Task<IReadOnlyList<OwnerRevenueDayHourRow>> SumByVietnamDayHourAsync(OwnerRevenueFilter filter, CancellationToken cancellationToken);

    /// <summary>Giao dịch theo bộ lọc, mới nhất trước (PaidAtUtc giảm dần, Id giảm dần) – lọc, đếm, phân trang ở database.</summary>
    Task<PagedResult<OwnerRevenueTransactionRow>> ListTransactionsAsync(OwnerRevenueTransactionFilter filter, int page, int pageSize,
        CancellationToken cancellationToken);

    /// <summary>Tối đa maxRows giao dịch theo bộ lọc, mới nhất trước (xuất CSV).</summary>
    Task<IReadOnlyList<OwnerRevenueTransactionRow>> ListTransactionsForExportAsync(OwnerRevenueTransactionFilter filter, int maxRows,
        CancellationToken cancellationToken);
}

/// <summary>US-078: quy tắc dùng chung cho mọi báo cáo doanh thu chủ bãi (summary, series, giao dịch, CSV).</summary>
public static class OwnerRevenueRules
{
    public const int MaxRangeDays = 366;
    public const string Currency = "VND";
    /// <summary>Tiền hoàn được tính vào kỳ của giao dịch gốc (theo PaidAtUtc), không theo ngày hoàn.</summary>
    public const string RefundBasis = "OriginalPaymentPeriod";
    public const string NotSettled = "NotSettled";
    public const string Settled = "Settled";

    public static readonly TimeSpan IctOffset = TimeSpan.FromHours(7);   // Việt Nam không có giờ mùa hè

    /// <summary>Trạng thái được tính doanh thu: đã thu tiền (kể cả đã hoàn một phần / toàn bộ).</summary>
    public static readonly PaymentStatus[] RevenueStatuses =
        [PaymentStatus.Succeeded, PaymentStatus.PartiallyRefunded, PaymentStatus.Refunded];

    public static DateTime StartOfIctDayUtc(DateOnly day) => day.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc) - IctOffset;

    /// <summary>
    /// Kiểm tra + chuẩn hóa. Thiếu cả from/to → tháng hiện tại theo giờ Việt Nam; thiếu một đầu → khoảng 1 tháng.
    /// Chủ bãi không gắn hồ sơ (thiếu claim OwnerProfileId) → 403.
    /// </summary>
    public static OwnerRevenueFilter Normalize(int? ownerProfileId, OwnerRevenueQuery query, DateTime nowUtc)
    {
        if (ownerProfileId is not > 0) throw new ForbiddenException("Tài khoản chưa gắn với một hồ sơ chủ bãi.");
        if (query.ParkingLotId is <= 0) throw new ValidationException("parkingLotId phải là số nguyên dương.");

        var todayIct = DateOnly.FromDateTime(nowUtc + IctOffset);
        var monthStart = new DateOnly(todayIct.Year, todayIct.Month, 1);
        var (from, to) = (query.From, query.To) switch
        {
            ({ } f, { } t) => (f, t),
            ({ } f, null) => (f, f.AddMonths(1)),
            (null, { } t) => (t.AddMonths(-1), t),
            _ => (monthStart, monthStart.AddMonths(1)),
        };
        if (from >= to) throw new ValidationException("from phải nhỏ hơn to.");
        if (to.DayNumber - from.DayNumber > MaxRangeDays)
            throw new ValidationException($"Khoảng thời gian báo cáo tối đa {MaxRangeDays} ngày.");

        return new OwnerRevenueFilter(ownerProfileId.Value, query.ParkingLotId, from, to);
    }

    /// <summary>
    /// Giao dịch được tính doanh thu của đúng chủ bãi trong token (+ bãi nếu có lọc). Bãi của chủ bãi khác → không có dòng nào.
    /// Dùng chung cho truy vấn EF (dịch sang SQL) và test chạy trong bộ nhớ.
    /// </summary>
    public static IQueryable<Payment> WhereRevenue(this IQueryable<Payment> payments, OwnerRevenueFilter filter)
    {
        var (ownerProfileId, fromUtc, toUtc) = (filter.OwnerProfileId, filter.FromUtc, filter.ToUtc);
        // Viết rõ 3 trạng thái (khớp RevenueStatuses): tránh array.Contains bị C# 14 gắn sang overload Span trong expression tree.
        var query = payments.Where(p => p.OwnerProfileId == ownerProfileId
            && (p.Status == PaymentStatus.Succeeded || p.Status == PaymentStatus.PartiallyRefunded || p.Status == PaymentStatus.Refunded)
            && p.PaidAtUtc >= fromUtc && p.PaidAtUtc < toUtc);
        if (filter.ParkingLotId is { } lotId)
            query = query.Where(p => p.ParkingLotId == lotId);
        return query;
    }

    /// <summary>Số tiền VND không có phần lẻ.</summary>
    public static decimal Vnd(decimal amount) => Math.Round(amount, 0, MidpointRounding.AwayFromZero);
}

// ===== USE CASE (đọc) =====
public interface IGetOwnerRevenueSummaryUseCase
{
    Task<OwnerRevenueSummaryDto> ExecuteAsync(int? ownerProfileId, OwnerRevenueQuery query, CancellationToken cancellationToken = default);
}

/// <summary>
/// US-078 (UC-35) AC1–AC2, AC4: chủ bãi xem doanh thu gộp, tiền hoàn, doanh thu ròng, số giao dịch; chia theo bãi, phương thức,
/// mục đích, giờ trong ngày, lý do hoàn; kèm hoa hồng/khoản nhận về nếu đã quyết toán. Chỉ đọc.
/// </summary>
public sealed class GetOwnerRevenueSummaryUseCase(IOwnerRevenueQueries queries, TimeProvider timeProvider) : IGetOwnerRevenueSummaryUseCase
{
    public async Task<OwnerRevenueSummaryDto> ExecuteAsync(int? ownerProfileId, OwnerRevenueQuery query,
        CancellationToken cancellationToken = default)
    {
        var filter = OwnerRevenueRules.Normalize(ownerProfileId, query, timeProvider.GetUtcNow().UtcDateTime);

        var groups = await queries.SumByLotMethodPurposeAsync(filter, cancellationToken);
        var hours = await queries.SumByHourOfDayAsync(filter, cancellationToken);
        var refunds = await queries.SumCompletedRefundsByReasonAsync(filter, cancellationToken);
        var settlements = await queries.ListSettledPeriodsAsync(filter, cancellationToken);

        var totals = Totals(groups);
        var byLot = groups.GroupBy(g => g.ParkingLotId)
            .Select(g => (Lot: g.Key, Totals: Totals(g)))
            .OrderByDescending(x => x.Totals.GrossRevenue).ThenBy(x => x.Lot)
            .Select(x => new OwnerRevenueByLotDto(x.Lot, x.Totals.GrossRevenue, x.Totals.RefundAmount, x.Totals.NetRevenue,
                x.Totals.TransactionCount))
            .ToList();
        var byMethod = Enum.GetValues<PaymentMethod>().Select(m => ByKey(m.ToString(), groups.Where(g => g.Method == m))).ToList();
        var byPurpose = Enum.GetValues<PaymentPurpose>().Select(p => ByKey(p.ToString(), groups.Where(g => g.Purpose == p))).ToList();

        // Đủ 24 giờ (kể cả giờ không có giao dịch) để FE vẽ biểu đồ không phải tự lấp.
        var byHour = Enumerable.Range(0, 24).Select(h =>
        {
            var rows = hours.Where(r => r.HourOfDay == h).ToList();
            var gross = rows.Sum(r => r.GrossAmount);
            return new OwnerRevenueByHourDto(h, OwnerRevenueRules.Vnd(gross), OwnerRevenueRules.Vnd(gross - rows.Sum(r => r.RefundedAmount)),
                rows.Sum(r => r.TransactionCount));
        }).ToList();

        var byRefundReason = Enum.GetValues<RefundReason>().Select(reason =>
        {
            var rows = refunds.Where(r => r.Reason == reason).ToList();
            return new OwnerRefundByReasonDto(reason.ToString(), rows.Sum(r => r.RefundCount), OwnerRevenueRules.Vnd(rows.Sum(r => r.Amount)));
        }).ToList();

        return new OwnerRevenueSummaryDto(filter.From, filter.To, filter.ParkingLotId, OwnerRevenueRules.Currency,
            OwnerRevenueRules.RefundBasis, totals, byLot, byMethod, byPurpose, byHour, byRefundReason,
            Settlement(settlements, filter.ParkingLotId is not null));
    }

    private static OwnerRevenueTotalsDto Totals(IEnumerable<OwnerRevenueGroupRow> rows)
    {
        var list = rows as IReadOnlyCollection<OwnerRevenueGroupRow> ?? rows.ToList();
        var gross = list.Sum(r => r.GrossAmount);
        var refund = list.Sum(r => r.RefundedAmount);
        return new OwnerRevenueTotalsDto(OwnerRevenueRules.Vnd(gross), OwnerRevenueRules.Vnd(refund), OwnerRevenueRules.Vnd(gross - refund),
            list.Sum(r => r.TransactionCount));
    }

    private static OwnerRevenueByKeyDto ByKey(string key, IEnumerable<OwnerRevenueGroupRow> rows)
    {
        var t = Totals(rows);
        return new OwnerRevenueByKeyDto(key, t.GrossRevenue, t.RefundAmount, t.NetRevenue, t.TransactionCount);
    }

    private static OwnerSettlementSummaryDto Settlement(IReadOnlyList<OwnerSettlementRow> periods, bool filteredByLot)
    {
        if (periods.Count == 0)
            return new OwnerSettlementSummaryDto(OwnerRevenueRules.NotSettled, 0, null, null, null, null);

        return new OwnerSettlementSummaryDto(OwnerRevenueRules.Settled, periods.Count,
            periods.Min(p => p.PeriodStart), periods.Max(p => p.PeriodEnd),
            OwnerRevenueRules.Vnd(periods.Sum(p => p.CommissionAmount)),
            filteredByLot ? null : OwnerRevenueRules.Vnd(periods.Sum(p => p.NetPayout)));
    }
}
