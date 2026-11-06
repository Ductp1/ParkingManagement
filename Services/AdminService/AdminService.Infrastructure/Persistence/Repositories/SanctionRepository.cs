using AdminService.Application.Features.Sanctions;
using AdminService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using ParkingManagement.SharedKernel.Enums;
using ParkingManagement.SharedKernel.Exceptions;

namespace AdminService.Infrastructure.Persistence.Repositories;

/// <summary>US-097: cài đặt port ISanctionRepository bằng AdminDbContext (Application không đụng EF Core).</summary>
public sealed class SanctionRepository(AdminDbContext db) : ISanctionRepository
{
    public Task<Sanction?> FindTrackedAsync(int id, CancellationToken cancellationToken)
        => db.Sanctions.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

    // Điều kiện phải trùng với filter của unique index IX_Sanctions_ParkingLotId_Level_Active.
    public async Task<IReadOnlyList<Sanction>> FindActiveLotSanctionsTrackedAsync(IReadOnlyCollection<int> parkingLotIds, SanctionLevel level,
        CancellationToken cancellationToken)
        => await db.Sanctions
            .Where(s => s.Status == SanctionStatus.Active && s.Level == level && s.ParkingLotId != null
                && parkingLotIds.Contains(s.ParkingLotId.Value))
            .ToListAsync(cancellationToken);

    // Điều kiện phải trùng với filter của unique index IX_Sanctions_OwnerProfileId_ActiveOwnerRankingPenalty.
    public Task<Sanction?> FindActiveOwnerRankingPenaltyTrackedAsync(int ownerProfileId, CancellationToken cancellationToken)
        => db.Sanctions.FirstOrDefaultAsync(s => s.OwnerProfileId == ownerProfileId && s.Status == SanctionStatus.Active
            && s.ParkingLotId == null && s.Level == SanctionLevel.RankingPenalty, cancellationToken);

    public Task<RiskFlag?> FindRiskFlagTrackedAsync(int id, CancellationToken cancellationToken)
        => db.RiskFlags.FirstOrDefaultAsync(f => f.Id == id, cancellationToken);

    public async Task AddIssuedAsync(IReadOnlyList<Sanction> sanctions, Func<Sanction, AuditLog> buildAuditLog,
        Action<IReadOnlyList<Sanction>>? afterInsert, CancellationToken cancellationToken)
    {
        try
        {
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

            // Chế tài cũ vừa được đóng (quá hạn) phải rời khỏi unique index trước khi chèn chế tài mới.
            if (db.ChangeTracker.HasChanges()) await db.SaveChangesAsync(cancellationToken);

            db.Sanctions.AddRange(sanctions);
            await db.SaveChangesAsync(cancellationToken);           // sinh Id cho audit log + RiskFlag.SanctionId

            db.AuditLogs.AddRange(sanctions.Select(buildAuditLog));
            afterInsert?.Invoke(sanctions);
            await db.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);       // chế tài + audit log: cùng 1 transaction
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // Partial unique index chặn 2 request áp cùng chế tài lên một bãi / chủ bãi cùng lúc.
            throw new ConflictException("Bãi / chủ bãi vừa được áp chế tài cùng cấp bởi một yêu cầu khác. Tải lại danh sách rồi thử lại.");
        }
    }

    public async Task SaveWithAuditAsync(AuditLog auditLog, CancellationToken cancellationToken)
    {
        db.AuditLogs.Add(auditLog);
        await db.SaveChangesAsync(cancellationToken);       // đổi trạng thái chế tài + audit log: cùng 1 lần SaveChanges
    }
}
