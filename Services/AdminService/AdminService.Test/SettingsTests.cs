using AdminService.Application.Features;

namespace AdminService.Test;

public class SettingsTests
{
    [Fact]
    public async Task Settings_combine_configs_and_feature_flags()
    {
        var result = await new GetPlatformSettingsUseCase(new FakeQueries()).ExecuteAsync();
        Assert.Contains(result.Configs, c => c.Key == "CANCELLATION_WINDOW_MINUTES" && c.Value == "60");
        Assert.Contains(result.FeatureFlags, f => f.Key == "FEATURE_3D_MAP" && !f.IsEnabled);
    }

    private sealed class FakeQueries : ISettingsQueries
    {
        public Task<IReadOnlyList<SystemConfigDto>> ListConfigsAsync(CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<SystemConfigDto>>([new("CANCELLATION_WINDOW_MINUTES", "60", "int", null, null)]);

        public Task<IReadOnlyList<FeatureFlagDto>> ListFeatureFlagsAsync(CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<FeatureFlagDto>>([new("FEATURE_3D_MAP", false, null)]);
    }
}
