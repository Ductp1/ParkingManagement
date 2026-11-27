using System.Globalization;
using System.Text;
using ParkingManagement.SharedKernel.Contracts;
using ParkingManagement.SharedKernel.Domain;
using ParkingManagement.SharedKernel.Enums;
using ParkingManagement.SharedKernel.Exceptions;
using PaymentService.Domain.Entities;

namespace PaymentService.Application.Features.Settlements;

// ===== Cấu hình =====

/// <summary>"Settlements:RequireDifferentApprover" (mặc định true): người gửi / tạo không tự duyệt kỳ hoặc bút toán (4 mắt).</summary>
public sealed record SettlementReviewOptions(bool RequireDifferentApprover = true);

// ===== DTO (không có dữ liệu cá nhân của khách: chỉ mã booking / giao dịch / khiếu nại và số tiền) =====

public sealed record SettlementSummaryDto(int Id, string Code, int OwnerProfileId, DateOnly PeriodStart, DateOnly PeriodEnd, string? Cycle,
    decimal GrossAmount, decimal RefundAmount, decimal OwnerCollectedAmount, decimal CommissionAmount, decimal CommissionRate, decimal HeldAmount,
    decimal AdjustmentAmount, decimal CarryOverAmount, decimal NetPayout, string Status, bool IsBlocked, IReadOnlyList<string> BlockedReasons,
    DateTime GeneratedAtUtc, DateTime? SubmittedAtUtc, DateTime? ApprovedAtUtc, DateTime? PaidAtUtc, string? PayoutReference, string RowVersion);

/// <summary>Các thành phần của công thức NetPayout (giao diện hiển thị từng bước).</summary>
public sealed record SettlementFormulaDto(decimal Gross, decimal Refund, decimal OwnerCollected, decimal Commission, decimal Held,
    decimal Adjustment, decimal CarryOver, decimal NetPayout, string Expression)
{
    public const string Text = "NetPayout = Gross − Refund − OwnerCollected − Commission − Held + Adjustment + CarryOver";
}

/// <summary>Mốc trạng thái: Event = Generated | Submitted | Rejected | Approved | Paid; ByUserId = null khi do hệ thống.</summary>
public sealed record SettlementTimelineEventDto(string Event, DateTime AtUtc, int? ByUserId, string? Note);

public sealed record SettlementLineTotalDto(string LineType, int Count, decimal Amount, decimal CommissionAmount);

public sealed record SettlementLineDto(int Id, string LineType, int? PaymentId, int? RefundId, int ParkingLotId, string? ReferenceCode,
    decimal Amount, decimal CommissionAmount, bool IsOwnerCollected, DateTime? OccurredAtUtc, string? BlockedReason);

public sealed record FinancialAdjustmentDto(int Id, int OwnerProfileId, int? SettlementId, string? SettlementCode, decimal Amount, string Reason,
    string Status, string SourceType, string? SourceReference, int? ReversesAdjustmentId, int CreatedByUserId, DateTime CreatedAtUtc,
    int? ApprovedByUserId, DateTime? ApprovedAtUtc, int? RejectedByUserId, DateTime? RejectedAtUtc, string? RejectReason, string RowVersion);

public sealed record SettlementDetailDto(SettlementSummaryDto Settlement, SettlementFormulaDto Formula, IReadOnlyList<SettlementTimelineEventDto> Timeline,
    IReadOnlyList<SettlementLineTotalDto> LineTotals, IReadOnlyList<FinancialAdjustmentDto> Adjustments, IReadOnlyList<string> AllowedActions);

public sealed record SettlementStatsDto(int Draft, int PendingReview, int Approved, int Paid, int Blocked, decimal PendingReviewNetPayout,
    decimal AwaitingPayoutNetPayout);

public sealed record SettlementPeriodDto(int Id, DateOnly PeriodStart, DateOnly PeriodEnd, string Cycle, bool HasEnded, int SettlementCount);

// ----- Chủ bãi: không có Id Admin, không có lý do chặn nội bộ -----

public sealed record OwnerSettlementDto(int Id, string Code, DateOnly PeriodStart, DateOnly PeriodEnd, decimal GrossAmount, decimal RefundAmount,
    decimal OwnerCollectedAmount, decimal CommissionAmount, decimal HeldAmount, decimal AdjustmentAmount, decimal CarryOverAmount,
    decimal NetPayout, string Status, DateTime? ApprovedAtUtc, DateTime? PaidAtUtc, string? PayoutReference);

public sealed record OwnerAdjustmentDto(int Id, decimal Amount, string Reason, string SourceType, DateTime CreatedAtUtc);

public sealed record OwnerSettlementDetailDto(OwnerSettlementDto Settlement, SettlementFormulaDto Formula, IReadOnlyList<SettlementLineTotalDto> LineTotals,
    IReadOnlyList<OwnerAdjustmentDto> Adjustments);

public sealed record SettlementCsvFile(string FileName, byte[] Content)
{
    public const string ContentType = "text/csv; charset=utf-8";
    public const int MaxRows = 10_000;
}

// ===== Bộ lọc =====

/// <summary>Search: số nguyên = chủ bãi, chuỗi khác = một phần mã kỳ (STL-…). Statuses (chủ bãi) giới hạn trạng thái được thấy.</summary>
public sealed record SettlementListFilter(SettlementStatus? Status = null, int? OwnerProfileId = null, DateOnly? PeriodStart = null,
    string? Search = null, bool? Blocked = null, IReadOnlyCollection<SettlementStatus>? Statuses = null);

/// <summary>Attached: true = đã gắn kỳ, false = chưa gắn. Search: lý do / mã nguồn.</summary>
public sealed record AdjustmentListFilter(int? OwnerProfileId = null, FinancialAdjustmentStatus? Status = null, bool? Attached = null,
    string? Search = null, int? SettlementId = null);

// ===== PORT đọc (AsNoTracking, lọc / đếm / tổng ở database) =====

public interface ISettlementQueries
{
    Task<PagedResult<SettlementSummaryDto>> ListAsync(SettlementListFilter filter, int page, int pageSize, CancellationToken cancellationToken);
    /// <summary>Tối đa <paramref name="take"/> dòng theo cùng thứ tự danh sách (để xuất CSV).</summary>
    Task<IReadOnlyList<SettlementSummaryDto>> ListAllAsync(SettlementListFilter filter, int take, CancellationToken cancellationToken);
    Task<SettlementStatsDto> GetStatsAsync(DateOnly? periodStart, CancellationToken cancellationToken);
    Task<Settlement?> FindAsync(int settlementId, CancellationToken cancellationToken);
    Task<IReadOnlyList<SettlementLineTotalDto>> SumLinesByTypeAsync(int settlementId, CancellationToken cancellationToken);
    Task<PagedResult<SettlementLineDto>> ListLinesAsync(int settlementId, SettlementLineType? lineType, int page, int pageSize,
        CancellationToken cancellationToken);
    Task<IReadOnlyList<SettlementLineDto>> ListAllLinesAsync(int settlementId, int take, CancellationToken cancellationToken);
    Task<IReadOnlyList<SettlementPeriodDto>> ListPeriodsAsync(int take, DateTime nowUtc, CancellationToken cancellationToken);
    Task<PagedResult<FinancialAdjustmentDto>> ListAdjustmentsAsync(AdjustmentListFilter filter, int page, int pageSize,
        CancellationToken cancellationToken);
}

// ===== PORT ghi =====

public interface ISettlementReviewRepository
{
    Task<Settlement?> FindTrackedAsync(int settlementId, CancellationToken cancellationToken);
    /// <summary>Lưu chuyển trạng thái + audit trong một SaveChanges; RowVersion khác bản Admin đã đọc → ConflictException.</summary>
    Task SaveAsync(Settlement settlement, byte[] expectedRowVersion, OutboxMessage audit, CancellationToken cancellationToken);

    Task<FinancialAdjustment?> FindAdjustmentTrackedAsync(int adjustmentId, CancellationToken cancellationToken);
    /// <summary>Đã có bút toán (chưa bị từ chối) đảo bút toán này.</summary>
    Task<bool> IsReversedAsync(int adjustmentId, CancellationToken cancellationToken);
    /// <summary>Thêm bút toán rồi audit (cần Id) trong một transaction.</summary>
    Task AddAdjustmentAsync(FinancialAdjustment adjustment, Func<FinancialAdjustment, OutboxMessage> audit, CancellationToken cancellationToken);
    Task SaveAdjustmentAsync(FinancialAdjustment adjustment, byte[] expectedRowVersion, OutboxMessage audit, CancellationToken cancellationToken);
}

// ===== Quy tắc dùng chung =====

public static class SettlementReviewRules
{
    public const int MaxPageSize = 100;
    public static readonly IReadOnlyCollection<SettlementStatus> OwnerVisibleStatuses = [SettlementStatus.Approved, SettlementStatus.Paid];

    public static void EnsurePaging(int page, int pageSize)
    {
        if (page < 1) throw new ValidationException("page phải ≥ 1.");
        if (pageSize is < 1 or > MaxPageSize) throw new ValidationException($"pageSize phải trong khoảng 1–{MaxPageSize}.");
    }

    public static T? ParseEnum<T>(string? value, string name) where T : struct, Enum
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        return Enum.TryParse<T>(value.Trim(), ignoreCase: true, out var parsed) && Enum.IsDefined(parsed)
            ? parsed
            : throw new ValidationException($"{name} phải là một trong: {string.Join(", ", Enum.GetNames<T>())}.");
    }

    public static string? NormalizeSearch(string? search)
    {
        var term = string.IsNullOrWhiteSpace(search) ? null : search.Trim();
        if (term is { Length: > 100 }) throw new ValidationException("Từ khóa tìm kiếm tối đa 100 ký tự.");
        return term;
    }

    public static SettlementFormulaDto Formula(Settlement s)
        => new(s.GrossAmount, s.RefundAmount, s.OwnerCollectedAmount, s.CommissionAmount, s.HeldAmount, s.AdjustmentAmount, s.CarryOverAmount,
            s.NetPayout, SettlementFormulaDto.Text);

    public static IReadOnlyList<SettlementTimelineEventDto> Timeline(Settlement s)
    {
        var events = new List<SettlementTimelineEventDto> { new("Generated", s.GeneratedAtUtc, s.GeneratedByUserId, null) };
        if (s.SubmittedAtUtc is { } submitted) events.Add(new("Submitted", submitted, s.SubmittedByUserId, null));
        if (s.RejectedAtUtc is { } rejected) events.Add(new("Rejected", rejected, s.RejectedByUserId, s.RejectReason));
        if (s.ApprovedAtUtc is { } approved) events.Add(new("Approved", approved, s.ApprovedByUserId, null));
        if (s.PaidAtUtc is { } paid) events.Add(new("Paid", paid, s.PaidByUserId, s.PayoutReference));
        return [.. events.OrderBy(e => e.AtUtc)];
    }

    public static SettlementSummaryDto Summary(Settlement s)
        => new(s.Id, s.Code, s.OwnerProfileId, s.PeriodStart, s.PeriodEnd, s.Period?.Cycle, s.GrossAmount, s.RefundAmount, s.OwnerCollectedAmount,
            s.CommissionAmount, s.CommissionRate, s.HeldAmount, s.AdjustmentAmount, s.CarryOverAmount, s.NetPayout, s.Status.ToString(), s.IsBlocked,
            SettlementBuilder.SplitReasons(s.BlockedReasons), s.GeneratedAtUtc, s.SubmittedAtUtc, s.ApprovedAtUtc, s.PaidAtUtc, s.PayoutReference,
            Convert.ToBase64String(s.RowVersion));

    public static OwnerSettlementDto ForOwner(Settlement s)
        => new(s.Id, s.Code, s.PeriodStart, s.PeriodEnd, s.GrossAmount, s.RefundAmount, s.OwnerCollectedAmount, s.CommissionAmount, s.HeldAmount,
            s.AdjustmentAmount, s.CarryOverAmount, s.NetPayout, s.Status.ToString(), s.ApprovedAtUtc, s.PaidAtUtc, s.PayoutReference);

    public static OwnerSettlementDto ForOwner(SettlementSummaryDto s)
        => new(s.Id, s.Code, s.PeriodStart, s.PeriodEnd, s.GrossAmount, s.RefundAmount, s.OwnerCollectedAmount, s.CommissionAmount, s.HeldAmount,
            s.AdjustmentAmount, s.CarryOverAmount, s.NetPayout, s.Status, s.ApprovedAtUtc, s.PaidAtUtc, s.PayoutReference);

    public static FinancialAdjustmentDto Adjustment(FinancialAdjustment a, string? settlementCode = null)
        => new(a.Id, a.OwnerProfileId, a.SettlementId, settlementCode ?? a.Settlement?.Code, a.Amount, a.Reason, a.Status.ToString(),
            a.SourceType.ToString(), a.SourceReference, a.ReversesAdjustmentId, a.CreatedByUserId, a.CreatedAtUtc, a.ApprovedByUserId,
            a.ApprovedAtUtc, a.RejectedByUserId, a.RejectedAtUtc, a.RejectReason, Convert.ToBase64String(a.RowVersion));

    /// <summary>AuditRecorded của bút toán điều chỉnh (outbox, cùng transaction với dữ liệu).</summary>
    public static OutboxMessage AdjustmentAudit(FinancialAdjustment a, string action, int userId, object? before, DateTime nowUtc)
    {
        var evt = new AuditRecorded(SettlementBuilder.AuditSource, userId, $"FinancialAdjustment.{action}", "FinancialAdjustment",
            a.Id.ToString(CultureInfo.InvariantCulture), before is null ? null : System.Text.Json.JsonSerializer.Serialize(before, Json),
            System.Text.Json.JsonSerializer.Serialize(new
            {
                a.Id, a.OwnerProfileId, a.Amount, a.Reason, Status = a.Status.ToString(), SourceType = a.SourceType.ToString(), a.SourceReference,
                a.ReversesAdjustmentId, a.SettlementId,
            }, Json), action == "Rejected" ? a.RejectReason : null)
        {
            OccurredAtUtc = nowUtc,
        };
        return new OutboxMessage { Id = evt.EventId, EventType = nameof(AuditRecorded), OccurredAtUtc = nowUtc,
            PayloadJson = System.Text.Json.JsonSerializer.Serialize(evt, Json) };
    }

    private static readonly System.Text.Json.JsonSerializerOptions Json = new(System.Text.Json.JsonSerializerDefaults.Web);
}

/// <summary>
/// CSV kỳ quyết toán (RFC 4180, UTF-8 có BOM, CRLF, chống CSV injection – cùng quy ước US-078 / US-100). Hàm thuần.
/// </summary>
public static class SettlementCsvWriter
{
    private static readonly TimeSpan Ict = TimeSpan.FromHours(7);

    public static readonly string[] SettlementHeader =
    [
        "Code", "OwnerProfileId", "PeriodStart", "PeriodEnd", "Status", "GrossAmount", "RefundAmount", "OwnerCollectedAmount",
        "CommissionAmount", "HeldAmount", "AdjustmentAmount", "CarryOverAmount", "NetPayout", "IsBlocked", "BlockedReasons",
        "ApprovedAtUtc", "PaidAtUtc", "PayoutReference",
    ];

    public static readonly string[] LineHeader =
    [
        "SettlementCode", "LineType", "ReferenceCode", "ParkingLotId", "PaymentId", "RefundId", "OccurredAtUtc", "OccurredAtIct",
        "Amount", "CommissionAmount", "OwnerCollected", "BlockedReason",
    ];

    public static byte[] Settlements(IEnumerable<SettlementSummaryDto> rows)
        => Write(SettlementHeader, rows.Select(r => new[]
        {
            Text(r.Code), Int(r.OwnerProfileId), Date(r.PeriodStart), Date(r.PeriodEnd), r.Status, Money(r.GrossAmount), Money(r.RefundAmount),
            Money(r.OwnerCollectedAmount), Money(r.CommissionAmount), Money(r.HeldAmount), Money(r.AdjustmentAmount), Money(r.CarryOverAmount),
            Money(r.NetPayout), r.IsBlocked ? "true" : "false", string.Join(";", r.BlockedReasons), Utc(r.ApprovedAtUtc), Utc(r.PaidAtUtc),
            Text(r.PayoutReference),
        }));

    public static byte[] Lines(string settlementCode, IEnumerable<SettlementLineDto> rows)
        => Write(LineHeader, rows.Select(r => new[]
        {
            Text(settlementCode), r.LineType, Text(r.ReferenceCode), Int(r.ParkingLotId), r.PaymentId?.ToString(CultureInfo.InvariantCulture),
            r.RefundId?.ToString(CultureInfo.InvariantCulture), Utc(r.OccurredAtUtc),
            r.OccurredAtUtc is { } at ? (at + Ict).ToString("dd/MM/yyyy HH:mm:ss", CultureInfo.InvariantCulture) : null,
            Money(r.Amount), Money(r.CommissionAmount), r.IsOwnerCollected ? "true" : "false", r.BlockedReason,
        }));

    private static byte[] Write(string[] header, IEnumerable<string?[]> rows)
    {
        var csv = new StringBuilder();
        AppendLine(csv, header);
        foreach (var row in rows) AppendLine(csv, row);
        return [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes(csv.ToString())];
    }

    private static string Money(decimal amount) => amount.ToString("0", CultureInfo.InvariantCulture);
    private static string Int(int value) => value.ToString(CultureInfo.InvariantCulture);
    private static string Date(DateOnly day) => day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    private static string? Utc(DateTime? at) => at?.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);

    /// <summary>Ô chữ do người dùng / service khác nhập → chặn công thức.</summary>
    private static string? Text(string? value)
        => value is { Length: > 0 } && value[0] is '=' or '+' or '-' or '@' or '\t' or '\r' ? "'" + value : value;

    private static void AppendLine(StringBuilder csv, IEnumerable<string?> cells)
    {
        csv.AppendJoin(',', cells.Select(Escape));
        csv.Append("\r\n");
    }

    private static string Escape(string? cell)
        => cell is null ? string.Empty
            : cell.IndexOfAny([',', '"', '\r', '\n']) >= 0 ? $"\"{cell.Replace("\"", "\"\"")}\"" : cell;
}
