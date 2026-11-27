using System.Globalization;
using System.Linq.Expressions;
using AdminService.Application.Features.Sanctions;
using AdminService.Domain.Entities;
using AdminService.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using ParkingManagement.SharedKernel.Contracts;
using ParkingManagement.SharedKernel.Enums;

namespace AdminService.Infrastructure.Persistence.Queries;

/// <summary>US-097: đọc bảng Sanctions cho màn quản lý chế tài (AsNoTracking, Select thẳng sang DTO, lọc + đếm ở PostgreSQL).</summary>
public sealed class SanctionAdminQueries(AdminDbContext db) : ISanctionAdminQueries
{
    public async Task<PagedResult<SanctionSummaryDto>> ListAsync(SanctionFilter filter, DateTime nowUtc, int page, int pageSize,
        CancellationToken cancellationToken)
    {
        var query = SanctionQueryFilter.Apply(db.Sanctions.AsNoTracking(), filter, nowUtc);
        var total = await query.CountAsync(cancellationToken);
        var items = await SanctionQueryFilter.NewestFirst(query)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(SanctionQueryFilter.ToSummary(nowUtc))
            .ToListAsync(cancellationToken);
        return new PagedResult<SanctionSummaryDto>(items, page, pageSize, total);
    }

    public async Task<SanctionDetailDto?> GetDetailAsync(int id, DateTime nowUtc, CancellationToken cancellationToken)
    {
        var sanction = await db.Sanctions.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
        if (sanction is null) return null;
        var summary = SanctionQueryFilter.ToSummary(nowUtc).Compile()(sanction);

        var riskFlagIds = await db.RiskFlags.AsNoTracking().Where(f => f.SanctionId == id).OrderBy(f => f.Id).Select(f => f.Id)
            .ToListAsync(cancellationToken);
        var window = sanction.IsOwnerLock ? await OwnerLockWindowAsync(sanction, cancellationToken) : null;
        var timeline = await SanctionQueryFilter.Timeline(db.AuditLogs.AsNoTracking(), sanction, window)
            .OrderBy(a => a.CreatedAtUtc).ThenBy(a => a.Id)
            .Select(a => new SanctionTimelineEntryDto(a.Id, a.CreatedAtUtc, a.Action, a.UserId, a.Reason))
            .ToListAsync(cancellationToken);

        return new SanctionDetailDto(summary,sanction.EvidenceJson, sanction.RevokedByUserId, sanction.RevokeReason,
            sanction.UserServiceSyncedAtUtc, sanction.ParkingSyncedAtUtc, sanction.NotificationSentAtUtc, riskFlagIds, timeline);
    }

    public async Task<IReadOnlyList<SanctionSummaryDto>> ListByIdsAsync(IReadOnlyCollection<int> ids, DateTime nowUtc,
        CancellationToken cancellationToken)
        => await db.Sanctions.AsNoTracking().Where(s => ids.Contains(s.Id)).OrderBy(s => s.Id)
            .Select(SanctionQueryFilter.ToSummary(nowUtc)).ToListAsync(cancellationToken);

    public async Task<SanctionStatsDto> GetStatsAsync(DateTime nowUtc, CancellationToken cancellationToken)
    {
        var activeByLevel = await SanctionQueryFilter.InEffect(db.Sanctions.AsNoTracking(), nowUtc)
            .GroupBy(s => s.Level)
            .Select(g => new { Level = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Level, x => x.Count, cancellationToken);
        var syncFailed = await db.Sanctions.AsNoTracking().CountAsync(SanctionQueryFilter.AnySyncIs(SanctionSyncStatus.Failed), cancellationToken);
        var syncPending = await db.Sanctions.AsNoTracking().CountAsync(SanctionQueryFilter.AnySyncIs(SanctionSyncStatus.Pending), cancellationToken);
        var endingSoon = await SanctionQueryFilter.EndingWithin(db.Sanctions.AsNoTracking(), nowUtc, TimeSpan.FromDays(7))
            .CountAsync(cancellationToken);
        int Of(SanctionLevel level) => activeByLevel.GetValueOrDefault(level);
        return new SanctionStatsDto(activeByLevel.Values.Sum(), Of(SanctionLevel.Warning), Of(SanctionLevel.RankingPenalty),
            Of(SanctionLevel.TemporarySuspension), Of(SanctionLevel.PermanentBan), syncFailed, syncPending, endingSoon);
    }

    public async Task<IReadOnlyList<SanctionSummaryDto>> ListNotRevokedForOwnerSinceAsync(int ownerProfileId, DateTime sinceUtc,
        DateTime nowUtc, CancellationToken cancellationToken)
        => await SanctionQueryFilter.NewestFirst(db.Sanctions.AsNoTracking()
                .Where(s => s.OwnerProfileId == ownerProfileId && s.Status != SanctionStatus.Revoked && s.StartsAtUtc >= sinceUtc))
            .Select(SanctionQueryFilter.ToSummary(nowUtc)).ToListAsync(cancellationToken);

    /// <summary>Audit khóa / mở khóa (US-096) ghi theo OwnerProfile: lấy những dòng từ lúc tạo chế tài tới lúc có khóa kế tiếp.</summary>
    private async Task<SanctionQueryFilter.TimeWindow> OwnerLockWindowAsync(Sanction sanction, CancellationToken cancellationToken)
    {
        var nextLockCreatedAt = await db.Sanctions.AsNoTracking()
            .Where(s => s.OwnerProfileId == sanction.OwnerProfileId && s.ParkingLotId == null && s.Id > sanction.Id
                && (s.Level == SanctionLevel.TemporarySuspension || s.Level == SanctionLevel.PermanentBan))
            .OrderBy(s => s.Id).Select(s => (DateTime?)s.CreatedAtUtc).FirstOrDefaultAsync(cancellationToken);
        return SanctionQueryFilter.TimeWindow.For(sanction.CreatedAtUtc, nextLockCreatedAt);
    }
}

/// <summary>Biểu thức lọc / chiếu dùng chung cho SanctionAdminQueries và unit test (chạy LINQ in-memory).</summary>
public static class SanctionQueryFilter
{
    /// <summary>Audit log được tạo trong cùng request nhưng trước / sau chế tài vài mili-giây.</summary>
    private static readonly TimeSpan ClockTolerance = TimeSpan.FromSeconds(5);

    /// <summary>ToUtc = null: chưa có khóa kế tiếp – không giới hạn trên.</summary>
    public sealed record TimeWindow(DateTime FromUtc, DateTime? ToUtc)
    {
        public static TimeWindow For(DateTime createdAtUtc, DateTime? nextLockCreatedAtUtc)
            => new(createdAtUtc - ClockTolerance, nextLockCreatedAtUtc - ClockTolerance);
    }

    /// <summary>Trạng thái hiệu lực: Active đã qua EndsAtUtc hiển thị là Expired (cùng quy tắc Sanction.IsInEffect).</summary>
    public static Expression<Func<Sanction, SanctionSummaryDto>> ToSummary(DateTime nowUtc) => s => new SanctionSummaryDto(
        s.Id, s.OwnerProfileId, s.OwnerUserId, s.ParkingLotId, s.Level.ToString(), s.Reason, s.StartsAtUtc, s.EndsAtUtc,
        s.CorrectionDueAtUtc, s.AppealDeadlineUtc,
        s.Status == SanctionStatus.Active && s.EndsAtUtc != null && s.EndsAtUtc <= nowUtc ? nameof(SanctionStatus.Expired) : s.Status.ToString(),
        s.PenaltyAmount, s.IssuedByUserId, s.RevokedAtUtc, s.UserServiceSyncStatus.ToString(), s.ParkingSyncStatus.ToString(),
        s.NotificationSyncStatus.ToString(), s.CreatedAtUtc, s.UpdatedAtUtc);

    public static IQueryable<Sanction> InEffect(IQueryable<Sanction> query, DateTime nowUtc)
        => query.Where(s => s.Status == SanctionStatus.Active && (s.EndsAtUtc == null || s.EndsAtUtc > nowUtc));

    public static IQueryable<Sanction> EndingWithin(IQueryable<Sanction> query, DateTime nowUtc, TimeSpan within)
    {
        var until = nowUtc + within;
        return InEffect(query, nowUtc).Where(s => s.EndsAtUtc != null && s.EndsAtUtc <= until);
    }

    public static Expression<Func<Sanction, bool>> AnySyncIs(SanctionSyncStatus status)
        => s => s.UserServiceSyncStatus == status || s.ParkingSyncStatus == status || s.NotificationSyncStatus == status;

    public static IQueryable<Sanction> Apply(IQueryable<Sanction> query, SanctionFilter filter, DateTime nowUtc)
    {
        query = filter.Status switch
        {
            SanctionStatus.Active => InEffect(query, nowUtc),
            SanctionStatus.Expired => query.Where(s => s.Status == SanctionStatus.Expired
                || (s.Status == SanctionStatus.Active && s.EndsAtUtc != null && s.EndsAtUtc <= nowUtc)),
            SanctionStatus.Revoked => query.Where(s => s.Status == SanctionStatus.Revoked),
            _ => query,
        };
        if (filter.Level is { } level) query = query.Where(s => s.Level == level);
        if (filter.OwnerProfileId is { } ownerProfileId) query = query.Where(s => s.OwnerProfileId == ownerProfileId);
        if (filter.ParkingLotId is { } parkingLotId) query = query.Where(s => s.ParkingLotId == parkingLotId);
        if (filter.FromUtc is { } from) query = query.Where(s => s.StartsAtUtc >= from);
        if (filter.ToUtc is { } to) query = query.Where(s => s.StartsAtUtc < to);
        if (filter.SyncFailedOnly) query = query.Where(AnySyncIs(SanctionSyncStatus.Failed));
        if (filter.Search is { } search)
        {
            // Số (có thể kèm '#') → Id chế tài / chủ bãi / bãi; còn lại tìm trong lý do (không phân biệt hoa thường).
            var term = search.ToLowerInvariant();
            int? number = int.TryParse(search.TrimStart('#'), NumberStyles.None, CultureInfo.InvariantCulture, out var n) ? n : null;
            query = query.Where(s => (number != null && (s.Id == number || s.OwnerProfileId == number || s.ParkingLotId == number))
                                     || s.Reason.ToLower().Contains(term));
        }
        return query;
    }

    /// <summary>Mới áp trước; Id phá hòa để phân trang ổn định.</summary>
    public static IOrderedQueryable<Sanction> NewestFirst(IQueryable<Sanction> query)
        => query.OrderByDescending(s => s.StartsAtUtc).ThenByDescending(s => s.Id);

    /// <summary>
    /// Audit của chế tài: Sanction.* theo Id; với khóa chủ bãi thêm Owner.Locked / Owner.Unlocked (US-096 ghi theo OwnerProfile)
    /// trong khung thời gian của chế tài.
    /// </summary>
    public static IQueryable<AuditLog> Timeline(IQueryable<AuditLog> audits, Sanction sanction, TimeWindow? ownerLockWindow)
    {
        var sanctionId = sanction.Id.ToString(CultureInfo.InvariantCulture);
        if (ownerLockWindow is null)
            return audits.Where(a => a.EntityName == "Sanction" && a.EntityId == sanctionId);

        var ownerId = sanction.OwnerProfileId.ToString(CultureInfo.InvariantCulture);
        var (from, to) = (ownerLockWindow.FromUtc, ownerLockWindow.ToUtc);
        return audits.Where(a => (a.EntityName == "Sanction" && a.EntityId == sanctionId)
            || (a.EntityName == "OwnerProfile" && a.EntityId == ownerId && (a.Action == "Owner.Locked" || a.Action == "Owner.Unlocked")
                && a.CreatedAtUtc >= from && (to == null || a.CreatedAtUtc < to)));
    }
}
