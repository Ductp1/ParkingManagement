using GateService.Application.Features.GateDevices;
using Microsoft.EntityFrameworkCore;
using ParkingManagement.SharedKernel.Enums;

namespace GateService.Infrastructure.Persistence.Queries;

/// <summary>Đọc dữ liệu GateDevices (bất đồng bộ, AsNoTracking) cho các use case đọc.</summary>
public sealed class GateDeviceQueries(GateDbContext db) : IGateDeviceQueries
{
    public async Task<IReadOnlyList<GateDeviceDto>> ListAsync(int? parkingLotId, GateDeviceStatus? status, CancellationToken cancellationToken)
    {
        var query = db.GateDevices.AsNoTracking();
        if (parkingLotId.HasValue) query = query.Where(d => d.ParkingLotId == parkingLotId.Value);
        if (status.HasValue) query = query.Where(d => d.Status == status.Value);

        var rows = await query.OrderBy(d => d.ParkingLotId).ThenBy(d => d.Code).ToListAsync(cancellationToken);
        return rows.Select(GateDeviceMapper.ToDto).ToList();
    }

    public async Task<GateDeviceDto?> FindByIdAsync(int id, CancellationToken cancellationToken)
    {
        var device = await db.GateDevices.AsNoTracking().FirstOrDefaultAsync(d => d.Id == id, cancellationToken);
        return device is null ? null : GateDeviceMapper.ToDto(device);
    }
}
