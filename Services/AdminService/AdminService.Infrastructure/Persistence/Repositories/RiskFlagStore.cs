using AdminService.Application.Features.RiskFlags;
using AdminService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using ParkingManagement.SharedKernel.Enums;

namespace AdminService.Infrastructure.Persistence.Repositories;

/// <summary>ADAPTER IRiskFlagStore bằng AdminDbContext (US-077) – dùng index (SubjectType, SubjectId) có sẵn.</summary>
public sealed class RiskFlagStore(AdminDbContext db) : IRiskFlagStore
{
    public Task<RiskFlag?> FindPendingAsync(RiskSubjectType subjectType, int subjectId, string ruleCode,
        CancellationToken cancellationToken = default)
        => db.RiskFlags
            .Where(f => f.SubjectType == subjectType && f.SubjectId == subjectId && f.RuleCode == ruleCode
                        && (f.Status == RiskFlagStatus.Open || f.Status == RiskFlagStatus.Investigating))
            .OrderByDescending(f => f.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task AddAsync(RiskFlag flag, CancellationToken cancellationToken = default)
        => await db.RiskFlags.AddAsync(flag, cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) => db.SaveChangesAsync(cancellationToken);
}
