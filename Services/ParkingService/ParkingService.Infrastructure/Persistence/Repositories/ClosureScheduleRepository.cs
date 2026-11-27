using Microsoft.EntityFrameworkCore;
using ParkingManagement.SharedKernel.Enums;
using ParkingService.Application.Abstractions;
using ParkingService.Domain.Entities;

namespace ParkingService.Infrastructure.Persistence.Repositories;

/// <summary>
/// ADAPTER cài đặt IClosureScheduleRepository bằng EF Core + LINQ trên DbContext của ParkingService.
/// </summary>
public sealed class ClosureScheduleRepository(ParkingDbContext db) : IClosureScheduleRepository
{
    public Task<ClosureSchedule?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        => db.ClosureSchedules.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

    public async Task<IReadOnlyList<ClosureSchedule>> GetByParkingLotIdAsync(
        int parkingLotId,
        ClosureTargetType? targetType = null,
        bool? activeOnly = null,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var query = db.ClosureSchedules.AsNoTracking()
            .Where(c => c.ParkingLotId == parkingLotId);

        if (targetType.HasValue)
        {
            query = query.Where(c => c.TargetType == targetType.Value);
        }

        if (activeOnly == true)
        {
            query = query.Where(c => c.CancelledAtUtc == null
                                     && c.StartsAtUtc <= now
                                     && (c.EndsAtUtc == null || c.EndsAtUtc.Value > now));
        }

        return await query
            .OrderByDescending(c => c.StartsAtUtc)
            .ToListAsync(cancellationToken);
    }

    public Task<bool> HasOverlappingClosureAsync(
        int parkingLotId,
        ClosureTargetType targetType,
        int targetId,
        DateTime startsAtUtc,
        DateTime? endsAtUtc,
        int? excludeId = null,
        CancellationToken cancellationToken = default)
    {
        var maxTime = DateTime.MaxValue;
        var effectiveEnd = endsAtUtc ?? maxTime;

        return db.ClosureSchedules.AnyAsync(
            c => c.ParkingLotId == parkingLotId
                 && c.TargetType == targetType
                 && c.TargetId == targetId
                 && c.CancelledAtUtc == null
                 && (!excludeId.HasValue || c.Id != excludeId.Value)
                 && startsAtUtc < (c.EndsAtUtc ?? maxTime)
                 && effectiveEnd > c.StartsAtUtc,
            cancellationToken);
    }

    public Task AddAsync(ClosureSchedule schedule, CancellationToken cancellationToken = default)
    {
        db.ClosureSchedules.Add(schedule);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(ClosureSchedule schedule, CancellationToken cancellationToken = default)
    {
        db.ClosureSchedules.Update(schedule);
        return Task.CompletedTask;
    }

    public Task AddStateLogsAsync(IEnumerable<SlotStateLog> logs, CancellationToken cancellationToken = default)
    {
        db.SlotStateLogs.AddRange(logs);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => db.SaveChangesAsync(cancellationToken);
}
