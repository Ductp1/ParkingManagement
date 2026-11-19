using AdminService.Application.Features.PartnerOnboarding;
using Microsoft.EntityFrameworkCore;

namespace AdminService.Infrastructure.Persistence.Queries;

/// <summary>US-053: đọc giấy tờ pháp lý của hồ sơ đối tác (chỉ controller Admin dùng).</summary>
public sealed class PartnerDocumentQueries(AdminDbContext db) : IPartnerDocumentQueries
{
    public Task<bool> ApplicationExistsAsync(int applicationId, CancellationToken cancellationToken)
        => db.PartnerApplications.AsNoTracking().AnyAsync(a => a.Id == applicationId, cancellationToken);

    public async Task<IReadOnlyList<PartnerDocumentRecord>> ListAsync(int applicationId, CancellationToken cancellationToken)
        => await db.PartnerApplicationDocuments.AsNoTracking()
            .Where(d => d.PartnerApplicationId == applicationId)
            .OrderBy(d => d.DocumentType).ThenBy(d => d.Id)
            .Select(d => new PartnerDocumentRecord(d.Id, d.PartnerApplicationId, d.DocumentType, d.FileKey, d.ContentType,
                d.OriginalFileName, d.SizeBytes, d.CreatedAtUtc))
            .ToListAsync(cancellationToken);

    public Task<PartnerDocumentRecord?> FindAsync(int applicationId, int documentId, CancellationToken cancellationToken)
        => db.PartnerApplicationDocuments.AsNoTracking()
            .Where(d => d.Id == documentId && d.PartnerApplicationId == applicationId)
            .Select(d => new PartnerDocumentRecord(d.Id, d.PartnerApplicationId, d.DocumentType, d.FileKey, d.ContentType,
                d.OriginalFileName, d.SizeBytes, d.CreatedAtUtc))
            .FirstOrDefaultAsync(cancellationToken);
}
