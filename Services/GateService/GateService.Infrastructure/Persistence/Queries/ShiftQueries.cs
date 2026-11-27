using GateService.Application.Features.GateConsole;
using Microsoft.EntityFrameworkCore;
using ParkingManagement.SharedKernel.Enums;

namespace GateService.Infrastructure.Persistence.Queries;

/// <summary>Đọc ca trực (UC-20): ca đang mở của nhân viên xác định khung giờ "trong ca" và bãi được gán.</summary>
public sealed class ShiftQueries(GateDbContext db) : IShiftQueries
{
    public async Task<ShiftDto?> FindOpenByStaffAsync(int staffUserId, CancellationToken cancellationToken)
        => await db.Shifts.AsNoTracking()
            .Where(s => s.StaffUserId == staffUserId && s.Status == ShiftStatus.Open)
            .OrderByDescending(s => s.StartedAtUtc)
            .Select(s => new ShiftDto(s.Id, s.ParkingLotId, s.StaffUserId, s.StartedAtUtc, s.EndedAtUtc, s.Status.ToString()))
            .FirstOrDefaultAsync(cancellationToken);
}
