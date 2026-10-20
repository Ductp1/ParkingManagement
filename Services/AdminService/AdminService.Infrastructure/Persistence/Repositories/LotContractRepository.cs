using AdminService.Application.Features.LotContracts;
using AdminService.Domain.Entities;
using AdminService.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using ParkingManagement.SharedKernel.Exceptions;

namespace AdminService.Infrastructure.Persistence.Repositories;

/// <summary>US-116: ghi hợp đồng bãi. Hợp đồng + phiên bản + AuditLog lưu trong một SaveChanges; RowVersion lệch → 409.</summary>
public sealed class LotContractRepository(AdminDbContext db) : ILotContractRepository
{
    public Task<LotContract?> FindTrackedAsync(int id, CancellationToken cancellationToken)
        => db.LotContracts.Include(c => c.Versions).FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

    public Task<PartnerApplication?> FindPartnerByOwnerProfileAsync(int ownerProfileId, CancellationToken cancellationToken)
        => db.PartnerApplications.AsNoTracking()
            .Where(a => a.CreatedOwnerProfileId == ownerProfileId
                        && (a.Status == PartnerApplicationStatus.AccountIssued || a.Status == PartnerApplicationStatus.Activated))
            .OrderByDescending(a => a.Id)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<bool> HasOpenContractAsync(int parkingLotId, CancellationToken cancellationToken)
        => db.LotContracts.AnyAsync(c => c.ParkingLotId == parkingLotId && c.Status != LotContractStatus.Cancelled, cancellationToken);

    public Task<int> CountForLotAsync(int parkingLotId, CancellationToken cancellationToken)
        => db.LotContracts.CountAsync(c => c.ParkingLotId == parkingLotId, cancellationToken);

    public Task<ContractTemplate?> GetActiveTemplateAsync(CancellationToken cancellationToken)
        => db.ContractTemplates.AsNoTracking().FirstOrDefaultAsync(t => t.IsActive, cancellationToken);

    public void Add(LotContract contract) => db.LotContracts.Add(contract);

    public async Task SaveWithAuditAsync(AuditLog auditLog, CancellationToken cancellationToken)
    {
        db.AuditLogs.Add(auditLog);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            db.Entry(auditLog).State = EntityState.Detached;
            throw new ConflictException("Hợp đồng bãi vừa được Admin khác cập nhật, vui lòng tải lại và thử lại.");
        }
    }
}
