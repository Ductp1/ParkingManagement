using Microsoft.EntityFrameworkCore;
using ParkingManagement.SharedKernel.Enums;
using UserService.Application.Features.Identity;
using UserService.Domain.Entities;

namespace UserService.Infrastructure.Persistence.Repositories;

/// <summary>US-116 U2–U5: tra hồ sơ chủ bãi theo MST / Id và link kích hoạt (OtpCodes, Purpose = OwnerActivation).</summary>
public sealed class OwnerAccountStore(UserDbContext db) : IOwnerAccountStore
{
    public Task<OwnerProfile?> FindByTaxCodeAsync(string taxCode, CancellationToken ct)
        => db.OwnerProfiles.AsNoTracking().Include(p => p.User).FirstOrDefaultAsync(p => p.TaxCode == taxCode, ct);

    public Task<OwnerProfile?> FindProfileAsync(int ownerProfileId, CancellationToken ct)
        => db.OwnerProfiles.Include(p => p.User).FirstOrDefaultAsync(p => p.Id == ownerProfileId, ct);

    public Task<OtpCode?> FindActivationAsync(string tokenHash, CancellationToken ct)
        => db.OtpCodes.Include(o => o.User!).ThenInclude(u => u.OwnerProfile)
            .FirstOrDefaultAsync(o => o.CodeHash == tokenHash && o.Purpose == OtpPurpose.OwnerActivation, ct);

    public async Task<IReadOnlyList<OtpCode>> OpenActivationsAsync(int userId, CancellationToken ct)
        => await db.OtpCodes.Where(o => o.UserId == userId && o.Purpose == OtpPurpose.OwnerActivation && o.ConsumedAtUtc == null)
            .ToListAsync(ct);
}
