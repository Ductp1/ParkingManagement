using AdminService.Application.Features.Users;
using AdminService.Domain.Entities;

namespace AdminService.Infrastructure.Persistence.Repositories;

public sealed class AdminAuditWriter(AdminDbContext db) : IAdminAuditWriter
{
    public async Task WriteAsync(AuditLog auditLog, CancellationToken cancellationToken)
    {
        db.AuditLogs.Add(auditLog);
        await db.SaveChangesAsync(cancellationToken);
    }
}
