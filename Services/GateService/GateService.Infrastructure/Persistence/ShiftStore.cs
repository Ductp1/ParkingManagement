using GateService.Application.Features.GateConsole;
using GateService.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace GateService.Infrastructure.Persistence;

/// <summary>Port IShiftStore cài bằng GateDbContext – ca trực đang mở của Staff (1 Staff chỉ có 1 ca Open).</summary>
public sealed class ShiftStore(GateDbContext db) : IShiftStore
{
    public Task<Shift?> FindOpenByStaffAsync(int staffUserId, CancellationToken cancellationToken)
        => db.Shifts.FirstOrDefaultAsync(s => s.StaffUserId == staffUserId && s.Status == ShiftStatus.Open, cancellationToken);

    public Task SaveAsync(CancellationToken cancellationToken) => db.SaveChangesAsync(cancellationToken);
}