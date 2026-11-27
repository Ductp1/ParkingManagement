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
            // Unique index chặn race giữa 2 request: 2 lượt Active cho cùng biển/bãi,
            // hoặc 2 lượt cho cùng booking (T-704). Báo conflict đúng nguyên nhân.
            throw new ConflictException(session.BookingCode is null
                ? $"Xe {session.PlateNumber} đang có lượt gửi chưa kết thúc tại bãi này."
                : $"Booking {session.BookingCode} đã được check-in trước đó, không thể tạo lượt gửi xe lần hai.");
        }
        return session;
    }

    public Task<ParkingSession?> FindActiveTrackedAsync(int parkingLotId, string normalizedPlate, CancellationToken cancellationToken)
        => db.ParkingSessions.FirstOrDefaultAsync(
            s => s.ParkingLotId == parkingLotId && s.PlateNumber == normalizedPlate && s.Status == ParkingSessionStatus.Active,
            cancellationToken);

    // T-704: 1 booking chỉ được check-in 1 lần (mọi trạng thái) – pre-check, race bị unique index chặn ở AddAsync.
    public Task<ParkingSession?> FindByBookingCodeAsync(string bookingCode, CancellationToken cancellationToken)
        => db.ParkingSessions.FirstOrDefaultAsync(s => s.BookingCode == bookingCode, cancellationToken);

    public Task SaveAsync(CancellationToken cancellationToken) => db.SaveChangesAsync(cancellationToken);
}
