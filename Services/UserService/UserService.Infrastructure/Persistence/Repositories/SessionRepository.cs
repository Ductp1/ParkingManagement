using Microsoft.EntityFrameworkCore;
using ParkingManagement.SharedKernel.Enums;
using UserService.Application.Features.Auth;
using UserService.Domain.Entities;

namespace UserService.Infrastructure.Persistence.Repositories;

public sealed class SessionRepository(UserDbContext db, TimeProvider clock) : ISessionRepository
{
    public Task<RefreshToken?> FindAsync(string hash, CancellationToken cancellationToken)
        => db.RefreshTokens.AsNoTracking()
            .Include(t => t.User).ThenInclude(u => u.Roles)
            .Include(t => t.User).ThenInclude(u => u.OwnerProfile)
            .Include(t => t.User).ThenInclude(u => u.StaffAssignments).ThenInclude(s => s.OwnerProfile)
            .FirstOrDefaultAsync(t => t.TokenHash == hash, cancellationToken);

    public async Task<bool> TryRotateAsync(string hash, RefreshToken replacement, DateTime now, CancellationToken cancellationToken)
    {
        var userId = await db.RefreshTokens.Where(t => t.TokenHash == hash)
            .Select(t => (int?)t.UserId).FirstOrDefaultAsync(cancellationToken);
        if (userId is null) return false;
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var user = await LockUserAsync(userId.Value, cancellationToken);
        now = clock.GetUtcNow().UtcDateTime;
        var token = await db.RefreshTokens.AsNoTracking().FirstOrDefaultAsync(t => t.TokenHash == hash, cancellationToken);
        if (token is null || user is null) return false;
        if (token.RevokedAtUtc is not null)
        {
            await RevokeChainAsync(hash, userId.Value, now, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return false;
        }
        if (token.ExpiresAtUtc <= now || user.IsDeleted || user.Status != UserStatus.Active
            || user.LockedUntilUtc > now || replacement.UserId != user.Id) return false;

        // Cập nhật có điều kiện là lớp bảo vệ thêm; không phụ thuộc ChangeTracker.
        var changed = await db.RefreshTokens.Where(t => t.Id == token.Id && t.RevokedAtUtc == null && t.ExpiresAtUtc > now)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAtUtc, (DateTime?)now)
                .SetProperty(t => t.ReplacedByTokenHash, replacement.TokenHash)
                .SetProperty(t => t.UpdatedAtUtc, (DateTime?)now), cancellationToken);
        if (changed != 1) return false;
        replacement.ExpiresAtUtc = token.ExpiresAtUtc;
        db.RefreshTokens.Add(replacement);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    public async Task RevokeAsync(string hash, DateTime now, CancellationToken cancellationToken)
    {
        var userId = await db.RefreshTokens.Where(t => t.TokenHash == hash)
            .Select(t => (int?)t.UserId).FirstOrDefaultAsync(cancellationToken);
        if (userId is null) return;
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await LockUserAsync(userId.Value, cancellationToken);
        await RevokeChainAsync(hash, userId.Value, now, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    // Cùng khóa cho refresh/logout: hai request không thể đồng thời thay đổi chuỗi token.
    // Khóa DB thay vì khóa trong bộ nhớ để vẫn đúng khi chạy nhiều instance UserService.
    private async Task<User?> LockUserAsync(int userId, CancellationToken cancellationToken)
        => (await db.Users.FromSqlInterpolated($"SELECT * FROM \"Users\" WHERE \"Id\" = {userId} FOR UPDATE")
            .IgnoreQueryFilters().AsNoTracking().ToListAsync(cancellationToken)).SingleOrDefault();

    private async Task RevokeChainAsync(string hash, int userId, DateTime now, CancellationToken cancellationToken)
    {
        var visited = new HashSet<string>();
        string? current = hash;
        while (current is not null && visited.Add(current))
        {
            var token = await db.RefreshTokens.AsNoTracking()
                .FirstOrDefaultAsync(t => t.TokenHash == current && t.UserId == userId, cancellationToken);
            if (token is null) break;
            await db.RefreshTokens.Where(t => t.Id == token.Id && t.RevokedAtUtc == null)
                .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAtUtc, (DateTime?)now)
                    .SetProperty(t => t.UpdatedAtUtc, (DateTime?)now), cancellationToken);
            current = token.ReplacedByTokenHash;
        }
    }
}
