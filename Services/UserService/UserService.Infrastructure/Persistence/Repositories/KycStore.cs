using Microsoft.EntityFrameworkCore;
using ParkingManagement.SharedKernel.Enums;
using UserService.Application.Features.Kyc;
using UserService.Domain.Entities;

namespace UserService.Infrastructure.Persistence.Repositories;

public sealed class KycStore(UserDbContext db) : IKycStore
{
    private static readonly SecurityEventType[] KycEvents =
        [SecurityEventType.KycSubmitted, SecurityEventType.KycApproved, SecurityEventType.KycRejected];

    /// <summary>Hồ sơ chờ duyệt, gửi sớm nhất lên trước.</summary>
    public async Task<IReadOnlyList<KycQueueRow>> ListPendingAsync(int page, int pageSize, CancellationToken ct)
        => await db.Users.AsNoTracking().Where(u => u.KycStatus == KycStatus.Pending)
            .Select(u => new
            {
                u.Id, u.FullName, u.KycDocumentType, u.KycNumberEncrypted,
                SubmittedAtUtc = db.SecurityEvents.Where(e => e.UserId == u.Id && e.EventType == SecurityEventType.KycSubmitted)
                    .Max(e => (DateTime?)e.CreatedAtUtc)
            })
            .OrderBy(x => x.SubmittedAtUtc).ThenBy(x => x.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new KycQueueRow(x.Id, x.FullName, x.KycDocumentType, x.KycNumberEncrypted, x.SubmittedAtUtc))
            .ToListAsync(ct);

    public Task<SecurityEvent?> LatestKycEventAsync(int userId, CancellationToken ct)
        => db.SecurityEvents.AsNoTracking().Where(e => e.UserId == userId && KycEvents.Contains(e.EventType))
            .OrderByDescending(e => e.CreatedAtUtc).ThenByDescending(e => e.Id).FirstOrDefaultAsync(ct);
}
