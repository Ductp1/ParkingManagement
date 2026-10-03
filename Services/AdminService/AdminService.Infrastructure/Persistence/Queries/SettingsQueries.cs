using AdminService.Application.Features;
using Microsoft.EntityFrameworkCore;

namespace AdminService.Infrastructure.Persistence.Queries;

public sealed class SettingsQueries(AdminDbContext db) : ISettingsQueries
{
    public async Task<IReadOnlyList<SystemConfigDto>> ListConfigsAsync(CancellationToken cancellationToken)
        => await db.SystemConfigs.AsNoTracking()
            .OrderBy(c => c.Key)
            .Select(c => new SystemConfigDto(c.Key, c.Value, c.DataType, c.Description, c.UpdatedAtUtc))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<FeatureFlagDto>> ListFeatureFlagsAsync(CancellationToken cancellationToken)
        => await db.FeatureFlags.AsNoTracking()
            .OrderBy(f => f.Key)
            .Select(f => new FeatureFlagDto(f.Key, f.IsEnabled, f.Description))
            .ToListAsync(cancellationToken);
}
