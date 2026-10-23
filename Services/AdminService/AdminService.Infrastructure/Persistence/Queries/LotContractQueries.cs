using AdminService.Application.Features.LotContracts;
using AdminService.Application.Features.PartnerOnboarding;
using AdminService.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using ParkingManagement.SharedKernel.Contracts;

namespace AdminService.Infrastructure.Persistence.Queries;

/// <summary>US-116: đọc hợp đồng bãi (AsNoTracking, Select thẳng sang DTO, phân trang Skip/Take).</summary>
public sealed class LotContractQueries(AdminDbContext db) : ILotContractQueries
{
    public async Task<PagedResult<LotContractSummaryDto>> ListAsync(LotContractStatus? status, int? parkingLotId, int? ownerProfileId,
        string? search, int page, int pageSize, CancellationToken cancellationToken)
    {
        var query = db.LotContracts.AsNoTracking();
        if (status is { } s) query = query.Where(c => c.Status == s);
        if (parkingLotId is { } lotId) query = query.Where(c => c.ParkingLotId == lotId);
        if (ownerProfileId is { } ownerId) query = query.Where(c => c.OwnerProfileId == ownerId);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{search.Trim()}%";
            query = query.Where(c => EF.Functions.ILike(c.ContractNumber, pattern) || EF.Functions.ILike(c.LotName, pattern)
                                     || EF.Functions.ILike(c.BusinessName, pattern) || EF.Functions.ILike(c.TaxCode, pattern));
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(c => c.UpdatedAtUtc ?? c.CreatedAtUtc).ThenByDescending(c => c.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(c => new
            {
                c.Id, c.ContractNumber, c.Status, c.ParkingLotId, c.KybApplicationId, c.LotName, c.LotCity, c.OwnerProfileId,
                c.BusinessName, c.CreatedAtUtc, c.UpdatedAtUtc,
                Latest = c.Versions.OrderByDescending(v => v.Version)
                    .Select(v => new { v.Version, v.CommissionRate, v.EffectiveFrom, v.EffectiveTo }).FirstOrDefault(),
            })
            .ToListAsync(cancellationToken);

        return new PagedResult<LotContractSummaryDto>(items.Select(c => new LotContractSummaryDto(c.Id, c.ContractNumber,
            c.Status.ToString(), c.ParkingLotId, c.KybApplicationId, c.LotName, c.LotCity, c.OwnerProfileId, c.BusinessName,
            c.Latest?.Version, c.Latest?.CommissionRate, c.Latest?.EffectiveFrom, c.Latest?.EffectiveTo, c.CreatedAtUtc,
            c.UpdatedAtUtc)).ToList(), page, pageSize, total);
    }

    public Task<LotContractDetailDto?> GetDetailAsync(int id, CancellationToken cancellationToken)
        => db.LotContracts.AsNoTracking()
            .Where(c => c.Id == id)
            .Select(c => new LotContractDetailDto(c.Id, c.ContractNumber, c.Status.ToString(), c.ParkingLotId, c.KybApplicationId,
                c.LotName, c.LotAddress, c.LotCity, c.LotDistrict, c.LotTotalSlots, c.LotOpeningHours, c.OwnerProfileId,
                c.PartnerApplicationId, c.ApplicationCode, c.BusinessName, c.TaxCode, c.BusinessAddress, c.LegalRepresentativeName,
                c.LegalRepresentativePosition, c.Email, c.Phone,
                new PartnerBankAccountDto(c.BankBin, c.BankName, c.BankAccountNumber, c.BankAccountName),
                c.SignedFileContentType, c.SignedFileSha256, c.SignedAtUtc, c.CancelReason, c.CreatedByAdminId, c.CreatedAtUtc,
                c.UpdatedAtUtc,
                c.Versions.OrderByDescending(v => v.Version)
                    .Select(v => new LotContractVersionDto(v.Id, v.Version, v.TemplateVersion, v.CommissionRate, v.EffectiveFrom,
                        v.EffectiveTo, v.TermMonths, v.FileContentType, v.FileSha256, v.Status.ToString(), v.RevisionReason,
                        v.CreatedAtUtc, v.CreatedByAdminId, v.SpecialTerms, v.TemplateId,
                        db.ContractTemplates.Where(t => t.Version == v.TemplateVersion).Select(t => t.Name).FirstOrDefault(),
                        db.ContractTemplates.Where(t => t.Version == v.TemplateVersion).Select(t => t.Kind.ToString()).FirstOrDefault(),
                        db.ContractTemplates.Where(t => t.Version == v.TemplateVersion).Select(t => t.Status.ToString()).FirstOrDefault()))
                    .ToList()))
            .FirstOrDefaultAsync(cancellationToken);

    public Task<LotContractVersionFileInfo?> GetVersionFileAsync(int id, int version, CancellationToken cancellationToken)
        => db.LotContractVersions.AsNoTracking()
            .Where(v => v.LotContractId == id && v.Version == version)
            .Select(v => new LotContractVersionFileInfo(v.LotContract.ContractNumber, v.Version, v.FileKey, v.FileContentType,
                v.FileSha256))
            .FirstOrDefaultAsync(cancellationToken);
}
