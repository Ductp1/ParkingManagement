using System.Globalization;
using System.Text.Json;
using AdminService.Domain.Entities;
using ParkingManagement.SharedKernel.Exceptions;

namespace AdminService.Application.Features.Settings;

// ===== DTO =====
/// <summary>
/// Một thay đổi tham số (US-098). PreviousValue = giá trị đang hiệu lực ngay trước EffectiveFromUtc.
/// Status: Scheduled (chưa tới ngày hiệu lực) | Effective (đang áp dụng).
/// </summary>
public sealed record SystemConfigChangeDto(int Id, string Key, string Value, string PreviousValue, DateTime EffectiveFromUtc,
    string Status, string Reason, int CreatedByUserId, DateTime CreatedAtUtc, DateTime? CancelledAtUtc, int? CancelledByUserId,
    string? CancelReason);

// ===== COMMAND =====
/// <summary>
/// Đổi giá trị một tham số (US-098). Value luôn là chuỗi, kể cả tham số kiểu số. EffectiveFromUtc bỏ trống = hiệu lực ngay.
/// Admin thao tác không nằm trong body: controller lấy từ JWT.
/// </summary>
public sealed record UpdateSystemConfigRequest(string? Value, string? Reason, DateTime? EffectiveFromUtc = null);

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
            throw new ConflictException($"Tham số {config.Key} đã có một thay đổi hiệu lực từ {Format(effectiveFromUtc)}.");

        var previousValue = SystemConfigValueResolver.Resolve(config.DefaultValue, config.Changes, effectiveFromUtc).Value;
        if (SystemConfigRules.AreSameValue(config.DataType, previousValue, value))
            throw new ConflictException($"Giá trị mới trùng với giá trị của {config.Key} đang hiệu lực tại {Format(effectiveFromUtc)}.");

        await EnsureOrderRulesAsync(config, value, effectiveFromUtc, cancellationToken);

        var change = SystemConfigChange.Schedule(config.Id, value, effectiveFromUtc, reason, performedByUserId);
        await repository.AddChangeAsync(change, BuildAuditLog(config.Key, change, previousValue), cancellationToken);

        return new SystemConfigChangeDto(change.Id, config.Key, change.Value, previousValue, change.EffectiveFromUtc,
            change.EffectiveFromUtc > now ? "Scheduled" : "Effective", change.Reason, change.CreatedByUserId, change.CreatedAtUtc,
            change.CancelledAtUtc, change.CancelledByUserId, change.CancelReason);
    }

    /// <summary>
    /// Quy tắc liên khóa (sàn giá ≤ trần giá, ân hạn check-in ≤ mốc hủy no-show) phải đúng trong suốt khoảng giá trị mới được áp dụng:
    /// từ effectiveFromUtc tới thay đổi kế tiếp đã lên lịch của chính tham số này. Khóa còn lại được xét bằng giá trị hiệu lực
    /// tại effectiveFromUtc và mọi thay đổi đã lên lịch của nó trong khoảng đó – không chỉ giá trị hiện tại.
    /// </summary>
    private async Task EnsureOrderRulesAsync(SystemConfigRecord config, string value, DateTime effectiveFromUtc, CancellationToken cancellationToken)
    {
        if (!SystemConfigRules.TryParseDecimal(value, out var newValue)) return;

        var nextOwnChangeUtc = config.Changes
            .Where(c => c.CancelledAtUtc is null && c.EffectiveFromUtc > effectiveFromUtc)
            .Select(c => (DateTime?)c.EffectiveFromUtc)
            .Min();

        foreach (var rule in SystemConfigRules.OrderRules.Where(r => r.LowerKey == config.Key || r.UpperKey == config.Key))
        {
            var isLower = rule.LowerKey == config.Key;
            var other = await repository.FindByKeyWithChangesAsync(isLower ? rule.UpperKey : rule.LowerKey, cancellationToken);
            if (other is null) continue;

            var atEffectiveDate = SystemConfigValueResolver.Resolve(other.DefaultValue, other.Changes, effectiveFromUtc).Value;
            var otherValues = other.Changes
                .Where(c => c.CancelledAtUtc is null && c.EffectiveFromUtc > effectiveFromUtc
                    && (nextOwnChangeUtc is null || c.EffectiveFromUtc < nextOwnChangeUtc))
                .OrderBy(c => c.EffectiveFromUtc)
                .Select(c => (c.Value, SinceUtc: c.EffectiveFromUtc))
                .Prepend((Value: atEffectiveDate, SinceUtc: effectiveFromUtc));

            foreach (var (otherRaw, sinceUtc) in otherValues)
            {
                if (!SystemConfigRules.TryParseDecimal(otherRaw, out var otherValue)) continue;
                var (lower, upper) = isLower ? (newValue, otherValue) : (otherValue, newValue);
                if (lower > upper)
                    throw new ValidationException(
                        $"{rule.LowerLabel} ({rule.LowerKey} = {lower.ToString(CultureInfo.InvariantCulture)}) không được lớn hơn " +
                        $"{rule.UpperLabel} ({rule.UpperKey} = {upper.ToString(CultureInfo.InvariantCulture)}) kể từ {Format(sinceUtc)}.");
            }
        }
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

    private static string Format(DateTime utc) => utc.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
}
