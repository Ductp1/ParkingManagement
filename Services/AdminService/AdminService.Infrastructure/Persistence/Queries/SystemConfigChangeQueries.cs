using AdminService.Application.Features.Settings;
using Microsoft.EntityFrameworkCore;

namespace AdminService.Infrastructure.Persistence.Queries;

/// <summary>Chỉ lấy dữ liệu: trạng thái và giá trị liền trước của từng dòng do Application tính (SystemConfigChangeTimeline).</summary>
public sealed class SystemConfigChangeQueries(AdminDbContext db) : ISystemConfigChangeQueries
{
    public async Task<SystemConfigHistoryRecord?> FindHistoryByKeyAsync(string key, CancellationToken cancellationToken)
    {
        var config = await db.SystemConfigs.AsNoTracking()
            .Where(c => c.Key == key)
            .Select(c => new { c.Id, c.Key, c.Value })
            .FirstOrDefaultAsync(cancellationToken);
        if (config is null) return null;

        var changes = await db.SystemConfigChanges.AsNoTracking()
            .Where(h => h.SystemConfigId == config.Id)
            .Select(h => new SystemConfigChangeRow(h.Id, h.Value, h.EffectiveFromUtc, h.Reason, h.CreatedByUserId, h.CreatedAtUtc,
                h.CancelledAtUtc, h.CancelledByUserId, h.CancelReason))
            .ToListAsync(cancellationToken);
        return new SystemConfigHistoryRecord(config.Key, config.Value, changes);
    }
}
