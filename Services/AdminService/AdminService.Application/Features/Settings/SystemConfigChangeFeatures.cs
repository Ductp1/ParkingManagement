using System.Globalization;
using System.Text.Json;
using AdminService.Domain.Entities;
using ParkingManagement.SharedKernel.Exceptions;

namespace AdminService.Application.Features.Settings;

// ===== DTO =====
/// <summary>Scheduled = chưa tới ngày hiệu lực · Effective = đang áp dụng · Superseded = đã bị thay đổi sau thay thế · Cancelled = đã hủy trước khi hiệu lực.</summary>
public enum SystemConfigChangeStatus
{
    Scheduled,
    Effective,
    Superseded,
    Cancelled,
}

/// <summary>Một thay đổi tham số (US-098). PreviousValue = giá trị đang hiệu lực ngay trước EffectiveFromUtc.</summary>
public sealed record SystemConfigChangeDto(int Id, string Key, string Value, string PreviousValue, DateTime EffectiveFromUtc,
    string Status, string Reason, int CreatedByUserId, DateTime CreatedAtUtc, DateTime? CancelledAtUtc, int? CancelledByUserId,
    string? CancelReason);

// ===== COMMAND =====
/// <summary>
/// Đổi giá trị một tham số (US-098). Value luôn là chuỗi, kể cả tham số kiểu số. EffectiveFromUtc bỏ trống = hiệu lực ngay.
/// Admin thao tác không nằm trong body: controller lấy từ JWT.
/// </summary>
public sealed record UpdateSystemConfigRequest(string? Value, string? Reason, DateTime? EffectiveFromUtc = null);

/// <summary>Hủy một thay đổi chưa tới ngày hiệu lực (US-098). Reason = lý do hủy.</summary>
public sealed record CancelSystemConfigChangeRequest(string? Reason);

// ===== PORT (ghi) =====
public interface ISystemConfigRepository
{
    /// <summary>Tham số theo khóa (khớp chính xác) kèm TOÀN BỘ lịch sử thay đổi – cả thay đổi đã lên lịch và đã hủy.</summary>
    Task<SystemConfigRecord?> FindByKeyWithChangesAsync(string key, CancellationToken cancellationToken);
    /// <summary>
    /// Ghi thay đổi + audit log trong CÙNG 1 lần SaveChanges. Hai request đặt cùng mốc hiệu lực cho một tham số:
    /// request đến sau bị unique index chặn → ConflictException.
    /// </summary>
    Task AddChangeAsync(SystemConfigChange change, AuditLog auditLog, CancellationToken cancellationToken);
    /// <summary>Một dòng thay đổi theo Id, entity có tracking để đóng dấu hủy.</summary>
    Task<SystemConfigChange?> FindChangeTrackedAsync(int changeId, CancellationToken cancellationToken);
    /// <summary>Ghi thay đổi của dòng đang tracking + audit log trong CÙNG 1 lần SaveChanges.</summary>
    Task SaveWithAuditAsync(AuditLog auditLog, CancellationToken cancellationToken);
}

// ===== USE CASE (ghi) =====
public interface IUpdateSystemConfigUseCase
{
    Task<SystemConfigChangeDto> ExecuteAsync(string key, UpdateSystemConfigRequest request, int performedByUserId, CancellationToken cancellationToken = default);
}

/// <summary>
/// US-098 (UC-44): Admin đổi giá trị một tham số hệ thống, có Effective Date và bắt buộc có lý do.
/// Không ghi đè giá trị cũ: thêm 1 dòng SystemConfigChange + AuditLog (1 lần SaveChanges). Giá trị hiệu lực được tính khi đọc,
/// nên booking đã tạo (chốt tham số theo lúc tạo) không bị ảnh hưởng.
/// </summary>
public sealed class UpdateSystemConfigUseCase(ISystemConfigRepository repository, TimeProvider timeProvider) : IUpdateSystemConfigUseCase
{
    public const int MaxScheduleDays = 365;

    private static readonly JsonSerializerOptions AuditJson = new(JsonSerializerDefaults.Web);

    public async Task<SystemConfigChangeDto> ExecuteAsync(string key, UpdateSystemConfigRequest request, int performedByUserId, CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;

        if (performedByUserId <= 0) throw new ValidationException("performedByUserId phải là số nguyên dương.");
        if (string.IsNullOrWhiteSpace(request.Reason)) throw new ValidationException("Lý do thay đổi không được để trống.");
        var reason = request.Reason.Trim();
        if (reason.Length > 1000) throw new ValidationException("Lý do thay đổi tối đa 1000 ký tự.");

        // Mốc hiệu lực tính tới giây; bỏ trống = hiệu lực ngay (đầu giây hiện tại).
        var currentSecond = TruncateToSecond(now);
        var effectiveFromUtc = TruncateToSecond(UtcDateTime.ToUtc(request.EffectiveFromUtc) ?? now);
        if (effectiveFromUtc < currentSecond) throw new ValidationException("effectiveFromUtc không được ở quá khứ.");
        if (effectiveFromUtc > now.AddDays(MaxScheduleDays))
            throw new ValidationException($"effectiveFromUtc không được xa hơn {MaxScheduleDays} ngày kể từ hiện tại.");

        var normalizedKey = SystemConfigMapper.NormalizeKey(key);
        var config = await repository.FindByKeyWithChangesAsync(normalizedKey, cancellationToken)
            ?? throw new NotFoundException("Tham số hệ thống", normalizedKey);

        var value = SystemConfigRules.Normalize(config.Key, config.DataType, request.Value);

        if (config.Changes.Any(c => c.CancelledAtUtc is null && c.EffectiveFromUtc == effectiveFromUtc))
            throw new ConflictException($"Tham số {config.Key} đã có một thay đổi hiệu lực từ {SystemConfigChangeMapper.Format(effectiveFromUtc)}.");

        var previousValue = SystemConfigValueResolver.Resolve(config.DefaultValue, config.Changes, effectiveFromUtc).Value;
        if (SystemConfigRules.AreSameValue(config.DataType, previousValue, value))
            throw new ConflictException(
                $"Giá trị mới trùng với giá trị của {config.Key} đang hiệu lực tại {SystemConfigChangeMapper.Format(effectiveFromUtc)}.");

        var violation = await SystemConfigOrderRuleCheck.FindViolationAsync(repository, config.Key, config.Changes, value, effectiveFromUtc, cancellationToken);
        if (violation is not null) throw new ValidationException(violation);

        var change = SystemConfigChange.Schedule(config.Id, value, effectiveFromUtc, reason, performedByUserId);
        await repository.AddChangeAsync(change, BuildAuditLog(config.Key, change, previousValue), cancellationToken);

        return SystemConfigChangeMapper.ToDto(config.Key, change, previousValue,
            change.EffectiveFromUtc > now ? SystemConfigChangeStatus.Scheduled : SystemConfigChangeStatus.Effective);
    }

    private static AuditLog BuildAuditLog(string key, SystemConfigChange change, string previousValue) => new()
    {
        SourceService = "AdminService",
        UserId = change.CreatedByUserId,
        Action = "SystemConfig.ChangeScheduled",
        EntityName = "SystemConfig",
        EntityId = key,
        OldValuesJson = JsonSerializer.Serialize(new { value = previousValue }, AuditJson),
        NewValuesJson = JsonSerializer.Serialize(new { value = change.Value, effectiveFromUtc = change.EffectiveFromUtc }, AuditJson),
        Reason = change.Reason,
    };

    private static DateTime TruncateToSecond(DateTime value) => new(value.Ticks - value.Ticks % TimeSpan.TicksPerSecond, DateTimeKind.Utc);
}

public interface ICancelSystemConfigChangeUseCase
{
    Task<SystemConfigChangeDto> ExecuteAsync(string key, int changeId, CancelSystemConfigChangeRequest request, int performedByUserId, CancellationToken cancellationToken = default);
}

/// <summary>
/// US-098 (UC-44): Admin hủy một thay đổi tham số CHƯA tới ngày hiệu lực, bắt buộc có lý do.
/// Dòng thay đổi được giữ lại và đóng dấu hủy + AuditLog (1 lần SaveChanges). Thay đổi đã hiệu lực không hủy được –
/// giá trị của một thời điểm đã qua không bao giờ đổi; muốn đổi lại thì đặt một thay đổi mới.
/// </summary>
public sealed class CancelSystemConfigChangeUseCase(ISystemConfigRepository repository, TimeProvider timeProvider) : ICancelSystemConfigChangeUseCase
{
    private static readonly JsonSerializerOptions AuditJson = new(JsonSerializerDefaults.Web);

    public async Task<SystemConfigChangeDto> ExecuteAsync(string key, int changeId, CancelSystemConfigChangeRequest request, int performedByUserId, CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;

        if (performedByUserId <= 0) throw new ValidationException("performedByUserId phải là số nguyên dương.");
        if (changeId <= 0) throw new ValidationException("changeId phải là số nguyên dương.");
        if (string.IsNullOrWhiteSpace(request.Reason)) throw new ValidationException("Lý do hủy không được để trống.");
        var reason = request.Reason.Trim();
        if (reason.Length > 1000) throw new ValidationException("Lý do hủy tối đa 1000 ký tự.");

        var normalizedKey = SystemConfigMapper.NormalizeKey(key);
        var config = await repository.FindByKeyWithChangesAsync(normalizedKey, cancellationToken)
            ?? throw new NotFoundException("Tham số hệ thống", normalizedKey);

        var change = await repository.FindChangeTrackedAsync(changeId, cancellationToken);
        if (change is null || change.SystemConfigId != config.Id)       // thay đổi của tham số khác cũng coi như không có
            throw new NotFoundException($"Thay đổi của tham số {config.Key}", changeId);

        if (change.CancelledAtUtc is not null) throw new ConflictException($"Thay đổi #{changeId} đã bị hủy trước đó.");
        if (change.EffectiveFromUtc <= now)
            throw new ConflictException(
                $"Thay đổi #{changeId} đã có hiệu lực từ {SystemConfigChangeMapper.Format(change.EffectiveFromUtc)} nên không hủy được.");

        // Hủy xong, từ mốc hiệu lực của thay đổi này tham số quay về giá trị liền trước – giá trị đó vẫn phải thỏa quy tắc liên khóa.
        var remaining = config.Changes.Where(c => c.Id != changeId).ToList();
        var restoredValue = SystemConfigValueResolver.Resolve(config.DefaultValue, remaining, change.EffectiveFromUtc).Value;
        var violation = await SystemConfigOrderRuleCheck.FindViolationAsync(repository, config.Key, remaining, restoredValue,
            change.EffectiveFromUtc, cancellationToken);
        if (violation is not null) throw new ConflictException($"Không thể hủy thay đổi #{changeId}: {violation}");

        change.Cancel(performedByUserId, reason, now);
        await repository.SaveWithAuditAsync(BuildAuditLog(config.Key, change, restoredValue), cancellationToken);

        return SystemConfigChangeMapper.ToDto(config.Key, change, restoredValue, SystemConfigChangeStatus.Cancelled);
    }

    private static AuditLog BuildAuditLog(string key, SystemConfigChange change, string restoredValue) => new()
    {
        SourceService = "AdminService",
        UserId = change.CancelledByUserId,
        Action = "SystemConfig.ChangeCancelled",
        EntityName = "SystemConfig",
        EntityId = key,
        OldValuesJson = JsonSerializer.Serialize(
            new { changeId = change.Id, value = change.Value, effectiveFromUtc = change.EffectiveFromUtc }, AuditJson),
        NewValuesJson = JsonSerializer.Serialize(new { changeId = change.Id, cancelled = true, value = restoredValue }, AuditJson),
        Reason = change.CancelReason,
    };
}

/// <summary>
/// Quy tắc liên khóa (sàn giá ≤ trần giá, ân hạn check-in ≤ mốc hủy no-show) phải đúng trong suốt khoảng một giá trị được áp dụng:
/// từ fromUtc tới thay đổi kế tiếp đã lên lịch của chính tham số đó. Khóa còn lại được xét bằng giá trị hiệu lực tại fromUtc
/// và mọi thay đổi đã lên lịch của nó trong khoảng đó – không chỉ giá trị hiện tại.
/// </summary>
internal static class SystemConfigOrderRuleCheck
{
    /// <summary>
    /// Trả về câu mô tả vi phạm đầu tiên, null nếu hợp lệ. ownChanges = lịch sử của chính tham số, KHÔNG gồm thay đổi đang được
    /// thêm hoặc hủy; value = giá trị tham số sẽ mang kể từ fromUtc.
    /// </summary>
    public static async Task<string?> FindViolationAsync(ISystemConfigRepository repository, string key,
        IReadOnlyList<SystemConfigChangePoint> ownChanges, string value, DateTime fromUtc, CancellationToken cancellationToken)
    {
        if (!SystemConfigRules.TryParseDecimal(value, out var ownValue)) return null;

        var nextOwnChangeUtc = ownChanges
            .Where(c => c.CancelledAtUtc is null && c.EffectiveFromUtc > fromUtc)
            .Select(c => (DateTime?)c.EffectiveFromUtc)
            .Min();

        foreach (var rule in SystemConfigRules.OrderRules.Where(r => r.LowerKey == key || r.UpperKey == key))
        {
            var isLower = rule.LowerKey == key;
            var other = await repository.FindByKeyWithChangesAsync(isLower ? rule.UpperKey : rule.LowerKey, cancellationToken);
            if (other is null) continue;

            var atStart = SystemConfigValueResolver.Resolve(other.DefaultValue, other.Changes, fromUtc).Value;
            var otherValues = other.Changes
                .Where(c => c.CancelledAtUtc is null && c.EffectiveFromUtc > fromUtc
                    && (nextOwnChangeUtc is null || c.EffectiveFromUtc < nextOwnChangeUtc))
                .OrderBy(c => c.EffectiveFromUtc)
                .Select(c => (c.Value, SinceUtc: c.EffectiveFromUtc))
                .Prepend((Value: atStart, SinceUtc: fromUtc));

            foreach (var (otherRaw, sinceUtc) in otherValues)
            {
                if (!SystemConfigRules.TryParseDecimal(otherRaw, out var otherValue)) continue;
                var (lower, upper) = isLower ? (ownValue, otherValue) : (otherValue, ownValue);
                if (lower > upper)
                    return $"{rule.LowerLabel} ({rule.LowerKey} = {lower.ToString(CultureInfo.InvariantCulture)}) không được lớn hơn " +
                        $"{rule.UpperLabel} ({rule.UpperKey} = {upper.ToString(CultureInfo.InvariantCulture)}) " +
                        $"kể từ {SystemConfigChangeMapper.Format(sinceUtc)}.";
            }
        }

        return null;
    }
}

/// <summary>Map entity → DTO dùng chung cho các use case ghi thay đổi tham số.</summary>
public static class SystemConfigChangeMapper
{
    public static SystemConfigChangeDto ToDto(string key, SystemConfigChange change, string previousValue, SystemConfigChangeStatus status)
        => new(change.Id, key, change.Value, previousValue, change.EffectiveFromUtc, status.ToString(), change.Reason,
            change.CreatedByUserId, change.CreatedAtUtc, change.CancelledAtUtc, change.CancelledByUserId, change.CancelReason);

    /// <summary>Mốc thời gian UTC trong thông báo lỗi, VD 2026-10-09T08:00:00Z.</summary>
    public static string Format(DateTime utc) => utc.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
}
