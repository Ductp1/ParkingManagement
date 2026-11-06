using AdminService.Application.Features.Owners;
using AdminService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using ParkingManagement.SharedKernel.Enums;
using ParkingManagement.SharedKernel.Exceptions;

namespace AdminService.Infrastructure.Persistence.Repositories;

/// <summary>Cài đặt port IOwnerLockRepository bằng AdminDbContext (Application không đụng EF Core).</summary>
public sealed class OwnerLockRepository(AdminDbContext db) : IOwnerLockRepository
{
    // Điều kiện phải trùng với filter của unique index IX_Sanctions_OwnerProfileId_ActiveOwnerLock.
    public Task<Sanction?> FindActiveOwnerLockTrackedAsync(int ownerProfileId, CancellationToken cancellationToken)
        => db.Sanctions.FirstOrDefaultAsync(s => s.OwnerProfileId == ownerProfileId
            && s.Status == SanctionStatus.Active
            && s.ParkingLotId == null
            && (s.Level == SanctionLevel.TemporarySuspension || s.Level == SanctionLevel.PermanentBan), cancellationToken);

    public async Task AddLockAsync(Sanction sanction, AuditLog auditLog, CancellationToken cancellationToken)
    {
        try
        {
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

            // Chế tài cũ vừa bị đóng phải rời khỏi unique index trước khi chèn chế tài mới.
            if (db.ChangeTracker.HasChanges()) await db.SaveChangesAsync(cancellationToken);

            db.Sanctions.Add(sanction);
            db.AuditLogs.Add(auditLog);
            await db.SaveChangesAsync(cancellationToken);   // chế tài + audit log: cùng 1 lần SaveChanges

            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // IX_Sanctions_OwnerProfileId_ActiveOwnerLock chặn 2 request khóa cùng lúc (chống race).
            throw new ConflictException($"Chủ bãi {sanction.OwnerProfileId} đang được khóa bởi một yêu cầu khác.");
        }
    }

    public Task<Sanction?> FindLatestOwnerLockTrackedAsync(int ownerProfileId, CancellationToken cancellationToken)
        => db.Sanctions
            .Where(s => s.OwnerProfileId == ownerProfileId
                && s.ParkingLotId == null
                && (s.Level == SanctionLevel.TemporarySuspension || s.Level == SanctionLevel.PermanentBan))
            .OrderByDescending(s => s.Id)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task SaveWithAuditAsync(AuditLog auditLog, CancellationToken cancellationToken)
    {
        db.AuditLogs.Add(auditLog);
        await db.SaveChangesAsync(cancellationToken);       // đổi trạng thái chế tài + audit log: cùng 1 lần SaveChanges
    }

    public Task SaveAsync(CancellationToken cancellationToken) => db.SaveChangesAsync(cancellationToken);
}
