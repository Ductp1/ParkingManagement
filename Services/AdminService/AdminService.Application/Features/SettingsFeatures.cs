namespace AdminService.Application.Features;

public sealed record SystemConfigDto(string Key, string Value, string DataType, string? Description, DateTime? UpdatedAtUtc);
public sealed record FeatureFlagDto(string Key, bool IsEnabled, string? Description);
public sealed record PlatformSettingsDto(IReadOnlyList<SystemConfigDto> Configs, IReadOnlyList<FeatureFlagDto> FeatureFlags);

public interface ISettingsQueries
{
    Task<IReadOnlyList<SystemConfigDto>> ListConfigsAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<FeatureFlagDto>> ListFeatureFlagsAsync(CancellationToken cancellationToken);
}

public interface IGetPlatformSettingsUseCase
{
    Task<PlatformSettingsDto> ExecuteAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// UC-44: Tham số nghiệp vụ + feature flag. Các service khác đọc API này (hoặc cache) thay vì hard-code
/// giá trị như 15 phút giữ chỗ, 60 phút Cancellation Window, hoa hồng 10%...
/// </summary>
public sealed class GetPlatformSettingsUseCase(ISettingsQueries queries) : IGetPlatformSettingsUseCase
{
    public async Task<PlatformSettingsDto> ExecuteAsync(CancellationToken cancellationToken = default)
        => new(await queries.ListConfigsAsync(cancellationToken), await queries.ListFeatureFlagsAsync(cancellationToken));
}
