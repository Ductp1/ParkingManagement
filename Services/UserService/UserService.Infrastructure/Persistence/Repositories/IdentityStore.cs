using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using ParkingManagement.SharedKernel.Contracts;
using ParkingManagement.SharedKernel.Domain;
using ParkingManagement.SharedKernel.Enums;
using UserService.Application.Features.Identity;
using UserService.Domain.Entities;

namespace UserService.Infrastructure.Persistence.Repositories;

public sealed class IdentityStore(UserDbContext db) : IIdentityStore
{
    public async Task<IIdentityTransaction> BeginAsync(CancellationToken ct) => new Transaction(await db.Database.BeginTransactionAsync(ct));
    public Task<User?> FindContactAsync(string contact, CancellationToken ct)
        => db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Email == contact || u.PhoneNumber == contact, ct);
    public async Task<User?> FindUserAsync(int id, bool forUpdate, CancellationToken ct)
    {
        if (forUpdate)
        {
            // Không tracking khi lấy khóa: tránh trả một entity cũ từ ChangeTracker.
            await db.Users.FromSqlInterpolated($"SELECT * FROM \"Users\" WHERE \"Id\" = {id} FOR UPDATE")
                .IgnoreQueryFilters().AsNoTracking().ToListAsync(ct);
            var tracked = db.ChangeTracker.Entries<User>().FirstOrDefault(e => e.Entity.Id == id);
            if (tracked is not null) await tracked.ReloadAsync(ct);
        }
        return await db.Users.Include(u => u.Roles).Include(u => u.OwnerProfile).FirstOrDefaultAsync(u => u.Id == id, ct);
    }
    public Task<OtpCode?> LatestOtpAsync(int userId, OtpPurpose purpose, CancellationToken ct)
        => db.OtpCodes.Where(o => o.UserId == userId && o.Purpose == purpose).OrderByDescending(o => o.Id).FirstOrDefaultAsync(ct);
    public async Task<IReadOnlyList<OtpCode>> PendingOtpsAsync(int userId, OtpPurpose purpose, CancellationToken ct)
        => await db.OtpCodes.Where(o => o.UserId == userId && o.Purpose == purpose && o.ConsumedAtUtc == null).ToListAsync(ct);
    public Task<int> OtpFailuresAsync(int userId, DateTime since, CancellationToken ct)
        => db.SecurityEvents.CountAsync(e => e.UserId == userId && e.EventType == SecurityEventType.OtpFailed && e.CreatedAtUtc >= since, ct);
    public async Task RevokeSessionsAsync(int userId, DateTime now, CancellationToken ct)
        => await db.RefreshTokens.Where(t => t.UserId == userId && t.RevokedAtUtc == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAtUtc, (DateTime?)now).SetProperty(t => t.UpdatedAtUtc, (DateTime?)now), ct);
    public void Add(object entity) => db.Add(entity);
    public void Event(IntegrationEvent ev) => db.OutboxMessages.Add(new OutboxMessage
        { Id = ev.EventId, EventType = ev.GetType().Name, OccurredAtUtc = ev.OccurredAtUtc, PayloadJson = JsonSerializer.Serialize(ev, ev.GetType()) });
    public async Task SaveAsync(CancellationToken ct) => await db.SaveChangesAsync(ct);
    private sealed class Transaction(IDbContextTransaction transaction) : IIdentityTransaction
    {
        public Task CommitAsync(CancellationToken ct) => transaction.CommitAsync(ct);
        public ValueTask DisposeAsync() => transaction.DisposeAsync();
    }
}
