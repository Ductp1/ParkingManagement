using System.Text.Json;
using AdminService.Domain.Entities;
using ParkingManagement.SharedKernel.Enums;
using ParkingManagement.SharedKernel.Exceptions;

namespace AdminService.Application.Features.RiskFlags;

// ===== US-077: CẢNH BÁO STAFF HỦY NO-SHOW BẤT THƯỜNG (GateService đẩy sang qua endpoint nội bộ) =====

/// <summary>
/// Dữ liệu hủy No-show của 1 Staff trong cửa sổ thống kê do GateService gửi kèm tiêu chí đang áp dụng
/// (ngưỡng thuộc GateService – "GateConsole:AbnormalNoShowCancelThreshold" / "CriticalNoShowCancelThreshold").
/// </summary>
public sealed record StaffCancellationAlertRequest(
    int StaffUserId,
    int ParkingLotId,
    int CancelCount,
    DateTime WindowFromUtc,
    DateTime WindowToUtc,
    int WarningThreshold,
    int CriticalThreshold,
    DateTime? LastCancelAtUtc = null,
    string? LastBookingCode = null);

public sealed record RiskFlagAlertResultDto(int RiskFlagId, string RuleCode, string Severity, string Status,
    bool Created, bool Escalated, DateTime DueAtUtc);

/// <summary>PORT ghi RiskFlags (cảnh báo cho Admin) – tra cờ đang chờ xử lý để không tạo trùng.</summary>
public interface IRiskFlagStore
{
    Task<RiskFlag?> FindPendingAsync(RiskSubjectType subjectType, int subjectId, string ruleCode, CancellationToken cancellationToken = default);
    Task AddAsync(RiskFlag flag, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

public interface IRaiseStaffCancellationAlertUseCase
{
    Task<RiskFlagAlertResultDto> ExecuteAsync(string callerService, StaffCancellationAlertRequest? request,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// US-077: nhận cảnh báo hủy No-show bất thường từ GateService → RiskFlag STAFF_ABNORMAL_CANCEL (SubjectType Staff).
///  - Chỉ GateService được gọi (khóa nội bộ "InternalEvents:ApiKeys:GateService").
///  - Mức độ: ≥ CriticalThreshold → Critical (xử lý trong 15 phút), ≥ WarningThreshold → Warning (4 giờ).
///  - Chống trùng: Staff đã có cờ Open/Investigating cùng RuleCode → cập nhật bằng chứng, nâng mức nếu nặng hơn
///    (không hạ mức, không tạo dòng mới) – GateService gọi lại nhiều lần vẫn an toàn.
/// </summary>
public sealed class RaiseStaffCancellationAlertUseCase(IRiskFlagStore store, TimeProvider timeProvider) : IRaiseStaffCancellationAlertUseCase
{
    public const string AllowedCaller = "GateService";
    public const string RuleCode = "STAFF_ABNORMAL_CANCEL";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<RiskFlagAlertResultDto> ExecuteAsync(string callerService, StaffCancellationAlertRequest? request,
        CancellationToken cancellationToken = default)
    {
        if (!string.Equals(callerService, AllowedCaller, StringComparison.Ordinal))
            throw new ForbiddenException($"Service {callerService} không được gửi cảnh báo hủy No-show của Staff.");
        var alert = Validate(request);

        var severity = alert.CancelCount >= alert.CriticalThreshold ? RiskSeverity.Critical : RiskSeverity.Warning;
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var description = $"Staff #{alert.StaffUserId} hủy No-show {alert.CancelCount} lượt tại bãi #{alert.ParkingLotId} " +
                          $"từ {alert.WindowFromUtc:yyyy-MM-dd HH:mm} đến {alert.WindowToUtc:yyyy-MM-dd HH:mm} UTC " +
                          $"(ngưỡng Warning {alert.WarningThreshold}, Critical {alert.CriticalThreshold}).";
        var evidence = JsonSerializer.Serialize(alert, Json);

        var existing = await store.FindPendingAsync(RiskSubjectType.Staff, alert.StaffUserId, RuleCode, cancellationToken);
        if (existing is not null)
        {
            var escalated = severity > existing.Severity;
            if (escalated)
            {
                existing.Severity = severity;
                existing.DueAtUtc = Earlier(existing.DueAtUtc, now + ResponseTime(severity));
            }
            existing.Description = description;
            existing.EvidenceJson = evidence;
            await store.SaveChangesAsync(cancellationToken);
            return ToDto(existing, created: false, escalated);
        }

        var flag = new RiskFlag
        {
            SubjectType = RiskSubjectType.Staff,
            SubjectId = alert.StaffUserId,
            RuleCode = RuleCode,
            Severity = severity,
            Status = RiskFlagStatus.Open,
            Description = description,
            EvidenceJson = evidence,
            DueAtUtc = now + ResponseTime(severity),
        };
        await store.AddAsync(flag, cancellationToken);
        await store.SaveChangesAsync(cancellationToken);
        return ToDto(flag, created: true, escalated: false);
    }

    /// <summary>Alert tiers (RiskFlag): Info 24 giờ, Warning 4 giờ, Critical 15 phút.</summary>
    public static TimeSpan ResponseTime(RiskSeverity severity) => severity switch
    {
        RiskSeverity.Critical => TimeSpan.FromMinutes(15),
        RiskSeverity.Warning => TimeSpan.FromHours(4),
        _ => TimeSpan.FromHours(24),
    };

    private static StaffCancellationAlertRequest Validate(StaffCancellationAlertRequest? request)
    {
        if (request is null) throw new ValidationException("Body cảnh báo không được rỗng.");
        if (request.StaffUserId <= 0) throw new ValidationException("staffUserId phải là số nguyên dương.");
        if (request.ParkingLotId <= 0) throw new ValidationException("parkingLotId phải là số nguyên dương.");
        if (request.WarningThreshold <= 0 || request.CriticalThreshold < request.WarningThreshold)
            throw new ValidationException("Ngưỡng cảnh báo không hợp lệ (0 < warningThreshold ≤ criticalThreshold).");
        if (request.WindowToUtc <= request.WindowFromUtc)
            throw new ValidationException("Cửa sổ thống kê không hợp lệ (windowToUtc phải sau windowFromUtc).");
        if (request.CancelCount < request.WarningThreshold)
            throw new ValidationException($"Số lượt hủy {request.CancelCount} chưa đạt ngưỡng cảnh báo {request.WarningThreshold}.");
        return request with { LastBookingCode = request.LastBookingCode?.Trim() is { Length: > 0 } code ? code[..Math.Min(code.Length, 30)] : null };
    }

    private static DateTime Earlier(DateTime a, DateTime b) => a <= b ? a : b;

    private static RiskFlagAlertResultDto ToDto(RiskFlag flag, bool created, bool escalated)
        => new(flag.Id, flag.RuleCode, flag.Severity.ToString(), flag.Status.ToString(), created, escalated, flag.DueAtUtc);
}
