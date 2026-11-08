using AdminService.Application.Features.Commission;
using AdminService.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace AdminService.Infrastructure.Persistence.Queries;

/// <summary>
/// US-079: phiên bản đã ký của hợp đồng bãi (AsNoTracking, Select ở database). Lọc theo bãi dùng index IX_LotContracts_SingleOpenPerLot;
/// quy tắc chọn tỷ lệ nằm ở Application (CommissionRateResolver).
/// </summary>
public sealed class CommissionRateQueries(AdminDbContext db) : ICommissionRateQueries
{
    public async Task<IReadOnlyList<SignedLotContractRate>> ListSignedContractsAsync(IReadOnlyCollection<int>? parkingLotIds,
        CancellationToken cancellationToken)
        => await SignedContracts(db, parkingLotIds).ToListAsync(cancellationToken);

    /// <summary>Truy vấn dùng chung cho repository và test sinh SQL (ToQueryString).</summary>
    public static IQueryable<SignedLotContractRate> SignedContracts(AdminDbContext db, IReadOnlyCollection<int>? parkingLotIds)
    {
        var query = db.LotContractVersions.AsNoTracking()
            .Where(v => v.Status == LotContractVersionStatus.Signed
                        && v.LotContract.Status == LotContractStatus.Signed
                        && v.LotContract.SignedAtUtc != null);
        if (parkingLotIds is not null)
        {
            // List<int>.Contains (phương thức của List) – tránh array.Contains bị C# 14 gắn sang overload Span trong expression tree.
            var ids = parkingLotIds.ToList();
            query = query.Where(v => ids.Contains(v.LotContract.ParkingLotId));
        }

        return query
            .OrderBy(v => v.LotContract.ParkingLotId).ThenBy(v => v.LotContractId)
            .Select(v => new SignedLotContractRate(v.LotContractId, v.LotContract.ContractNumber, v.LotContract.ParkingLotId,
                v.LotContract.OwnerProfileId, v.LotContract.BusinessName, v.LotContract.LotName, v.LotContract.LotDistrict, v.Version,
                v.CommissionRate, v.EffectiveFrom, v.EffectiveTo, v.LotContract.SignedAtUtc!.Value));
    }
}
