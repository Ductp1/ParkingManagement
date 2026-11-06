using Microsoft.EntityFrameworkCore;
using UserService.Application.Features.Users;
using UserService.Domain.Entities;

namespace UserService.Infrastructure.Persistence.Repositories;

public sealed class OwnerProfileRepository(UserDbContext db) : IOwnerProfileRepository
{
    public Task<OwnerProfile?> FindByIdWithUserAsync(int ownerProfileId, CancellationToken cancellationToken)
        => db.OwnerProfiles
            .Include(o => o.User)
            .FirstOrDefaultAsync(o => o.Id == ownerProfileId, cancellationToken);

    public Task UpdateAsync(OwnerProfile ownerProfile, CancellationToken cancellationToken)
        => db.SaveChangesAsync(cancellationToken);
}
