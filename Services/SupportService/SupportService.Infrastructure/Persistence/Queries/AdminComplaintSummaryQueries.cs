using Microsoft.EntityFrameworkCore;
using ParkingManagement.SharedKernel.Enums;
using SupportService.Application.Features.AdminDashboard;
using SupportService.Domain.Entities;

namespace SupportService.Infrastructure.Persistence.Queries;

/// <summary>US-081: đếm tranh chấp cho Admin dashboard – AsNoTracking + GroupBy/Count ở database.</summary>
public sealed class AdminComplaintSummaryQueries(SupportDbContext db) : IAdminComplaintSummaryQueries
{
    public async Task<IReadOnlyList<ComplaintStatusCountRow>> CountUnresolvedByStatusAsync(CancellationToken cancellationToken)
        => await UnresolvedByStatus(db.Complaints.AsNoTracking()).ToListAsync(cancellationToken);

    public Task<int> CountAwaitingOwnerOverdueAsync(DateTime nowUtc, CancellationToken cancellationToken)
        => AwaitingOwnerOverdue(db.Complaints.AsNoTracking(), nowUtc).CountAsync(cancellationToken);

    /// <summary>So sánh tường minh từng trạng thái (không dùng mảng.Contains để EF luôn dịch được sang SQL).</summary>
    public static IQueryable<ComplaintStatusCountRow> UnresolvedByStatus(IQueryable<Complaint> complaints)
        => complaints
            .Where(c => c.Status == ComplaintStatus.Open || c.Status == ComplaintStatus.AwaitingOwner
                        || c.Status == ComplaintStatus.OwnerResponded || c.Status == ComplaintStatus.Escalated)
            .GroupBy(c => c.Status)
            .Select(g => new ComplaintStatusCountRow(g.Key, g.Count()));

    public static IQueryable<Complaint> AwaitingOwnerOverdue(IQueryable<Complaint> complaints, DateTime nowUtc)
        => complaints.Where(c => c.Status == ComplaintStatus.AwaitingOwner && c.OwnerResponseDueAtUtc < nowUtc);
}
