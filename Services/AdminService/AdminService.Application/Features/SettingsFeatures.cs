using AdminService.Application.Features.Settings;
using ParkingManagement.SharedKernel.Exceptions;

namespace AdminService.Application.Features;

// ===== DTO =====
/// <summary>Value = giá trị hiệu lực tại thời điểm hỏi; UpdatedAtUtc = mốc giá trị đó bắt đầu hiệu lực (null = vẫn là giá trị gốc).</summary>
public sealed record SystemConfigDto(string Key, string Value, string DataType, string? Description, DateTime? UpdatedAtUtc);
public sealed record FeatureFlagDto(string Key, bool IsEnabled, string? Description);
public sealed record PlatformSettingsDto(IReadOnlyList<SystemConfigDto> Configs, IReadOnlyList<FeatureFlagDto> FeatureFlags);

/// <summary>Chi tiết một tham số (US-098). EffectiveFromUtc = null khi Value vẫn là DefaultValue.</summary>
public sealed record SystemConfigDetailDto(string Key, string Value, string DataType, string? Description, string DefaultValue,
    DateTime? EffectiveFromUtc);

// ===== PORT (đọc) =====
/// <summary>Tham số đúng như lưu trong pm_admin: giá trị gốc + các dòng lịch sử thay đổi (chưa tính giá trị hiệu lực).</summary>
public sealed record SystemConfigRecord(int Id, string Key, string DefaultValue, string DataType, string? Description,
    IReadOnlyList<SystemConfigChangePoint> Changes);

public interface ISettingsQueries
{
    /// <summary>Mọi tham số, kèm các thay đổi có mốc hiệu lực ≤ <paramref name="changesUpToUtc"/> (UTC).</summary>
    Task<IReadOnlyList<SystemConfigRecord>> ListConfigsAsync(DateTime changesUpToUtc, CancellationToken cancellationToken);
    /// <summary>Một tham số theo khóa (khớp chính xác), kèm các thay đổi có mốc hiệu lực ≤ <paramref name="changesUpToUtc"/> (UTC).</summary>
    Task<SystemConfigRecord?> FindConfigByKeyAsync(string key, DateTime changesUpToUtc, CancellationToken cancellationToken);
    Task<IReadOnlyList<FeatureFlagDto>> ListFeatureFlagsAsync(CancellationToken cancellationToken);
}

// ===== USE CASE (đọc) =====
public interface IGetPlatformSettingsUseCase
{
    Task<PlatformSettingsDto> ExecuteAsync(DateTime? at = null, CancellationToken cancellationToken = default);
}

/// <summary>
/// UC-44: Tham số nghiệp vụ + feature flag. Các service khác đọc API này (hoặc cache) thay vì hard-code
/// giá trị như 15 phút giữ chỗ, 60 phút Cancellation Window, hoa hồng 10%...
/// US-098: giá trị tham số là giá trị hiệu lực tại <c>at</c> (bỏ trống = hiện tại). Service tạo booking lúc T gọi với at = T
/// để chốt tham số theo lúc tạo – thay đổi sau đó của Admin không ảnh hưởng booking đã tạo. Feature flag không theo thời điểm.
/// </summary>
public sealed class GetPlatformSettingsUseCase(ISettingsQueries queries, TimeProvider timeProvider) : IGetPlatformSettingsUseCase
{
    public async Task<PlatformSettingsDto> ExecuteAsync(DateTime? at = null, CancellationToken cancellationToken = default)
    {
        var atUtc = UtcDateTime.ToUtc(at) ?? timeProvider.GetUtcNow().UtcDateTime;
        var configs = await queries.ListConfigsAsync(atUtc, cancellationToken);
        return new([.. configs.Select(c => SystemConfigMapper.ToDto(c, atUtc))], await queries.ListFeatureFlagsAsync(cancellationToken));
    }
}

public interface IGetSystemConfigByKeyUseCase
{
    Task<SystemConfigDetailDto> ExecuteAsync(string key, DateTime? at = null, CancellationToken cancellationToken = default);
}

/// <summary>US-098: Một tham số theo khóa, giá trị hiệu lực tại <c>at</c> (bỏ trống = hiện tại). Khóa không phân biệt hoa thường.</summary>
public sealed class GetSystemConfigByKeyUseCase(ISettingsQueries queries, TimeProvider timeProvider) : IGetSystemConfigByKeyUseCase
{
    public async Task<SystemConfigDetailDto> ExecuteAsync(string key, DateTime? at = null, CancellationToken cancellationToken = default)
    {
        var atUtc = UtcDateTime.ToUtc(at) ?? timeProvider.GetUtcNow().UtcDateTime;
        var normalizedKey = SystemConfigMapper.NormalizeKey(key);
        var config = await queries.FindConfigByKeyAsync(normalizedKey, atUtc, cancellationToken)
            ?? throw new NotFoundException("Tham số hệ thống", normalizedKey);
        return SystemConfigMapper.ToDetailDto(config, atUtc);
    }
}

/// <summary>Map tham số đọc từ pm_admin → DTO, giá trị tính bằng <see cref="SystemConfigValueResolver"/>.</summary>
public static class SystemConfigMapper
{
    /// <summary>Khóa tham số lưu dạng CHỮ_HOA; khóa nhận từ client được trim và đổi sang chữ hoa trước khi tra.</summary>
    public static string NormalizeKey(string? key) => (key ?? string.Empty).Trim().ToUpperInvariant();

    public static SystemConfigDto ToDto(SystemConfigRecord config, DateTime atUtc)
    {
        var effective = SystemConfigValueResolver.Resolve(config.DefaultValue, config.Changes, atUtc);
        return new(config.Key, effective.Value, config.DataType, config.Description, effective.EffectiveFromUtc);
    }

    public static SystemConfigDetailDto ToDetailDto(SystemConfigRecord config, DateTime atUtc)
    {
        var effective = SystemConfigValueResolver.Resolve(config.DefaultValue, config.Changes, atUtc);
        return new(config.Key, effective.Value, config.DataType, config.Description, config.DefaultValue, effective.EffectiveFromUtc);
    }
}
