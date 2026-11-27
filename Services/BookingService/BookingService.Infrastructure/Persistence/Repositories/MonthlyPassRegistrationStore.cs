using BookingService.Application.Features.MonthlyPasses;
using BookingService.Domain.Entities;
using BookingService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using ParkingManagement.SharedKernel.Enums;

namespace BookingService.Infrastructure.Persistence.Repositories;

/// <summary>
/// Store đăng ký thuê bao US-066: CountActiveAsync đếm thuê bao đang hiệu lực
/// (Status = Active và ValidTo >= hôm nay); TryAddActiveWithinQuotaAsync chèn trong
/// transaction có pg_advisory_xact_lock(lotId) → đăng ký đồng thời được tuần tự hóa theo bãi.
/// </summary>
public sealed class MonthlyPassRegistrationStore(BookingDbContext db, TimeProvider timeProvider)
    : IMonthlyPassRegistrationStore
{
    public Task<int> CountActiveAsync(int parkingLotId, CancellationToken cancellationToken = default)
    {
        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        return db.MonthlyPasses.AsNoTracking()
            .CountAsync(p => p.ParkingLotId == parkingLotId
                             && p.Status == MonthlyPassStatus.Active
                             && p.ValidTo >= today, cancellationToken);
    }

    public async Task<bool> TryAddActiveWithinQuotaAsync(
        MonthlyPass pass, int subscriberQuota, CancellationToken cancellationToken = default)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        // Khuây khóa đồng thời theo bãi: mọi đăng ký cùng lot chạy tuần tự tại đây.
        await db.Database.ExecuteSqlRawAsync(
            "SELECT pg_advisory_xact_lock({0})", new object[] { (long)pass.ParkingLotId }, cancellationToken);

        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        var activeCount = await db.MonthlyPasses
            .CountAsync(p => p.ParkingLotId == pass.ParkingLotId
                             && p.Status == MonthlyPassStatus.Active
                             && p.ValidTo >= today, cancellationToken);
        if (activeCount >= subscriberQuota)
        {
            await transaction.RollbackAsync(cancellationToken);
            return false;
        }

        db.MonthlyPasses.Add(pass);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }
}