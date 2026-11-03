using GateService.Application.Features.AdminDashboard;
using GateService.Infrastructure.Persistence;
using GateService.Infrastructure.Persistence.Queries;
using Microsoft.EntityFrameworkCore;
using ParkingManagement.SharedKernel.Enums;

namespace GateService.Test;

/// <summary>
/// US-081: số liệu Admin dashboard của GateService – ghép phiên đang hoạt động với sức chứa theo bãi, ma trận thiết bị,
/// "0 thật" và hình dạng SQL (đếm / gộp ở database, không đọc biển số).
/// </summary>
public class AdminGateDashboardTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 3, 3, 0, 0, TimeSpan.Zero);

    private sealed class FakeQueries : IAdminGateDashboardQueries
    {
        public List<ActiveSessionGroupRow> Sessions { get; } = [];
        public int Abnormal { get; set; }
        public List<LotCapacityRow> Capacities { get; } = [];
        public List<GateDeviceGroupRow> Devices { get; } = [];

        public Task<IReadOnlyList<ActiveSessionGroupRow>> CountActiveSessionsAsync(CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<ActiveSessionGroupRow>>(Sessions);
        public Task<int> CountAbnormalSessionsAsync(CancellationToken cancellationToken) => Task.FromResult(Abnormal);
        public Task<IReadOnlyList<LotCapacityRow>> ListCapacitiesAsync(CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<LotCapacityRow>>(Capacities);
        public Task<IReadOnlyList<GateDeviceGroupRow>> CountActiveDevicesAsync(CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<GateDeviceGroupRow>>(Devices);
    }

    [Fact]
    public async Task Occupancy_MergesSessionsWithCapacity_PerLot()
    {
        var queries = new FakeQueries { Abnormal = 2 };
        queries.Sessions.AddRange([new(1, true, 3), new(1, false, 5), new(2, false, 4)]);
        queries.Capacities.AddRange([new(1, 100), new(3, 50)]);

        var result = await new GetAdminGateOccupancyUseCase(queries, new FixedTimeProvider(Now)).ExecuteAsync();

        Assert.Equal(Now.UtcDateTime, result.AsOfUtc);
        Assert.Equal(12, result.Total);
        Assert.Equal(3, result.WalkIn);
        Assert.Equal(9, result.Booked);
        Assert.Equal(2, result.Abnormal);
        Assert.Equal([1, 2, 3], result.Lots.Select(l => l.ParkingLotId));
        Assert.Equal(new AdminLotOccupancyRowDto(1, 8, 3, 5, 100), result.Lots[0]);
        Assert.Equal(new AdminLotOccupancyRowDto(2, 4, 0, 4, null), result.Lots[1]);   // có xe, chưa cấu hình sức chứa
        Assert.Equal(new AdminLotOccupancyRowDto(3, 0, 0, 0, 50), result.Lots[2]);     // có cấu hình, chưa có xe → 0 thật
    }

    [Fact]
    public async Task Occupancy_NoData_IsRealZero()
    {
        var result = await new GetAdminGateOccupancyUseCase(new FakeQueries(), new FixedTimeProvider(Now)).ExecuteAsync();

        Assert.Equal(0, result.Total);
        Assert.Equal(0, result.Abnormal);
        Assert.Empty(result.Lots);
    }

    [Fact]
    public async Task Devices_BuildsTypeByStatusMatrix_WithTotals()
    {
        var queries = new FakeQueries();
        queries.Devices.AddRange([
            new(GateDeviceType.QrScanner, GateDeviceStatus.Online, 4),
            new(GateDeviceType.QrScanner, GateDeviceStatus.Offline, 1),
            new(GateDeviceType.AnprCamera, GateDeviceStatus.Maintenance, 2),
            new(GateDeviceType.AnprCamera, GateDeviceStatus.Online, 3),
        ]);

        var result = await new GetAdminGateDevicesUseCase(queries, new FixedTimeProvider(Now)).ExecuteAsync();

        Assert.Equal([GateDeviceType.AnprCamera, GateDeviceType.QrScanner], result.ByType.Select(r => r.DeviceType));
        Assert.Equal(new AdminGateDeviceTypeRowDto(GateDeviceType.AnprCamera, 3, 0, 2), result.ByType[0]);
        Assert.Equal(new AdminGateDeviceTypeRowDto(GateDeviceType.QrScanner, 4, 1, 0), result.ByType[1]);
        Assert.Equal((7, 1, 2, 10), (result.Online, result.Offline, result.Maintenance, result.Total));
    }

    [Fact]
    public async Task Devices_NoDevices_IsRealZero()
    {
        var result = await new GetAdminGateDevicesUseCase(new FakeQueries(), new FixedTimeProvider(Now)).ExecuteAsync();

        Assert.Equal(0, result.Total);
        Assert.Empty(result.ByType);
    }

    private static GateDbContext SqlOnlyContext()
        => new(new DbContextOptionsBuilder<GateDbContext>().UseNpgsql("Host=localhost;Database=pm_gate_sql_shape").Options);

    [Fact]
    public void ActiveSessionGroups_AggregatesInDatabase_WithoutPersonalColumns()
    {
        using var db = SqlOnlyContext();
        var sql = AdminGateDashboardQueries.ActiveSessionGroups(db.ParkingSessions).ToQueryString();

        Assert.Contains("GROUP BY", sql);
        Assert.Contains("count(*)", sql);
        Assert.DoesNotContain("PlateNumber", sql);
        Assert.DoesNotContain("UserId", sql);
    }

    [Fact]
    public void DeviceGroups_AggregatesActiveDevicesInDatabase()
    {
        using var db = SqlOnlyContext();
        var sql = AdminGateDashboardQueries.DeviceGroups(db.GateDevices).ToQueryString();

        Assert.Contains("GROUP BY", sql);
        Assert.Contains("\"IsActive\"", sql);
        Assert.DoesNotContain("OfflinePublicKey", sql);
    }
}
