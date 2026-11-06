using Microsoft.EntityFrameworkCore;
using UserService.Application.Features.Auth;
using UserService.Domain.Entities;
using UserService.Infrastructure.Persistence;

namespace UserService.Infrastructure.Persistence.Repositories;

public class AuthRepository(UserDbContext dbContext, TimeProvider clock) : IAuthRepository, IAsyncDisposable
{
    private Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction? transaction;
    public async Task<User?> GetUserByEmailOrPhoneAsync(string emailOrPhone, CancellationToken cancellationToken)
    {
        var id = await dbContext.Users.AsNoTracking().Where(u => u.Email == emailOrPhone || u.PhoneNumber == emailOrPhone)
            .Select(u => (int?)u.Id).FirstOrDefaultAsync(cancellationToken);
        if (id is null) return null;
        transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        await dbContext.Users.FromSqlInterpolated($"SELECT * FROM \"Users\" WHERE \"Id\" = {id.Value} FOR UPDATE")
            .IgnoreQueryFilters().AsNoTracking().ToListAsync(cancellationToken);
        return await dbContext.Users
            .Include(u => u.Roles)
            .Include(u => u.OwnerProfile)
            .Include(u => u.StaffAssignments).ThenInclude(s => s.OwnerProfile)
            .FirstOrDefaultAsync(u => u.Email == emailOrPhone || u.PhoneNumber == emailOrPhone, cancellationToken);
    }

    public async Task UpdateUserAsync(User user, CancellationToken cancellationToken)
    {
        // EntityFramework Core đã theo dõi (track) sự thay đổi của entity `user` lấy từ GetUserByEmailOrPhoneAsync.
        // Chỉ cần gọi SaveChangesAsync là đủ.
        // Nhật ký và sự kiện khóa được lưu cùng thay đổi của tài khoản.
        var locked = user.LockedUntilUtc > clock.GetUtcNow().UtcDateTime;
        dbContext.SecurityEvents.Add(new UserService.Domain.Entities.SecurityEvent
        {
            UserId = user.Id,
            EventType = locked ? ParkingManagement.SharedKernel.Enums.SecurityEventType.AccountLocked
                : user.FailedLoginCount > 0 ? ParkingManagement.SharedKernel.Enums.SecurityEventType.LoginFailed
                : ParkingManagement.SharedKernel.Enums.SecurityEventType.LoginSucceeded
        });
        if (locked)
        {
            var ev = new ParkingManagement.SharedKernel.Contracts.UserLocked(user.Id, user.LockReason ?? "Account locked");
            dbContext.OutboxMessages.Add(new ParkingManagement.SharedKernel.Domain.OutboxMessage
            { Id = ev.EventId, EventType = nameof(ParkingManagement.SharedKernel.Contracts.UserLocked),
                PayloadJson = System.Text.Json.JsonSerializer.Serialize(ev) });
        }
        await dbContext.SaveChangesAsync(cancellationToken);
        if (transaction is not null) await transaction.CommitAsync(cancellationToken);
    }
    public async ValueTask DisposeAsync() { if (transaction is not null) await transaction.DisposeAsync(); }
}
