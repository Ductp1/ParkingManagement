using GateService.Application.Features.GateDevices;
using GateService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using ParkingManagement.SharedKernel.Exceptions;

namespace GateService.Infrastructure.Persistence.Repositories;

/// <summary>Cài đặt port IGateDeviceRepository bằng GateDbContext (Application không đụng EF Core).</summary>
public sealed class GateDeviceRepository(GateDbContext db) : IGateDeviceRepository
{
    public Task<GateDevice?> FindTrackedByIdAsync(int id, CancellationToken cancellationToken)
        => db.GateDevices.FirstOrDefaultAsync(d => d.Id == id, cancellationToken);

    public Task<bool> CodeExistsAsync(string code, int? excludeId = null, CancellationToken cancellationToken = default)
        => db.GateDevices.AnyAsync(d => d.Code == code && (excludeId == null || d.Id != excludeId), cancellationToken);

    public async Task AddAsync(GateDevice device, CancellationToken cancellationToken)
    {
        db.GateDevices.Add(device);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Unique index IX_GateDevices_Code chặn trùng mã thiết bị (chống race giữa 2 request).
            throw new ConflictException($"Mã thiết bị '{device.Code}' đã tồn tại.");
        }
    }

    public Task SaveAsync(CancellationToken cancellationToken) => db.SaveChangesAsync(cancellationToken);

    public async Task RemoveAsync(GateDevice device, CancellationToken cancellationToken)
    {
        db.GateDevices.Remove(device);
        await db.SaveChangesAsync(cancellationToken);
    }
}
