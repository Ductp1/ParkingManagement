using Microsoft.EntityFrameworkCore;
using ParkingService.Application.Abstractions;
using ParkingService.Domain.Entities;

namespace ParkingService.Infrastructure.Persistence.Repositories;

/// <summary>
/// ADAPTER cài đặt ISlotRepository bằng EF Core + LINQ.
/// </summary>
public sealed class SlotRepository(ParkingDbContext db) : ISlotRepository
{
    public Task<Slot?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        => db.Slots.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Slot>> GetByFloorIdAsync(int floorId, CancellationToken cancellationToken = default)
        => await db.Slots.AsNoTracking()
            .Where(s => s.FloorId == floorId)
            .OrderBy(s => s.Code)
            .ToListAsync(cancellationToken);

    public Task<bool> ExistsCodeAsync(int floorId, string code, int? excludeId = null, CancellationToken cancellationToken = default)
        => db.Slots.AnyAsync(
            s => s.FloorId == floorId
                 && s.Code == code
                 && (!excludeId.HasValue || s.Id != excludeId.Value),
            cancellationToken);

    public Task<bool> IsGridCellOccupiedAsync(int floorId, int gridX, int gridY, int? excludeId = null, CancellationToken cancellationToken = default)
        => db.Slots.AnyAsync(
            s => s.FloorId == floorId
                 && s.GridX == gridX
                 && s.GridY == gridY
                 && (!excludeId.HasValue || s.Id != excludeId.Value),
            cancellationToken);

    public Task AddAsync(Slot slot, CancellationToken cancellationToken = default)
    {
        db.Slots.Add(slot);
        return Task.CompletedTask;
    }

    public Task AddRangeAsync(IEnumerable<Slot> slots, CancellationToken cancellationToken = default)
    {
        db.Slots.AddRange(slots);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(Slot slot, CancellationToken cancellationToken = default)
    {
        db.Slots.Update(slot);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Slot slot, CancellationToken cancellationToken = default)
    {
        db.Slots.Remove(slot);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Chỉ mục unique (FloorId, GridX, GridY) kiểm ngay trên từng dòng: xuất bản / khôi phục sơ đồ mà ô này dời vào chỗ ô kia
    /// đang giữ (kể cả ô đã tắt) sẽ bị PostgreSQL từ chối. Khi có đụng chỗ, ghi hai bước trong một transaction: đưa các ô
    /// sắp đổi ra ngoài lưới (-Id), rồi ghi vị trí thật. Ô đã tắt bị ô khác lấy chỗ thì ở lại ngoài lưới.
    /// </summary>
    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var changed = db.ChangeTracker.Entries<Slot>()
            .Where(e => e.State is EntityState.Added or EntityState.Modified)
            .Select(e => e.Entity)
            .ToList();
        // Vị trí mà các ô đang bật muốn nhận.
        var targets = changed.Where(s => s.IsActive)
            .GroupBy(s => (s.FloorId, s.GridX, s.GridY))
            .ToDictionary(g => g.Key, g => g.First().Id);
        var floorIds = targets.Keys.Select(k => k.FloorId).Distinct().ToList();
        var stored = await db.Slots.AsNoTracking()
            .Where(s => floorIds.Contains(s.FloorId))
            .Select(s => new { s.Id, s.FloorId, s.GridX, s.GridY })
            .ToListAsync(cancellationToken);
        var blockers = stored
            .Where(r => targets.TryGetValue((r.FloorId, r.GridX, r.GridY), out var taker) && taker != r.Id)
            .Select(r => r.Id)
            .ToList();
        if (blockers.Count == 0)
        {
            await db.SaveChangesAsync(cancellationToken);
            return;
        }

        // Ô đang bật giữ chỗ mà không nằm trong lần lưu này là trùng thật → để database từ chối.
        var changedIds = changed.Select(s => s.Id).ToList();
        var idle = await db.Slots
            .Where(s => blockers.Contains(s.Id) && !s.IsActive && !changedIds.Contains(s.Id))
            .ToListAsync(cancellationToken);
        var moving = changed.Concat(idle).Select(s => (Slot: s, s.GridX, s.GridY)).ToList();

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        for (var i = 0; i < moving.Count; i++)
        {
            var slot = moving[i].Slot;
            // Ngoài lưới và không trùng nhau: ô đã có dùng -Id, ô mới (chưa có Id) dùng hàng riêng.
            (slot.GridX, slot.GridY) = slot.Id > 0 ? (-slot.Id, -1) : (-(i + 1), -2);
        }
        await db.SaveChangesAsync(cancellationToken);

        foreach (var (slot, x, y) in moving)
        {
            if (slot.IsActive || !targets.ContainsKey((slot.FloorId, x, y))) (slot.GridX, slot.GridY) = (x, y);
        }
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
}
