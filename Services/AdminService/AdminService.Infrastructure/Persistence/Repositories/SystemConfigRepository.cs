using AdminService.Application.Features;
using AdminService.Application.Features.Settings;
using AdminService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using ParkingManagement.SharedKernel.Exceptions;

namespace AdminService.Infrastructure.Persistence.Repositories;

/// <summary>Cài đặt port ISystemConfigRepository bằng AdminDbContext (Application không đụng EF Core).</summary>
public sealed class SystemConfigRepository(AdminDbContext db) : ISystemConfigRepository
{
    public async Task<SystemConfigRecord?> FindByKeyWithChangesAsync(string key, CancellationToken cancellationToken)
    {
        var config = await db.SystemConfigs.AsNoTracking()
            .Where(c => c.Key == key)
            .Select(c => new { c.Id, c.Key, c.Value, c.DataType, c.Description })
            .FirstOrDefaultAsync(cancellationToken);
        if (config is null) return null;

        var changes = await db.SystemConfigChanges.AsNoTracking()
            .Where(h => h.SystemConfigId == config.Id)
            .Select(h => new SystemConfigChangePoint(h.Id, h.Value, h.EffectiveFromUtc, h.CancelledAtUtc))
            .ToListAsync(cancellationToken);
        return new SystemConfigRecord(config.Id, config.Key, config.Value, config.DataType, config.Description, changes);
    }

    public async Task AddChangeAsync(SystemConfigChange change, AuditLog auditLog, CancellationToken cancellationToken)
    {
        db.SystemConfigChanges.Add(change);
        db.AuditLogs.Add(auditLog);
        try
        {
            await db.SaveChangesAsync(cancellationToken);   // thay đổi tham số + audit log: cùng 1 lần SaveChanges
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // IX_SystemConfigChanges_SystemConfigId_EffectiveFromUtc_Active chặn 2 request đặt cùng mốc hiệu lực (chống race).
            throw new ConflictException("Tham số này vừa có một thay đổi cùng thời điểm hiệu lực từ một yêu cầu khác.");
        }
    }

    public Task<SystemConfigChange?> FindChangeTrackedAsync(int changeId, CancellationToken cancellationToken)
        => db.SystemConfigChanges.FirstOrDefaultAsync(c => c.Id == changeId, cancellationToken);

    public async Task SaveWithAuditAsync(AuditLog auditLog, CancellationToken cancellationToken)
    {
        db.AuditLogs.Add(auditLog);
        await db.SaveChangesAsync(cancellationToken);       // đóng dấu hủy + audit log: cùng 1 lần SaveChanges
    }
}
