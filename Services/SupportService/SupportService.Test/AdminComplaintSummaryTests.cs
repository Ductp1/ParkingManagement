using Microsoft.EntityFrameworkCore;
using ParkingManagement.SharedKernel.Enums;
using SupportService.Application.Features.AdminDashboard;
using SupportService.Infrastructure.Persistence;
using SupportService.Infrastructure.Persistence.Queries;

namespace SupportService.Test;

/// <summary>US-081: tổng tranh chấp đang mở cho Admin dashboard – ánh xạ trạng thái, quá hạn 48 giờ, "0 thật", hình dạng SQL.</summary>
public class AdminComplaintSummaryTests
{
    private static readonly DateTime Now = new(2026, 11, 3, 3, 0, 0, DateTimeKind.Utc);

    private sealed class FixedClock(DateTime utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(utcNow, TimeSpan.Zero);
    }

    private sealed class FakeQueries(IReadOnlyList<ComplaintStatusCountRow> counts, int overdue) : IAdminComplaintSummaryQueries
    {
        public DateTime? OverdueAsOf { get; private set; }

        public Task<IReadOnlyList<ComplaintStatusCountRow>> CountUnresolvedByStatusAsync(CancellationToken cancellationToken)
            => Task.FromResult(counts);

        public Task<int> CountAwaitingOwnerOverdueAsync(DateTime nowUtc, CancellationToken cancellationToken)
        {
            OverdueAsOf = nowUtc;
            return Task.FromResult(overdue);
        }
    }

    [Fact]
    public async Task Summary_MapsEachUnresolvedStatus_AndOverdueUsesClock()
    {
        var queries = new FakeQueries([
            new(ComplaintStatus.Open, 4), new(ComplaintStatus.AwaitingOwner, 3), new(ComplaintStatus.OwnerResponded, 2),
            new(ComplaintStatus.Escalated, 1),
        ], overdue: 1);

        var result = await new GetAdminComplaintSummaryUseCase(queries, new FixedClock(Now)).ExecuteAsync();

        Assert.Equal(new AdminComplaintSummaryDto(Now, 4, 3, 1, 2, 1), result);
        Assert.Equal(10, result.Unresolved);
        Assert.Equal(Now, queries.OverdueAsOf);
    }

    [Fact]
    public async Task Summary_NoOpenComplaints_IsRealZero()
    {
        var result = await new GetAdminComplaintSummaryUseCase(new FakeQueries([], 0), new FixedClock(Now)).ExecuteAsync();

        Assert.Equal(new AdminComplaintSummaryDto(Now, 0, 0, 0, 0, 0), result);
        Assert.Equal(0, result.Unresolved);
    }

    private static SupportDbContext SqlOnlyContext()
        => new(new DbContextOptionsBuilder<SupportDbContext>().UseNpgsql("Host=localhost;Database=pm_support_sql_shape").Options);

    [Fact]
    public void UnresolvedByStatus_GroupsInDatabase_AndExcludesClosedStatuses()
    {
        using var db = SqlOnlyContext();
        var sql = AdminComplaintSummaryQueries.UnresolvedByStatus(db.Complaints).ToQueryString();

        Assert.Contains("GROUP BY", sql);
        Assert.Contains("count(*)", sql);
        Assert.DoesNotContain("Resolved'", sql);
        Assert.DoesNotContain("Rejected", sql);
        Assert.DoesNotContain("\"Description\"", sql);
        Assert.DoesNotContain("\"UserId\"", sql);
    }

    [Fact]
    public void AwaitingOwnerOverdue_FiltersByDueDate()
    {
        using var db = SqlOnlyContext();
        var sql = AdminComplaintSummaryQueries.AwaitingOwnerOverdue(db.Complaints, Now).Select(c => c.Id).ToQueryString();

        Assert.Contains("\"OwnerResponseDueAtUtc\" <", sql);
    }
}
