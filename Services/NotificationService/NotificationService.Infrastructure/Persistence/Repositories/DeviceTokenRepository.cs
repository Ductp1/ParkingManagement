using Microsoft.EntityFrameworkCore;
using NotificationService.Application.Features.DeviceTokens;
using NotificationService.Domain.Entities;

namespace NotificationService.Infrastructure.Persistence.Repositories;

/// <summary>
/// Implementation của IDeviceTokenRepository (EF Core + LINQ). Module: TV6.
/// </summary>
public sealed class DeviceTokenRepository(NotificationDbContext dbContext) : IDeviceTokenRepository
{
    public Task<DeviceToken?> GetByUserAndTokenAsync(int userId, string token, CancellationToken cancellationToken = default)
        => dbContext.DeviceTokens.AsNoTracking()
            .FirstOrDefaultAsync(t => t.UserId == userId && t.Token == token, cancellationToken);

    public async Task<IReadOnlyList<DeviceToken>> GetActiveByUserAsync(int userId, CancellationToken cancellationToken = default)
        => await dbContext.DeviceTokens.AsNoTracking()
            .Where(t => t.UserId == userId && !t.IsRevoked)
            .OrderByDescending(t => t.LastUsedAtUtc)
            .ToListAsync(cancellationToken);

    public async Task AddAsync(DeviceToken deviceToken, CancellationToken cancellationToken = default)
    {
        dbContext.DeviceTokens.Add(deviceToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(DeviceToken deviceToken, CancellationToken cancellationToken = default)
    {
        dbContext.DeviceTokens.Update(deviceToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<int> RevokeByUserAndTokenAsync(int userId, string token, CancellationToken cancellationToken = default)
    {
        var deviceToken = await dbContext.DeviceTokens
            .FirstOrDefaultAsync(t => t.UserId == userId && t.Token == token, cancellationToken);

        if (deviceToken is null)
            return 0;

        deviceToken.IsRevoked = true;
        await dbContext.SaveChangesAsync(cancellationToken);
        return 1;
    }
}
