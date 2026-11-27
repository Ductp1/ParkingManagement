using Microsoft.EntityFrameworkCore;
using ParkingManagement.SharedKernel.Enums;
using ParkingService.Application.Abstractions;
using ParkingService.Application.Features.AdminDashboard;
using ParkingService.Infrastructure.Persistence;
using ParkingService.Infrastructure.Persistence.Queries;

namespace ParkingService.Test;

/// <summary>US-081: danh sách bãi cho Admin dashboard – trả nguyên dòng từ port + thời điểm, SQL bỏ bãi đã xóa và không đọc cột riêng tư.</summary>
public class AdminLotsSummaryTests
{
    private static readonly DateTime Now = new(2026, 11, 3, 3, 0, 0, DateTimeKind.Utc);

    private sealed class FixedClock(DateTime utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(utcNow, TimeSpan.Zero);
    }

    private sealed class FakeQueries(IReadOnlyList<AdminLotSummaryRow> rows) : IAdminLotSummaryQueries
    {
        public Task<IReadOnlyList<AdminLotSummaryRow>> ListAsync(CancellationToken cancellationToken = default) => Task.FromResult(rows);
    }

    [Fact]
    public async Task Summary_ReturnsRowsWithAsOf()
    {
        AdminLotSummaryRow[] rows =
        [
            new(1, "Bãi A", "TP.HCM", "Quận 1", ParkingLotStatus.Active, IntegrationTier.Manual, 120, false, false),
            new(2, "Bãi B", "TP.HCM", null, ParkingLotStatus.Suspended, IntegrationTier.PmsApi, 40, true, true),
        ];

        var result = await new GetAdminLotsSummaryUseCase(new FakeQueries(rows), new FixedClock(Now)).ExecuteAsync();

        Assert.Equal(Now, result.AsOfUtc);
        Assert.Equal(rows, result.Lots);
    }

    [Fact]
    public async Task Summary_NoLots_IsEmptyList()
    {
        var result = await new GetAdminLotsSummaryUseCase(new FakeQueries([]), new FixedClock(Now)).ExecuteAsync();

        Assert.Empty(result.Lots);
    }

    [Fact]
    public void Rows_ExcludeDeletedLots_AndPrivateColumns()
    {
        using var db = new ParkingDbContext(new DbContextOptionsBuilder<ParkingDbContext>()
            .UseNpgsql("Host=localhost;Database=pm_parking_sql_shape").Options);
        var sql = AdminLotSummaryQueries.Rows(db.ParkingLots, db.LotCapacityConfigs).ToQueryString();

        Assert.Contains("\"IsDeleted\"", sql);
        Assert.Contains("\"IsEmergencyStopped\"", sql);
        Assert.DoesNotContain("\"OwnerProfileId\"", sql);
        Assert.DoesNotContain("\"HotlinePhone\"", sql);
        Assert.DoesNotContain("\"Address\"", sql);
    }
}
