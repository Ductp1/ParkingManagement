using AdminService.Application.Features.ContractTemplates;
using Microsoft.EntityFrameworkCore;

namespace AdminService.Infrastructure.Persistence.Queries;

/// <summary>US-116: đọc mẫu hợp đồng bãi (AsNoTracking). UsedByVersions đếm phiên bản hợp đồng bãi sinh từ mẫu.</summary>
public sealed class ContractTemplateQueries(AdminDbContext db) : IContractTemplateQueries
{
    public async Task<IReadOnlyList<ContractTemplateSummaryDto>> ListAsync(CancellationToken cancellationToken)
    {
        var rows = await db.ContractTemplates.AsNoTracking()
            .OrderByDescending(t => t.Version)
            .Select(t => new
            {
                t.Id, t.Version, t.Name, t.Kind, t.Status, t.ChangeNote, t.CreatedAtUtc, t.UpdatedAtUtc, t.ActivatedAtUtc, t.CreatedByAdminId,
                UsedByVersions = db.LotContractVersions.Count(v => v.TemplateVersion == t.Version),
            })
            .ToListAsync(cancellationToken);
        return rows.Select(t => new ContractTemplateSummaryDto(t.Id, t.Version, t.Name, t.Kind.ToString(), t.Status.ToString(), t.ChangeNote,
            t.CreatedAtUtc, t.UpdatedAtUtc, t.ActivatedAtUtc, t.CreatedByAdminId, t.UsedByVersions)).ToList();
    }

    public async Task<ContractTemplateDetailDto?> GetDetailAsync(int id, CancellationToken cancellationToken)
    {
        var t = await db.ContractTemplates.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (t is null) return null;
        var used = await db.LotContractVersions.CountAsync(v => v.TemplateVersion == t.Version, cancellationToken);
        var sourceName = t.SourceTemplateId is { } sourceId
            ? await db.ContractTemplates.Where(x => x.Id == sourceId).Select(x => x.Name).FirstOrDefaultAsync(cancellationToken)
            : null;
        var content = TemplateContentJson.Deserialize(t.ContentJson);
        return new ContractTemplateDetailDto(t.Id, t.Version, t.Name, t.Kind.ToString(), t.Status.ToString(), t.ChangeNote,
            content is null ? null : ContractTemplateMapping.ToDto(content), t.Body, ContractTemplateMapping.ToDto(t.Platform),
            t.SourceFileName, t.ImportNotes, t.SourceTemplateId, sourceName, t.CreatedAtUtc, t.UpdatedAtUtc, t.ActivatedAtUtc,
            t.CreatedByAdminId, used, Convert.ToBase64String(t.RowVersion));
    }
}
