using AdminService.Application.Features;
using AdminService.Application.Features.Settings;
using AdminService.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace AdminService.Infrastructure.Persistence.Repositories;

/// <summary>Cài đặt port IFeatureFlagRepository bằng AdminDbContext (Application không đụng EF Core).</summary>
public sealed class FeatureFlagRepository(AdminDbContext db) : IFeatureFlagRepository
{
    public Task<FeatureFlagDto?> FindByKeyAsync(string key, CancellationToken cancellationToken)
        => db.FeatureFlags.AsNoTracking()
            .Where(f => f.Key == key)
            .Select(f => new FeatureFlagDto(f.Key, f.IsEnabled, f.Description, f.UpdatedByUserId, f.UpdatedAtUtc))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<bool> TryChangeAsync(string key, bool isEnabled, int performedByUserId, DateTime nowUtc, AuditLog auditLog, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        // UPDATE có điều kiện: 2 request đồng thời thì request sau khớp 0 dòng. ExecuteUpdate bỏ qua SaveChanges nên tự đóng dấu UpdatedAtUtc.
        var changed = await db.FeatureFlags
            .Where(f => f.Key == key && f.IsEnabled != isEnabled)
            .ExecuteUpdateAsync(s => s
                .SetProperty(f => f.IsEnabled, isEnabled)
                .SetProperty(f => f.UpdatedByUserId, performedByUserId)
                .SetProperty(f => f.UpdatedAtUtc, nowUtc), cancellationToken);
        if (changed == 0) return false;

        db.AuditLogs.Add(auditLog);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }
}
