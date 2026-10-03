using GateService.Application.Features;
using GateService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using ParkingManagement.SharedKernel.Enums;
using ParkingManagement.SharedKernel.Rules;

namespace GateService.Infrastructure.Persistence.Queries;

public sealed class ParkingSessionQueries(GateDbContext db) : IParkingSessionQueries
{
    public async Task<IReadOnlyList<ParkingSessionDto>> ListByLotAsync(int parkingLotId, ParkingSessionStatus? status, DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        var query = db.ParkingSessions.AsNoTracking().Where(s => s.ParkingLotId == parkingLotId);
        query = query.Where(s => s.Status == (status ?? ParkingSessionStatus.Active));

        var rows = await query.OrderByDescending(s => s.EntryAtUtc).ToListAsync(cancellationToken);
        return rows.Select(s => ToDto(s, nowUtc)).ToList();
    }

    public async Task<ParkingSessionDto?> FindActiveByPlateAsync(int parkingLotId, string normalizedPlate, DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        var s = await db.ParkingSessions.AsNoTracking()
            .FirstOrDefaultAsync(x => x.ParkingLotId == parkingLotId && x.PlateNumber == normalizedPlate
                                   && x.Status == ParkingSessionStatus.Active, cancellationToken);
        return s is null ? null : ToDto(s, nowUtc);
    }

    // Format biển số và tính số phút đỗ chạy trong bộ nhớ (LINQ to Objects) vì SQL không dịch được PlateNormalizer.
    private static ParkingSessionDto ToDto(ParkingSession s, DateTime nowUtc) => new(
        s.Id, s.Code, s.ParkingLotId, s.PlateNumber, PlateNormalizer.Format(s.PlateNumber), s.IsWalkIn,
        s.BookingCode, s.SlotCode, s.Status.ToString(), s.EntryAtUtc, s.ExitAtUtc,
        (int)Math.Ceiling(((s.ExitAtUtc ?? nowUtc) - s.EntryAtUtc).TotalMinutes),
        s.CheckInMethod.ToString(), s.EntryOcrConfidence, s.Fee);
}
