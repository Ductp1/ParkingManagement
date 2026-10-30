using System.Globalization;
using System.Text;
using ParkingManagement.SharedKernel.Exceptions;

namespace AdminService.Application.Features.AuditLogs;

// ===== DTO =====
/// <summary>Một dòng xuất CSV: các cột của danh sách + IpAddress (không xuất OldValuesJson / NewValuesJson).</summary>
public sealed record AuditLogExportRow(int Id, DateTime CreatedAtUtc, string SourceService, int? UserId, int? OwnerProfileId,
    string Action, string EntityName, string? EntityId, string? Reason, string? IpAddress);

/// <summary>File CSV trả về cho trình duyệt tải xuống.</summary>
public sealed record AuditLogCsvFile(string FileName, byte[] Content)
{
    public const string ContentType = "text/csv; charset=utf-8";
}

/// <summary>
/// US-100: ghi CSV (RFC 4180, UTF-8 có BOM để Excel đọc đúng tiếng Việt, xuống dòng CRLF).
/// Chống CSV injection: ô chữ bắt đầu bằng = + - @ tab hoặc CR được thêm dấu ' phía trước để Excel không chạy như công thức.
/// Hàm thuần – không đọc database.
/// </summary>
public static class AuditLogCsvWriter
{
    private static readonly TimeSpan IctOffset = TimeSpan.FromHours(7);   // Việt Nam không có giờ mùa hè

    private static readonly string[] Header =
        ["Id", "CreatedAtUtc", "CreatedAtIct", "SourceService", "UserId", "OwnerProfileId", "Action", "EntityName", "EntityId", "Reason", "IpAddress"];

    public static byte[] Write(IEnumerable<AuditLogExportRow> rows)
    {
        var csv = new StringBuilder();
        AppendLine(csv, Header);
        foreach (var r in rows)
        {
            AppendLine(csv,
            [
                r.Id.ToString(CultureInfo.InvariantCulture),
                r.CreatedAtUtc.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture),
                (r.CreatedAtUtc + IctOffset).ToString("dd/MM/yyyy HH:mm:ss", CultureInfo.InvariantCulture),
                Text(r.SourceService),
                r.UserId?.ToString(CultureInfo.InvariantCulture),
                r.OwnerProfileId?.ToString(CultureInfo.InvariantCulture),
                Text(r.Action),
                Text(r.EntityName),
                Text(r.EntityId),
                Text(r.Reason),
                Text(r.IpAddress),
            ]);
        }
        return [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes(csv.ToString())];
    }

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
public interface IExportAuditLogsUseCase
{
    Task<AuditLogCsvFile> ExecuteAsync(AuditLogListQuery query, CancellationToken cancellationToken = default);
}

/// <summary>
/// US-100 (UC-45 bước 3): xuất audit log theo đúng bộ lọc của danh sách, mới nhất trước, tối đa <see cref="MaxRows"/> dòng.
/// Vượt giới hạn → 400 yêu cầu thu hẹp bộ lọc (không cắt ngầm để file không thiếu dữ liệu mà người xem không biết).
/// </summary>
public sealed class ExportAuditLogsUseCase(IAuditLogQueries queries, TimeProvider timeProvider) : IExportAuditLogsUseCase
{
    public const int MaxRows = 10_000;

    public async Task<AuditLogCsvFile> ExecuteAsync(AuditLogListQuery query, CancellationToken cancellationToken = default)
    {
        var filter = AuditLogFilterRules.Normalize(query);
        var rows = await queries.ListForExportAsync(filter, MaxRows + 1, cancellationToken);   // +1 để biết có vượt không
        if (rows.Count > MaxRows)
            throw new ValidationException($"Kết quả vượt quá {MaxRows:N0} dòng. Vui lòng thu hẹp bộ lọc (thời gian, service, thao tác…) rồi xuất lại.");

        var nowUtc = timeProvider.GetUtcNow().UtcDateTime;
        return new AuditLogCsvFile($"audit-logs-{nowUtc:yyyyMMddHHmm}.csv", AuditLogCsvWriter.Write(rows));
    }
}
