using System.Globalization;
using System.Text;
using ParkingManagement.SharedKernel.Contracts;
using ParkingManagement.SharedKernel.Enums;
using ParkingManagement.SharedKernel.Exceptions;

namespace PaymentService.Application.Features.Reports;

// ===== DTO =====
/// <summary>
/// US-078 AC3: một giao dịch trong báo cáo doanh thu của chủ bãi. Không có dữ liệu tài xế (UserId), nội dung chuyển khoản,
/// payload callback hay nhân viên thu tiền; mã giao dịch cổng thanh toán chỉ giữ 4 ký tự cuối (****1234) để đối soát.
/// </summary>
public sealed record OwnerRevenueTransactionDto(int Id, string Code, DateTime PaidAtUtc, int ParkingLotId, string? BookingCode,
    string Method, string Purpose, string Status, decimal Amount, decimal RefundedAmount, decimal NetAmount, string? ProviderTransactionRef);

/// <summary>Tham số lọc nhận từ query string (chưa kiểm tra). method/purpose/status là tên enum; bỏ trống = không lọc.</summary>
public sealed record OwnerRevenueTransactionQuery(DateOnly? From = null, DateOnly? To = null, int? ParkingLotId = null,
    string? Method = null, string? Purpose = null, string? Status = null);

/// <summary>Bộ lọc đã kiểm tra: khoảng thời gian + chủ bãi của token (+ bãi) và các bộ lọc thêm.</summary>
public sealed record OwnerRevenueTransactionFilter(OwnerRevenueFilter Range, PaymentMethod? Method = null, PaymentPurpose? Purpose = null,
    PaymentStatus? Status = null);

/// <summary>Dòng đọc từ database (ProviderTransactionId còn nguyên – use case che trước khi trả ra ngoài).</summary>
public sealed record OwnerRevenueTransactionRow(int Id, string Code, DateTime PaidAtUtc, int ParkingLotId, string? BookingCode,
    PaymentMethod Method, PaymentPurpose Purpose, PaymentStatus Status, decimal Amount, decimal RefundedAmount, string? ProviderTransactionId);

/// <summary>File CSV trả về cho trình duyệt tải xuống.</summary>
public sealed record OwnerRevenueCsvFile(string FileName, byte[] Content)
{
    public const string ContentType = "text/csv; charset=utf-8";
}

/// <summary>US-078: kiểm tra tham số dùng chung cho danh sách giao dịch và xuất CSV.</summary>
public static class OwnerRevenueTransactionRules
{
    public const int MaxPageSize = 100;

    public static OwnerRevenueTransactionFilter Normalize(int? ownerProfileId, OwnerRevenueTransactionQuery query, DateTime nowUtc)
    {
        var range = OwnerRevenueRules.Normalize(ownerProfileId, new OwnerRevenueQuery(query.From, query.To, query.ParkingLotId), nowUtc);
        return new OwnerRevenueTransactionFilter(range,
            Parse("method", query.Method, Enum.GetValues<PaymentMethod>()),
            Parse("purpose", query.Purpose, Enum.GetValues<PaymentPurpose>()),
            Parse("status", query.Status, OwnerRevenueRules.RevenueStatuses));   // chỉ trạng thái được tính doanh thu
    }

    public static void ValidatePaging(int page, int pageSize)
    {
        if (page < 1) throw new ValidationException("page phải ≥ 1.");
        if (pageSize is < 1 or > MaxPageSize) throw new ValidationException($"pageSize phải trong khoảng 1–{MaxPageSize}.");
        if ((long)(page - 1) * pageSize > int.MaxValue) throw new ValidationException("page quá lớn.");   // Skip của EF là int
    }

    /// <summary>"VNP14500001" → "****0001"; ≤ 4 ký tự → "****" (không lộ cả mã); rỗng → null.</summary>
    public static string? MaskProviderTransactionId(string? value)
        => string.IsNullOrWhiteSpace(value) ? null
            : value.Length <= 4 ? "****"
            : "****" + value[^4..];

    public static OwnerRevenueTransactionDto ToDto(OwnerRevenueTransactionRow r)
        => new(r.Id, r.Code, r.PaidAtUtc, r.ParkingLotId, r.BookingCode, r.Method.ToString(), r.Purpose.ToString(), r.Status.ToString(),
            OwnerRevenueRules.Vnd(r.Amount), OwnerRevenueRules.Vnd(r.RefundedAmount), OwnerRevenueRules.Vnd(r.Amount - r.RefundedAmount),
            MaskProviderTransactionId(r.ProviderTransactionId));

    /// <summary>Chỉ nhận đúng tên trong <paramref name="allowed"/> (không phân biệt hoa thường; không nhận số / chuỗi kiểu cờ).</summary>
    private static T? Parse<T>(string name, string? value, IReadOnlyList<T> allowed) where T : struct, Enum
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        foreach (var candidate in allowed)
            if (string.Equals(candidate.ToString(), value.Trim(), StringComparison.OrdinalIgnoreCase))
                return candidate;
        throw new ValidationException($"{name} phải là {string.Join(", ", allowed.Take(allowed.Count - 1))} hoặc {allowed[^1]}.");
    }
}

/// <summary>
/// US-078 AC3: ghi CSV (RFC 4180, UTF-8 có BOM để Excel đọc đúng tiếng Việt, xuống dòng CRLF) – cùng quy ước với US-100.
/// Chống CSV injection: ô chữ bắt đầu bằng = + - @ tab hoặc CR được thêm dấu ' phía trước. Hàm thuần – không đọc database.
/// </summary>
public static class OwnerRevenueCsvWriter
{
    private static readonly string[] Header =
    [
        "Code", "PaidAtUtc", "PaidAtIct", "ParkingLotId", "BookingCode", "Method", "Purpose", "Status",
        "Amount", "RefundedAmount", "NetAmount", "ProviderTransactionRef",
    ];

    public static byte[] Write(IEnumerable<OwnerRevenueTransactionDto> rows)
    {
        var csv = new StringBuilder();
        AppendLine(csv, Header);
        foreach (var r in rows)
        {
            AppendLine(csv,
            [
                Text(r.Code),
                r.PaidAtUtc.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture),
                (r.PaidAtUtc + OwnerRevenueRules.IctOffset).ToString("dd/MM/yyyy HH:mm:ss", CultureInfo.InvariantCulture),
                r.ParkingLotId.ToString(CultureInfo.InvariantCulture),
                Text(r.BookingCode),
                r.Method,
                r.Purpose,
                r.Status,
                Money(r.Amount),
                Money(r.RefundedAmount),
                Money(r.NetAmount),
                Text(r.ProviderTransactionRef),
            ]);
        }
        return [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes(csv.ToString())];
    }

    private static string Money(decimal amount) => amount.ToString("0", CultureInfo.InvariantCulture);

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

// ===== USE CASE (đọc) =====
public interface IListOwnerRevenueTransactionsUseCase
{
    Task<PagedResult<OwnerRevenueTransactionDto>> ExecuteAsync(int? ownerProfileId, OwnerRevenueTransactionQuery query, int page,
        int pageSize, CancellationToken cancellationToken = default);
}

/// <summary>
/// US-078 (UC-35) AC2–AC3: danh sách giao dịch được tính doanh thu của chủ bãi đang đăng nhập, có lọc và phân trang. Chỉ đọc.
/// Cùng bộ lọc với summary/series nên tổng Amount của mọi trang = GrossRevenue của summary.
/// </summary>
public sealed class ListOwnerRevenueTransactionsUseCase(IOwnerRevenueQueries queries, TimeProvider timeProvider)
    : IListOwnerRevenueTransactionsUseCase
{
    public async Task<PagedResult<OwnerRevenueTransactionDto>> ExecuteAsync(int? ownerProfileId, OwnerRevenueTransactionQuery query, int page,
        int pageSize, CancellationToken cancellationToken = default)
    {
        var filter = OwnerRevenueTransactionRules.Normalize(ownerProfileId, query, timeProvider.GetUtcNow().UtcDateTime);
        OwnerRevenueTransactionRules.ValidatePaging(page, pageSize);

        var result = await queries.ListTransactionsAsync(filter, page, pageSize, cancellationToken);
        return new PagedResult<OwnerRevenueTransactionDto>(result.Items.Select(OwnerRevenueTransactionRules.ToDto).ToList(),
            result.Page, result.PageSize, result.TotalCount);
    }
}

public interface IExportOwnerRevenueTransactionsUseCase
{
    Task<OwnerRevenueCsvFile> ExecuteAsync(int? ownerProfileId, OwnerRevenueTransactionQuery query, CancellationToken cancellationToken = default);
}

/// <summary>
/// US-078 AC3 (UC-35 bước 4): xuất giao dịch theo đúng bộ lọc của danh sách, mới nhất trước, tối đa <see cref="MaxRows"/> dòng.
/// Vượt giới hạn → 400 yêu cầu thu hẹp bộ lọc (không cắt ngầm để file không thiếu dữ liệu mà người xem không biết).
/// </summary>
public sealed class ExportOwnerRevenueTransactionsUseCase(IOwnerRevenueQueries queries, TimeProvider timeProvider)
    : IExportOwnerRevenueTransactionsUseCase
{
    public const int MaxRows = 10_000;

    public async Task<OwnerRevenueCsvFile> ExecuteAsync(int? ownerProfileId, OwnerRevenueTransactionQuery query,
        CancellationToken cancellationToken = default)
    {
        var filter = OwnerRevenueTransactionRules.Normalize(ownerProfileId, query, timeProvider.GetUtcNow().UtcDateTime);
        var rows = await queries.ListTransactionsForExportAsync(filter, MaxRows + 1, cancellationToken);   // +1 để biết có vượt không
        if (rows.Count > MaxRows)
            throw new ValidationException(
                $"Kết quả vượt quá {MaxRows.ToString("N0", CultureInfo.GetCultureInfo("vi-VN"))} dòng. Vui lòng thu hẹp bộ lọc (thời gian, bãi, phương thức…) rồi xuất lại.");

        var range = filter.Range;
        return new OwnerRevenueCsvFile($"owner-revenue-{range.From:yyyyMMdd}-{range.To:yyyyMMdd}.csv",
            OwnerRevenueCsvWriter.Write(rows.Select(OwnerRevenueTransactionRules.ToDto)));
    }
}
