using AdminService.Application.Features.Dashboard;
using AdminService.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using ParkingManagement.SharedKernel.Enums;

namespace AdminService.Infrastructure.Persistence.Queries;

/// <summary>
/// US-081: số liệu dashboard trong pm_admin. Mỗi widget một truy vấn GROUP BY / COUNT chạy ở PostgreSQL (AsNoTracking),
/// không tải bản ghi về rồi mới đếm. Dùng index sẵn có: PartnerApplications(Status), LotContracts(Status),
/// RiskFlags(Status, Severity, DueAtUtc), Sanctions(OwnerProfileId, Status), SystemConfigChanges(SystemConfigId, EffectiveFromUtc).
/// </summary>
public sealed class DashboardQueries(AdminDbContext db) : IDashboardQueries
{
    public async Task<PartnerQueueDto> CountPartnerApplicationsAsync(CancellationToken cancellationToken)
    {
        var counts = await db.PartnerApplications.AsNoTracking()
            .Where(p => p.Status == PartnerApplicationStatus.Submitted || p.Status == PartnerApplicationStatus.UnderReview
                || p.Status == PartnerApplicationStatus.NeedsMoreInfo || p.Status == PartnerApplicationStatus.AccountIssued)
            .GroupBy(p => p.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Status, x => x.Count, cancellationToken);
        int Of(PartnerApplicationStatus s) => counts.GetValueOrDefault(s);
        return new PartnerQueueDto(Of(PartnerApplicationStatus.Submitted), Of(PartnerApplicationStatus.UnderReview),
            Of(PartnerApplicationStatus.NeedsMoreInfo), Of(PartnerApplicationStatus.AccountIssued));
    }

    public async Task<LotContractsQueueDto> CountLotContractsToSignAsync(CancellationToken cancellationToken)
        => new(await db.LotContracts.AsNoTracking().CountAsync(c => c.Status == LotContractStatus.Pending, cancellationToken));

    public async Task<RiskFlagsDto> CountRiskFlagsAsync(DateTime nowUtc, CancellationToken cancellationToken)
    {
        var groups = await db.RiskFlags.AsNoTracking()
            .Where(r => r.Status == RiskFlagStatus.Open || r.Status == RiskFlagStatus.Investigating)
            .GroupBy(r => new { r.Status, r.Severity })
            .Select(g => new { g.Key.Status, g.Key.Severity, Count = g.Count(), Overdue = g.Count(r => r.DueAtUtc < nowUtc) })
            .ToListAsync(cancellationToken);
        return new RiskFlagsDto(
            groups.Where(g => g.Status == RiskFlagStatus.Open).Sum(g => g.Count),
            groups.Where(g => g.Status == RiskFlagStatus.Investigating).Sum(g => g.Count),
            groups.Where(g => g.Severity == RiskSeverity.Critical).Sum(g => g.Count),
            groups.Where(g => g.Severity == RiskSeverity.Warning).Sum(g => g.Count),
            groups.Where(g => g.Severity == RiskSeverity.Info).Sum(g => g.Count),
            groups.Sum(g => g.Overdue));
    }

    public async Task<ActiveSanctionsDto> CountActiveSanctionsAsync(DateTime nowUtc, CancellationToken cancellationToken)
    {
        // Cùng điều kiện với Sanction.IsInEffect: Active và chưa hết hạn (EndsAtUtc null = vô thời hạn).
        var counts = await db.Sanctions.AsNoTracking()
            .Where(s => s.Status == SanctionStatus.Active && (s.EndsAtUtc == null || s.EndsAtUtc > nowUtc))
            .GroupBy(s => s.Level)
            .Select(g => new { Level = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Level, x => x.Count, cancellationToken);
        int Of(SanctionLevel l) => counts.GetValueOrDefault(l);
        return new ActiveSanctionsDto(counts.Values.Sum(), Of(SanctionLevel.Warning), Of(SanctionLevel.RankingPenalty),
            Of(SanctionLevel.TemporarySuspension), Of(SanctionLevel.PermanentBan));
    }

    public async Task<ScheduledConfigChangesDto> GetScheduledConfigChangesAsync(DateTime nowUtc, CancellationToken cancellationToken)
    {
        // Cùng quy tắc trạng thái Scheduled của US-098: chưa hủy và chưa tới mốc hiệu lực.
        var scheduled = db.SystemConfigChanges.AsNoTracking().Where(c => c.CancelledAtUtc == null && c.EffectiveFromUtc > nowUtc);
        var count = await scheduled.CountAsync(cancellationToken);
        if (count == 0) return new ScheduledConfigChangesDto(0, null, null);
        var next = await scheduled
            .OrderBy(c => c.EffectiveFromUtc).ThenBy(c => c.Id)
            .Join(db.SystemConfigs.AsNoTracking(), c => c.SystemConfigId, s => s.Id, (c, s) => new { s.Key, c.EffectiveFromUtc })
            .FirstAsync(cancellationToken);
        return new ScheduledConfigChangesDto(count, next.Key, next.EffectiveFromUtc);
    }

    public async Task<AuditTodayDto> CountAuditLogsAsync(DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken)
    {
        var bySource = await db.AuditLogs.AsNoTracking()
            .Where(a => a.CreatedAtUtc >= fromUtc && a.CreatedAtUtc < toUtc)
            .GroupBy(a => a.SourceService)
            .Select(g => new AuditSourceCountDto(g.Key, g.Count()))
            .ToListAsync(cancellationToken);
        var ordered = bySource.OrderByDescending(s => s.Count).ThenBy(s => s.SourceService, StringComparer.Ordinal).ToList();
        return new AuditTodayDto(ordered.Sum(s => s.Count), ordered);
    }
}
