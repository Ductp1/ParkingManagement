using GateService.Application.Features.GateConsole;
using GateService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using ParkingManagement.SharedKernel.Enums;
using ParkingManagement.SharedKernel.Exceptions;

namespace GateService.Infrastructure.Persistence;

/// <summary>Cài đặt port IParkingSessionWriter bằng GateDbContext (Application không đụng EF Core).</summary>
public sealed class ParkingSessionWriter(GateDbContext db) : IParkingSessionWriter
{
    public async Task<ParkingSession> AddAsync(ParkingSession session, CancellationToken cancellationToken)
    {
        db.ParkingSessions.Add(session);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Unique index chặn 2 lượt Active cho cùng 1 biển số/bãi (chống race giữa 2 request).
            throw new ConflictException($"Xe {session.PlateNumber} đang có lượt gửi chưa kết thúc tại bãi này.");
        }
        return session;
    }

    public Task<ParkingSession?> FindActiveTrackedAsync(int parkingLotId, string normalizedPlate, CancellationToken cancellationToken)
        => db.ParkingSessions.FirstOrDefaultAsync(
            s => s.ParkingLotId == parkingLotId && s.PlateNumber == normalizedPlate && s.Status == ParkingSessionStatus.Active,
            cancellationToken);

    public Task SaveAsync(CancellationToken cancellationToken) => db.SaveChangesAsync(cancellationToken);
}
