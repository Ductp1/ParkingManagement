using Microsoft.EntityFrameworkCore;
using ParkingManagement.SharedKernel.Enums;
using UserService.Application.Features.Identity;

namespace UserService.Infrastructure.Persistence.Repositories;

/// <summary>Truy vấn phụ cho đình chỉ / xóa user (US-096) và hủy hồ sơ đối tác (US-116). Global filter đã loại user IsDeleted.</summary>
public sealed class UserLifecycleStore(UserDbContext db) : IUserLifecycleStore
{
    public Task<int> CountOtherActiveAdminsAsync(int excludeUserId, DateTime nowUtc, CancellationToken ct)
        => db.Users.CountAsync(u => u.Id != excludeUserId && u.Status == UserStatus.Active
            && !(u.LockedUntilUtc > nowUtc) && u.Roles.Any(r => r.Role == UserRoleType.Admin), ct);

    public Task<bool> HasEverSignedInAsync(int userId, CancellationToken ct)
        => db.RefreshTokens.AnyAsync(t => t.UserId == userId, ct);
}
