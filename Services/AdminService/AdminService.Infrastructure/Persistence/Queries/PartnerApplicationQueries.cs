using AdminService.Application.Features.PartnerOnboarding;
using AdminService.Domain.Entities;
using AdminService.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using ParkingManagement.SharedKernel.Contracts;

namespace AdminService.Infrastructure.Persistence.Queries;

/// <summary>US-116: đọc hồ sơ đối tác (AsNoTracking, Select thẳng sang DTO, phân trang Skip/Take).</summary>
public sealed class PartnerApplicationQueries(AdminDbContext db) : IPartnerApplicationQueries
{
    public async Task<PagedResult<PartnerApplicationSummaryDto>> ListAsync(PartnerApplicationStatus? status, string? search, int page,
        int pageSize, CancellationToken cancellationToken)
    {
        var query = db.PartnerApplications.AsNoTracking();
        if (status is { } s) query = query.Where(a => a.Status == s);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{search.Trim()}%";
            query = query.Where(a => EF.Functions.ILike(a.Code, pattern) || EF.Functions.ILike(a.BusinessName, pattern)
                                     || EF.Functions.ILike(a.TaxCode, pattern) || EF.Functions.ILike(a.Email, pattern)
                                     || EF.Functions.ILike(a.Phone, pattern));
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(a => a.UpdatedAtUtc ?? a.CreatedAtUtc).ThenByDescending(a => a.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(a => new PartnerApplicationSummaryDto(a.Id, a.Code, a.BusinessName, a.TaxCode, a.FullName, a.Email, a.Phone,
                a.LotCount, a.Status.ToString(), a.ReviewerAdminId, a.ContractNumber, a.CreatedAtUtc, a.UpdatedAtUtc))
            .ToListAsync(cancellationToken);
        return new PagedResult<PartnerApplicationSummaryDto>(items, page, pageSize, total);
    }

    public async Task<PartnerApplicationDetailDto?> GetDetailAsync(int id, CancellationToken cancellationToken)
    {
        var row = await db.PartnerApplications.AsNoTracking()
            .Where(a => a.Id == id)
            .Select(a => new
            {
                a.Status,
                Dto = new PartnerApplicationDetailDto(a.Id, a.Code, a.Status.ToString(), a.FullName, a.Email, a.Phone, a.Position,
                    a.BusinessType.ToString(), a.BusinessName, a.TaxCode, a.BusinessAddress, a.LegalRepresentativeName,
                    a.LegalRepresentativePosition, a.BusinessLicenseUrl, a.LotCount, a.PrimaryLotAddress, a.EstimatedCarSlots,
                    a.EstimatedMotorbikeSlots, a.OperatingHours, a.ConsentAcceptedAtUtc, a.ReviewerAdminId, a.InfoRequestNote,
                    a.RejectReason, a.CancelReason, a.ContractNumber, a.CreatedUserId, a.CreatedOwnerProfileId, a.CreatedAtUtc,
                    a.UpdatedAtUtc, new List<string>(),
                    a.ContractDrafts.OrderByDescending(d => d.Version)
                        .Select(d => new ContractDraftDto(d.Id, d.Version, d.ContractNumber, d.TemplateVersion, d.CommissionRate,
                            d.EffectiveFrom, d.EffectiveTo, d.TermMonths, d.BankBin, d.BankName, d.BankAccountNumber,
                            d.BankAccountName, d.FileContentType, d.FileSha256, d.Status.ToString(), d.RevisionReason, d.SentAtUtc,
                            d.SentToEmail, d.CreatedByAdminId))
                        .ToList()),
            })
            .FirstOrDefaultAsync(cancellationToken);

        return row is null
            ? null
            : row.Dto with { NextStatuses = PartnerApplication.NextStatuses(row.Status).Select(s => s.ToString()).ToList() };
    }

    public Task<ContractDraftFileInfo?> GetContractFileAsync(int id, int version, CancellationToken cancellationToken)
        => db.ContractDrafts.AsNoTracking()
            .Where(d => d.PartnerApplicationId == id && d.Version == version)
            .Select(d => new ContractDraftFileInfo(d.ContractNumber, d.Version, d.FileKey, d.FileContentType, d.FileSha256))
            .FirstOrDefaultAsync(cancellationToken);
}
