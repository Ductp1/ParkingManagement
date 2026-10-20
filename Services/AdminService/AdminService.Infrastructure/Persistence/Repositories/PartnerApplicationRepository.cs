using AdminService.Application.Features.PartnerOnboarding;
using AdminService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using ParkingManagement.SharedKernel.Exceptions;

namespace AdminService.Infrastructure.Persistence.Repositories;

/// <summary>US-116: ghi hồ sơ đối tác. Thay đổi hồ sơ + AuditLog lưu trong một SaveChanges; RowVersion lệch → 409.</summary>
public sealed class PartnerApplicationRepository(AdminDbContext db) : IPartnerApplicationRepository
{
    public Task<PartnerApplication?> FindTrackedAsync(int id, CancellationToken cancellationToken)
        => db.PartnerApplications.Include(a => a.ContractDrafts).FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

    public Task<ContractTemplate?> GetActiveTemplateAsync(CancellationToken cancellationToken)
        => db.ContractTemplates.AsNoTracking().FirstOrDefaultAsync(t => t.IsActive, cancellationToken);

    public void AddOwnerContract(OwnerContract contract) => db.OwnerContracts.Add(contract);

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
            foreach (var contract in db.ChangeTracker.Entries<OwnerContract>().Where(e => e.State == EntityState.Added).ToList())
                contract.State = EntityState.Detached;
            throw new ConflictException("Hồ sơ đối tác vừa được Admin khác cập nhật, vui lòng tải lại và thử lại.");
        }
    }
}
