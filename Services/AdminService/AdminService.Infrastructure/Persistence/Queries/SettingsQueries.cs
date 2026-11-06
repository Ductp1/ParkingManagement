using AdminService.Application.Features;
using AdminService.Application.Features.Settings;
using Microsoft.EntityFrameworkCore;

namespace AdminService.Infrastructure.Persistence.Queries;

/// <summary>Chỉ lấy dữ liệu: giá trị hiệu lực do Application tính (SystemConfigValueResolver).</summary>
public sealed class SettingsQueries(AdminDbContext db) : ISettingsQueries
{
    public async Task<IReadOnlyList<SystemConfigRecord>> ListConfigsAsync(DateTime changesUpToUtc, CancellationToken cancellationToken)
    {
        var configs = await db.SystemConfigs.AsNoTracking()
            .OrderBy(c => c.Key)
            .Select(c => new { c.Id, c.Key, c.Value, c.DataType, c.Description })
            .ToListAsync(cancellationToken);
        var changes = await db.SystemConfigChanges.AsNoTracking()
            .Where(h => h.EffectiveFromUtc <= changesUpToUtc)
            .Select(h => new { h.SystemConfigId, Point = new SystemConfigChangePoint(h.Id, h.Value, h.EffectiveFromUtc, h.CancelledAtUtc) })
            .ToListAsync(cancellationToken);

        var changesByConfig = changes.ToLookup(h => h.SystemConfigId, h => h.Point);
        return [.. configs.Select(c => new SystemConfigRecord(c.Id, c.Key, c.Value, c.DataType, c.Description, [.. changesByConfig[c.Id]]))];
    }

    public async Task<SystemConfigRecord?> FindConfigByKeyAsync(string key, DateTime changesUpToUtc, CancellationToken cancellationToken)
    {
        var config = await db.SystemConfigs.AsNoTracking()
            .Where(c => c.Key == key)
            .Select(c => new { c.Id, c.Key, c.Value, c.DataType, c.Description })
            .FirstOrDefaultAsync(cancellationToken);
        if (config is null) return null;

        var changes = await db.SystemConfigChanges.AsNoTracking()
            .Where(h => h.SystemConfigId == config.Id && h.EffectiveFromUtc <= changesUpToUtc)
            .Select(h => new SystemConfigChangePoint(h.Id, h.Value, h.EffectiveFromUtc, h.CancelledAtUtc))
            .ToListAsync(cancellationToken);
        return new SystemConfigRecord(config.Id, config.Key, config.Value, config.DataType, config.Description, changes);
    }

    public async Task<IReadOnlyList<FeatureFlagDto>> ListFeatureFlagsAsync(CancellationToken cancellationToken)
        => await db.FeatureFlags.AsNoTracking()
            .OrderBy(f => f.Key)
            .Select(f => new FeatureFlagDto(f.Key, f.IsEnabled, f.Description))
            .ToListAsync(cancellationToken);
}
