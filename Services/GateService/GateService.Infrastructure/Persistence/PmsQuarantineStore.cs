using GateService.Application.Features.PmsIntegration;
using GateService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using ParkingManagement.SharedKernel.Enums;

namespace GateService.Infrastructure.Persistence;

/// <summary>Port IPmsQuarantineStore cài bằng GateDbContext – hàng chờ sự kiện PMS không khớp (UC-51 E1).</summary>
public sealed class PmsQuarantineStore(GateDbContext db) : IPmsQuarantineStore
{
    public async Task AddPendingAsync(PmsUnmatchedEvent item, CancellationToken cancellationToken)
    {
        // EventId trùng đang Pending → cập nhật lý do mới thay vì thêm dòng lặp (PMS retry cùng event).
        var existing = await db.PmsUnmatchedEvents
            .FirstOrDefaultAsync(e => e.EventId == item.EventId && e.Status == PmsQuarantineStatus.Pending, cancellationToken);
        if (existing is null)
        {
            await db.PmsUnmatchedEvents.AddAsync(item, cancellationToken);
        }
        else
        {
            existing.Reason = item.Reason;
            existing.PayloadJson = item.PayloadJson;
            existing.OccurredAtUtc = item.OccurredAtUtc;
        }
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<PmsUnmatchedEvent>> ListPendingAsync(int? parkingLotId, CancellationToken cancellationToken)
    {
        var query = db.PmsUnmatchedEvents.AsNoTracking().Where(e => e.Status == PmsQuarantineStatus.Pending);
        if (parkingLotId is { } lotId) query = query.Where(e => e.ParkingLotId == lotId);

        var rows = await query.OrderByDescending(e => e.OccurredAtUtc).Take(200).ToListAsync(cancellationToken);
        return rows;
    }

    public Task<PmsUnmatchedEvent?> FindByIdAsync(int id, CancellationToken cancellationToken)
        => db.PmsUnmatchedEvents.FirstOrDefaultAsync(e => e.Id == id, cancellationToken);

    public Task SaveAsync(CancellationToken cancellationToken) => db.SaveChangesAsync(cancellationToken);
}