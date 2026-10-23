using AdminService.Application.Features.ContractTemplates;
using AdminService.Domain.Entities;
using AdminService.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using ParkingManagement.SharedKernel.Exceptions;

namespace AdminService.Infrastructure.Persistence.Repositories;

/// <summary>
/// US-116: ghi mẫu hợp đồng bãi. Thay đổi + AuditLog lưu trong một SaveChanges; RowVersion lệch hoặc vi phạm index duy nhất
/// (trùng số phiên bản do hai Admin tạo cùng lúc) → 409.
/// </summary>
public sealed class ContractTemplateRepository(AdminDbContext db) : IContractTemplateRepository
{
    public Task<ContractTemplate?> FindTrackedAsync(int id, CancellationToken cancellationToken)
        => db.ContractTemplates.FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

    public Task<ContractTemplate?> FindLatestActiveAsync(CancellationToken cancellationToken)
        => db.ContractTemplates.AsNoTracking()
            .Where(t => t.Status == ContractTemplateStatus.Active && t.PlatformName != "")
            .OrderByDescending(t => t.ActivatedAtUtc).ThenByDescending(t => t.Version)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<bool> NameInUseAsync(string name, int? excludeId, CancellationToken cancellationToken)
    {
        var lower = name.ToLower();
        return db.ContractTemplates.AnyAsync(t => t.Status != ContractTemplateStatus.Retired && t.Name.ToLower() == lower
                                                  && (excludeId == null || t.Id != excludeId), cancellationToken);
    }

    public async Task<int> MaxVersionAsync(CancellationToken cancellationToken)
        => await db.ContractTemplates.MaxAsync(t => (int?)t.Version, cancellationToken) ?? 0;

    public void Add(ContractTemplate template) => db.ContractTemplates.Add(template);

    public void Remove(ContractTemplate template) => db.ContractTemplates.Remove(template);

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
            throw new ConflictException("Mẫu hợp đồng vừa được Admin khác cập nhật, vui lòng tải lại và thử lại.");
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            db.Entry(auditLog).State = EntityState.Detached;
            throw new ConflictException("Admin khác vừa tạo mẫu cùng số phiên bản, vui lòng thử lại.");
        }
    }
}
