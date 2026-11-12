using GateService.Application.Features.AdminDashboard;
using Microsoft.EntityFrameworkCore;
using ParkingManagement.SharedKernel.Enums;

namespace GateService.Infrastructure.Persistence.Queries;

/// <summary>US-081: số liệu Admin dashboard – AsNoTracking + GroupBy/Count ở database, không đọc biển số / người dùng.</summary>
public sealed class AdminGateDashboardQueries(GateDbContext db) : IAdminGateDashboardQueries
{
    public async Task<IReadOnlyList<ActiveSessionGroupRow>> CountActiveSessionsAsync(CancellationToken cancellationToken)
        => await ActiveSessionGroups(db.ParkingSessions.AsNoTracking()).ToListAsync(cancellationToken);

    public Task<int> CountAbnormalSessionsAsync(CancellationToken cancellationToken)
        => db.ParkingSessions.AsNoTracking().CountAsync(s => s.Status == ParkingSessionStatus.Abnormal, cancellationToken);

    public async Task<IReadOnlyList<LotCapacityRow>> ListCapacitiesAsync(CancellationToken cancellationToken)
        => await db.LotCapacityConfigs.AsNoTracking()
            .Select(c => new LotCapacityRow(c.ParkingLotId, c.Capacity))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<GateDeviceGroupRow>> CountActiveDevicesAsync(CancellationToken cancellationToken)
        => await DeviceGroups(db.GateDevices.AsNoTracking()).ToListAsync(cancellationToken);

    public static IQueryable<ActiveSessionGroupRow> ActiveSessionGroups(IQueryable<Domain.Entities.ParkingSession> sessions)
        => sessions.Where(s => s.Status == ParkingSessionStatus.Active)
            .GroupBy(s => new { s.ParkingLotId, s.IsWalkIn })
            .Select(g => new ActiveSessionGroupRow(g.Key.ParkingLotId, g.Key.IsWalkIn, g.Count()));

    public static IQueryable<GateDeviceGroupRow> DeviceGroups(IQueryable<Domain.Entities.GateDevice> devices)
        => devices.Where(d => d.IsActive)
            .GroupBy(d => new { d.DeviceType, d.Status })
            .Select(g => new GateDeviceGroupRow(g.Key.DeviceType, g.Key.Status, g.Count()));
}
