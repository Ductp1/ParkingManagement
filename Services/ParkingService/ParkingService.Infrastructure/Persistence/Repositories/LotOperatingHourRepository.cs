using Microsoft.EntityFrameworkCore;
using ParkingService.Application.Abstractions;
using ParkingService.Domain.Entities;

namespace ParkingService.Infrastructure.Persistence.Repositories;

/// <summary>
/// ADAPTER cài đặt ILotOperatingHourRepository bằng EF Core + LINQ (US-062).
/// </summary>
public sealed class LotOperatingHourRepository(ParkingDbContext db) : ILotOperatingHourRepository
{
    public async Task<IReadOnlyList<LotOperatingHour>> GetByParkingLotIdAsync(int parkingLotId, bool trackChanges = false, CancellationToken cancellationToken = default)
    {
        var query = trackChanges ? db.LotOperatingHours.AsTracking() : db.LotOperatingHours.AsNoTracking();
        return await query
            .Where(x => x.ParkingLotId == parkingLotId)
            .OrderBy(x => x.DayOfWeek)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<LotOperatingHourOverride>> GetOverridesByWeekAsync(int parkingLotId, int year, int weekOfYear, bool trackChanges = false, CancellationToken cancellationToken = default)
    {
        var query = trackChanges ? db.LotOperatingHourOverrides.AsTracking() : db.LotOperatingHourOverrides.AsNoTracking();
        return await query
            .Where(x => x.ParkingLotId == parkingLotId && x.Year == year && x.WeekOfYear == weekOfYear)
            .OrderBy(x => x.DayOfWeek)
            .ToListAsync(cancellationToken);
    }

    public async Task ReplaceScheduleAsync(int parkingLotId, IEnumerable<LotOperatingHour> newSchedule, CancellationToken cancellationToken = default)
    {
        var newScheduleList = newSchedule.ToList();
        var existing = await db.LotOperatingHours
            .Where(x => x.ParkingLotId == parkingLotId)
            .ToListAsync(cancellationToken);

        var incomingDays = newScheduleList.Select(s => s.DayOfWeek).ToHashSet();

        // 1. Xóa các ngày cấu hình cũ không còn trong danh sách mới
        var toRemove = existing.Where(e => !incomingDays.Contains(e.DayOfWeek)).ToList();
        if (toRemove.Count > 0)
        {
            db.LotOperatingHours.RemoveRange(toRemove);
        }

        // 2. Cập nhật ngày đã có hoặc thêm mới ngày chưa có
        foreach (var item in newScheduleList)
        {
            var matched = existing.FirstOrDefault(e => e.DayOfWeek == item.DayOfWeek);
            if (matched != null)
            {
                matched.OpenTime = item.OpenTime;
                matched.CloseTime = item.CloseTime;
                matched.IsClosed = item.IsClosed;
            }
            else
            {
                item.ParkingLotId = parkingLotId;
                db.LotOperatingHours.Add(item);
            }
        }
    }

    public async Task ReplaceWeekOverridesAsync(int parkingLotId, int year, int weekOfYear, IEnumerable<LotOperatingHourOverride> overrides, CancellationToken cancellationToken = default)
    {
        var incomingList = overrides.ToList();
        var existing = await db.LotOperatingHourOverrides
            .Where(x => x.ParkingLotId == parkingLotId && x.Year == year && x.WeekOfYear == weekOfYear)
            .ToListAsync(cancellationToken);

        var incomingDays = incomingList.Select(s => s.DayOfWeek).ToHashSet();

        // 1. Xóa các ngày override cũ không còn trong danh sách mới
        var toRemove = existing.Where(e => !incomingDays.Contains(e.DayOfWeek)).ToList();
        if (toRemove.Count > 0)
        {
            db.LotOperatingHourOverrides.RemoveRange(toRemove);
        }

        // 2. Cập nhật ngày đã có hoặc thêm mới
        foreach (var item in incomingList)
        {
            var matched = existing.FirstOrDefault(e => e.DayOfWeek == item.DayOfWeek);
            if (matched != null)
            {
                matched.OpenTime = item.OpenTime;
                matched.CloseTime = item.CloseTime;
                matched.IsClosed = item.IsClosed;
                matched.Reason = item.Reason;
            }
            else
            {
                item.ParkingLotId = parkingLotId;
                item.Year = year;
                item.WeekOfYear = weekOfYear;
                db.LotOperatingHourOverrides.Add(item);
            }
        }
    }

    public async Task DeleteWeekOverridesAsync(int parkingLotId, int year, int weekOfYear, CancellationToken cancellationToken = default)
    {
        var existing = await db.LotOperatingHourOverrides
            .Where(x => x.ParkingLotId == parkingLotId && x.Year == year && x.WeekOfYear == weekOfYear)
            .ToListAsync(cancellationToken);

        if (existing.Count > 0)
        {
            db.LotOperatingHourOverrides.RemoveRange(existing);
        }
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => db.SaveChangesAsync(cancellationToken);
}
