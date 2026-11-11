using AdminService.Application.Features.PartnerOnboarding;
using AdminService.Domain.Entities;
using AdminService.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace AdminService.Infrastructure.Persistence.Queries;

/// <summary>US-080: tài khoản nhận tiền quyết toán theo chủ bãi – chỉ đọc hồ sơ đối tác (index CreatedOwnerProfileId).</summary>
public sealed class OwnerPayoutAccountQueries(AdminDbContext db) : IOwnerPayoutAccountQueries
{
    public async Task<IReadOnlyList<OwnerPayoutAccountRow>> ListAsync(IReadOnlyCollection<int> ownerProfileIds,
        CancellationToken cancellationToken)
    {
        if (ownerProfileIds.Count == 0) return [];
        return await Shape(db.PartnerApplications.AsNoTracking(), ownerProfileIds).ToListAsync(cancellationToken);
    }

    /// <summary>Lọc + chiếu ở database (tách riêng để test SQL bằng ToQueryString).</summary>
    public static IQueryable<OwnerPayoutAccountRow> Shape(IQueryable<PartnerApplication> applications, IReadOnlyCollection<int> ownerProfileIds)
        => applications
            .Where(a => a.CreatedOwnerProfileId != null && ownerProfileIds.Contains(a.CreatedOwnerProfileId.Value)
                        && (a.Status == PartnerApplicationStatus.AccountIssued || a.Status == PartnerApplicationStatus.Activated)
                        && a.PayoutBankBin != null && a.PayoutBankName != null
                        && a.PayoutBankAccountNumber != null && a.PayoutBankAccountName != null)
            .OrderBy(a => a.CreatedOwnerProfileId).ThenByDescending(a => a.Id)
            .Select(a => new OwnerPayoutAccountRow(a.CreatedOwnerProfileId!.Value, a.Id, a.Code, a.BusinessName,
                a.PayoutBankBin!, a.PayoutBankName!, a.PayoutBankAccountNumber!, a.PayoutBankAccountName!));
}
