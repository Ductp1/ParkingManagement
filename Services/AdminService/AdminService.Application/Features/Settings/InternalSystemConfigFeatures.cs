using ParkingManagement.SharedKernel.Exceptions;

namespace AdminService.Application.Features.Settings;

/// <summary>
/// Giá trị hiệu lực của một tham số tại <see cref="AtUtc"/> cho service nội bộ.
/// EffectiveFromUtc = mốc thay đổi đang áp dụng; null (IsDefault = true) khi vẫn là giá trị gốc.
/// </summary>
public sealed record InternalSystemConfigValueDto(string Key, string Value, DateTime AtUtc, DateTime? EffectiveFromUtc, bool IsDefault);

public interface IResolveInternalSystemConfigUseCase
{
    Task<InternalSystemConfigValueDto> ExecuteAsync(string callerService, string? key, DateTime? atUtc,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// US-080: service nội bộ (đã xác thực bằng khóa InternalEvents:ApiKeys:{Service}) đọc giá trị hiệu lực của tham số tại một thời điểm.
/// Chỉ những tham số trong <see cref="AllowedKeys"/> của từng service được đọc – PaymentService chỉ cần SETTLEMENT_CYCLE để chia kỳ quyết toán.
/// Thời điểm ở tương lai bị từ chối: thay đổi chưa hiệu lực còn hủy được nên kết quả chưa cố định; thời điểm đã qua thì không bao giờ đổi.
/// </summary>
public sealed class ResolveInternalSystemConfigUseCase(ISettingsQueries queries, TimeProvider timeProvider) : IResolveInternalSystemConfigUseCase
{
    public const string SettlementCycleKey = "SETTLEMENT_CYCLE";

    /// <summary>Service → các khóa được đọc. Thêm khóa mới phải sửa ở đây (fail closed).</summary>
    public static readonly IReadOnlyDictionary<string, IReadOnlySet<string>> AllowedKeys = new Dictionary<string, IReadOnlySet<string>>
    {
        ["PaymentService"] = new HashSet<string>(StringComparer.Ordinal) { SettlementCycleKey },
    };

    /// <summary>Cho phép lệch đồng hồ giữa hai service (như US-079).</summary>
    public static readonly TimeSpan ClockSkew = TimeSpan.FromMinutes(5);

    public async Task<InternalSystemConfigValueDto> ExecuteAsync(string callerService, string? key, DateTime? atUtc,
        CancellationToken cancellationToken = default)
    {
        var normalizedKey = SystemConfigMapper.NormalizeKey(key);
        if (normalizedKey.Length == 0) throw new ValidationException("key không được để trống.");
        if (!AllowedKeys.TryGetValue(callerService, out var keys) || !keys.Contains(normalizedKey))
            throw new ForbiddenException($"Service {callerService} không được đọc tham số {normalizedKey}.");

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var at = UtcDateTime.ToUtc(atUtc) ?? now;
        if (at == default) throw new ValidationException("atUtc không hợp lệ.");
        if (at > now + ClockSkew) throw new ValidationException("atUtc không được ở tương lai.");

        // Thiếu tham số là lỗi cấu hình của AdminService → 503 để phía gọi thử lại, không tự đoán chu kỳ.
        var config = await queries.FindConfigByKeyAsync(normalizedKey, at, cancellationToken)
            ?? throw new DependencyUnavailableException($"Chưa có tham số {normalizedKey}.");
        var effective = SystemConfigValueResolver.Resolve(config.DefaultValue, config.Changes, at);
        return new InternalSystemConfigValueDto(config.Key, effective.Value, at, effective.EffectiveFromUtc, effective.EffectiveFromUtc is null);
    }
}
