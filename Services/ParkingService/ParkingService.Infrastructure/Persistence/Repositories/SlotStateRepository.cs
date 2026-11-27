using Microsoft.EntityFrameworkCore;
using ParkingManagement.SharedKernel.Exceptions;
using ParkingService.Application.Abstractions;
using ParkingService.Domain.Entities;

namespace ParkingService.Infrastructure.Persistence.Repositories;

/// <summary>ADAPTER cài ISlotStateRepository bằng EF Core – trạng thái vận hành slot + SlotStateLogs (US-039, US-049).</summary>
public sealed class SlotStateRepository(ParkingDbContext db) : ISlotStateRepository
{
    public async Task<SlotWithLot?> GetWithLotAsync(int slotId, CancellationToken cancellationToken = default)
    {
        var slot = await db.Slots
            .Include(s => s.Floor).ThenInclude(f => f.Zone)
            .FirstOrDefaultAsync(s => s.Id == slotId, cancellationToken);
        return slot is null ? null : new SlotWithLot(slot, slot.Floor.Zone.ParkingLotId);
    }

    public Task AddLogAsync(SlotStateLog log, CancellationToken cancellationToken = default)
    {
        db.SlotStateLogs.Add(log);
        return Task.CompletedTask;
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConflictException("Trạng thái ô đỗ vừa được cập nhật bởi nguồn khác, vui lòng tải lại sơ đồ và thử lại.");
        }
    }
}
