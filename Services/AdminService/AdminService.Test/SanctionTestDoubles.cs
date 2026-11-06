using System.Reflection;
using AdminService.Application.Features.Owners;
using AdminService.Application.Features.Sanctions;
using AdminService.Application.Features.Users;
using AdminService.Domain.Entities;
using AdminService.Infrastructure.Persistence.Queries;
using ParkingManagement.SharedKernel.Contracts;
using ParkingManagement.SharedKernel.Domain;
using ParkingManagement.SharedKernel.Enums;
using ParkingManagement.SharedKernel.Exceptions;

namespace AdminService.Test;

// US-097: test double dùng chung cho các test chế tài. Kho in-memory cài cả 3 port (lock US-096, ghi US-097, đọc US-097) để
// use case thấy cùng một trạng thái như khi chạy chung một AdminDbContext; phần đọc chạy đúng biểu thức của SanctionQueryFilter.
internal static class SanctionFixtures
{
    public const int Admin = 1;
    public const int OwnerTsn = 2;          // OwnerProfileId
    public const int OwnerTsnUser = 3;      // user sở hữu hồ sơ OwnerTsn
    public const int OwnerVincom = 1;
    public const int OwnerVincomUser = 2;
    public const int LotBenThanh = 4;
    public const int LotTanSonNhat = 5;
    public const int LotVincom = 1;

    /// <summary>Thứ Sáu 06/11/2026 15:00 giờ Việt Nam.</summary>
    public static readonly DateTimeOffset Now = new(2026, 11, 6, 8, 0, 0, TimeSpan.Zero);
    public static DateTime NowUtc => Now.UtcDateTime;

    public static readonly AdminUserDto TsnOwner = new(OwnerTsnUser, "Công ty Bãi xe Tân Sơn Nhất", "owner.tsn@smartparking.vn",
        "0900000003", "Active", "NotSubmitted", ["LotOwner"], new AdminUserOwnerProfileDto(OwnerTsn, "Công ty CP Bãi xe Tân Sơn Nhất"));
    public static readonly AdminUserDto VincomOwner = new(OwnerVincomUser, "Công ty Vincom Parking", "owner.vincom@smartparking.vn",
        "0900000002", "Active", "NotSubmitted", ["LotOwner"], new AdminUserOwnerProfileDto(OwnerVincom, "Công ty TNHH Vincom Parking"));

    public static T WithId<T>(this T entity, int id) where T : BaseEntity
    {
        typeof(BaseEntity).GetProperty(nameof(BaseEntity.Id), BindingFlags.Public | BindingFlags.Instance)!.SetValue(entity, id);
        return entity;
    }

    public static T CreatedAt<T>(this T entity, DateTime createdAtUtc) where T : BaseEntity
    {
        typeof(BaseEntity).GetProperty(nameof(BaseEntity.CreatedAtUtc), BindingFlags.Public | BindingFlags.Instance)!.SetValue(entity, createdAtUtc);
        return entity;
    }

    public static Sanction LotSanction(int id, int lotId, SanctionLevel level, DateTime? endsAtUtc, int ownerProfileId = OwnerTsn,
        SanctionStatus status = SanctionStatus.Active, DateTime? startsAtUtc = null)
        => new Sanction
        {
            OwnerProfileId = ownerProfileId, ParkingLotId = lotId, Level = level, Reason = "Từ chối booking nhiều lần.",
            StartsAtUtc = startsAtUtc ?? NowUtc.AddDays(-1), EndsAtUtc = endsAtUtc, Status = status, IssuedByUserId = Admin,
        }.WithId(id);

    public static Sanction OwnerLock(int id, SanctionLevel level, DateTime? endsAtUtc, SanctionStatus status = SanctionStatus.Active,
        int ownerProfileId = OwnerTsn)
    {
        var sanction = Sanction.CreateOwnerLock(ownerProfileId, "Gian lận doanh thu", Admin, NowUtc.AddDays(-2),
            level == SanctionLevel.PermanentBan ? null : endsAtUtc).WithId(id);
        sanction.Status = status;
        sanction.MarkSynced(NowUtc.AddDays(-2));
        return sanction;
    }
}

internal sealed class FixedTime(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}

internal sealed class FakeOwnerDirectoryUserService(params AdminUserDto[] users) : IUserServiceClient
{
    public List<(int OwnerProfileId, string Reason, DateTime? LockedUntilUtc)> LockCalls { get; } = [];
    public List<(int OwnerProfileId, string Reason)> UnlockCalls { get; } = [];
    public Exception? LockFailure { get; init; }

    public Task<AdminUserDto?> FindUserByIdAsync(int userId, CancellationToken cancellationToken)
        => Task.FromResult(users.FirstOrDefault(u => u.Id == userId));

    public Task<OwnerAccountDto?> LockOwnerAsync(int ownerProfileId, string reason, DateTime? lockedUntilUtc, int performedByUserId,
        CancellationToken cancellationToken)
    {
        LockCalls.Add((ownerProfileId, reason, lockedUntilUtc));
        if (LockFailure is not null) throw LockFailure;
        return Task.FromResult<OwnerAccountDto?>(new OwnerAccountDto(ownerProfileId, 3, "Bãi", "Chủ bãi", null, null, true, "Suspended", "Active"));
    }

    public Task<OwnerAccountDto?> UnlockOwnerAsync(int ownerProfileId, string reason, int performedByUserId, CancellationToken cancellationToken)
    {
        UnlockCalls.Add((ownerProfileId, reason));
        return Task.FromResult<OwnerAccountDto?>(new OwnerAccountDto(ownerProfileId, 3, "Bãi", "Chủ bãi", null, null, false, "Active", "Active"));
    }

    public Task<PagedResult<AdminUserSummaryDto>> ListUsersAsync(UserRoleType? role, int page, int pageSize, CancellationToken cancellationToken)
        => throw new NotSupportedException();
}

internal sealed class FakeLotDirectory(params (int OwnerProfileId, int LotId)[] lots) : IParkingLotDirectory
{
    public int Calls { get; private set; }
    public Exception? Failure { get; init; }

    public Task<IReadOnlyList<OwnerLotInfo>> ListOwnerLotsAsync(int ownerProfileId, CancellationToken cancellationToken)
    {
        Calls++;
        if (Failure is not null) throw Failure;
        IReadOnlyList<OwnerLotInfo> result = lots.Where(l => l.OwnerProfileId == ownerProfileId)
            .Select(l => new OwnerLotInfo(l.LotId, $"Bãi {l.LotId}", "Active")).ToList();
        return Task.FromResult(result);
    }
}

/// <summary>Kho chế tài in-memory: cấp Id khi thêm, mô phỏng partial unique index, đếm số lần lưu.</summary>
internal sealed class InMemorySanctionStore : IOwnerLockRepository, ISanctionRepository, ISanctionAdminQueries
{
    private int nextSanctionId = 100;
    private int nextAuditId = 1000;

    public InMemorySanctionStore(IEnumerable<Sanction>? sanctions = null, IEnumerable<RiskFlag>? riskFlags = null, IEnumerable<AuditLog>? audits = null)
    {
        Sanctions = [.. sanctions ?? []];
        RiskFlags = [.. riskFlags ?? []];
        AuditLogs = [.. audits ?? []];
    }

    public List<Sanction> Sanctions { get; }
    public List<RiskFlag> RiskFlags { get; }
    public List<AuditLog> AuditLogs { get; }
    public int SaveCount { get; private set; }
    public int TransactionCount { get; private set; }
    /// <summary>Mô phỏng request khác chèn trước (unique index) → ConflictException như SanctionRepository.</summary>
    public bool FailNextInsertWithUniqueViolation { get; set; }

    // ===== IOwnerLockRepository (US-096) =====
    public Task<Sanction?> FindActiveOwnerLockTrackedAsync(int ownerProfileId, CancellationToken cancellationToken)
        => Task.FromResult(Sanctions.FirstOrDefault(s => s.OwnerProfileId == ownerProfileId && s.Status == SanctionStatus.Active
            && s.ParkingLotId is null && (s.Level is SanctionLevel.TemporarySuspension or SanctionLevel.PermanentBan)));

    public Task AddLockAsync(Sanction sanction, AuditLog auditLog, CancellationToken cancellationToken)
    {
        TransactionCount++;
        Sanctions.Add(sanction.WithId(nextSanctionId++));
        AuditLogs.Add(auditLog.WithId(nextAuditId++));
        return Task.CompletedTask;
    }

    public Task<Sanction?> FindLatestOwnerLockTrackedAsync(int ownerProfileId, CancellationToken cancellationToken)
        => Task.FromResult(Sanctions.Where(s => s.OwnerProfileId == ownerProfileId && s.ParkingLotId is null
            && (s.Level is SanctionLevel.TemporarySuspension or SanctionLevel.PermanentBan)).OrderByDescending(s => s.Id).FirstOrDefault());

    public Task SaveWithAuditAsync(AuditLog auditLog, CancellationToken cancellationToken)
    {
        SaveCount++;
        AuditLogs.Add(auditLog.WithId(nextAuditId++));
        return Task.CompletedTask;
    }

    public Task SaveAsync(CancellationToken cancellationToken)
    {
        SaveCount++;
        return Task.CompletedTask;
    }

    // ===== ISanctionRepository (US-097) =====
    public Task<Sanction?> FindTrackedAsync(int id, CancellationToken cancellationToken)
        => Task.FromResult(Sanctions.FirstOrDefault(s => s.Id == id));

    public Task<IReadOnlyList<Sanction>> FindActiveLotSanctionsTrackedAsync(IReadOnlyCollection<int> parkingLotIds, SanctionLevel level,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<Sanction> result = Sanctions.Where(s => s.Status == SanctionStatus.Active && s.Level == level && s.ParkingLotId is { } lot
            && parkingLotIds.Contains(lot)).ToList();
        return Task.FromResult(result);
    }

    public Task<Sanction?> FindActiveOwnerRankingPenaltyTrackedAsync(int ownerProfileId, CancellationToken cancellationToken)
        => Task.FromResult(Sanctions.FirstOrDefault(s => s.OwnerProfileId == ownerProfileId && s.Status == SanctionStatus.Active
            && s.ParkingLotId is null && s.Level == SanctionLevel.RankingPenalty));

    public Task<RiskFlag?> FindRiskFlagTrackedAsync(int id, CancellationToken cancellationToken)
        => Task.FromResult(RiskFlags.FirstOrDefault(f => f.Id == id));

    public Task AddIssuedAsync(IReadOnlyList<Sanction> sanctions, Func<Sanction, AuditLog> buildAuditLog,
        Action<IReadOnlyList<Sanction>>? afterInsert, CancellationToken cancellationToken)
    {
        if (FailNextInsertWithUniqueViolation)
        {
            FailNextInsertWithUniqueViolation = false;
            throw new ConflictException("Bãi / chủ bãi vừa được áp chế tài cùng cấp bởi một yêu cầu khác.");
        }
        TransactionCount++;
        foreach (var sanction in sanctions) Sanctions.Add(sanction.WithId(nextSanctionId++));
        foreach (var sanction in sanctions) AuditLogs.Add(buildAuditLog(sanction).WithId(nextAuditId++));
        afterInsert?.Invoke(sanctions);
        return Task.CompletedTask;
    }

    // ===== ISanctionAdminQueries (US-097) – cùng biểu thức với SanctionAdminQueries =====
    public Task<PagedResult<SanctionSummaryDto>> ListAsync(SanctionFilter filter, DateTime nowUtc, int page, int pageSize,
        CancellationToken cancellationToken)
    {
        var query = SanctionQueryFilter.Apply(Sanctions.AsQueryable(), filter, nowUtc);
        var items = SanctionQueryFilter.NewestFirst(query).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(SanctionQueryFilter.ToSummary(nowUtc)).ToList();
        return Task.FromResult(new PagedResult<SanctionSummaryDto>(items, page, pageSize, query.Count()));
    }

    public Task<SanctionDetailDto?> GetDetailAsync(int id, DateTime nowUtc, CancellationToken cancellationToken)
    {
        var sanction = Sanctions.FirstOrDefault(s => s.Id == id);
        if (sanction is null) return Task.FromResult<SanctionDetailDto?>(null);
        var nextLock = sanction.IsOwnerLock
            ? Sanctions.Where(s => s.OwnerProfileId == sanction.OwnerProfileId && s.IsOwnerLock && s.Id > sanction.Id)
                .OrderBy(s => s.Id).Select(s => (DateTime?)s.CreatedAtUtc).FirstOrDefault()
            : null;
        var window = sanction.IsOwnerLock ? SanctionQueryFilter.TimeWindow.For(sanction.CreatedAtUtc, nextLock) : null;
        var timeline = SanctionQueryFilter.Timeline(AuditLogs.AsQueryable(), sanction, window).OrderBy(a => a.CreatedAtUtc).ThenBy(a => a.Id)
            .Select(a => new SanctionTimelineEntryDto(a.Id, a.CreatedAtUtc, a.Action, a.UserId, a.Reason)).ToList();
        var riskFlagIds = RiskFlags.Where(f => f.SanctionId == id).Select(f => f.Id).Order().ToList();
        return Task.FromResult<SanctionDetailDto?>(new SanctionDetailDto(SanctionQueryFilter.ToSummary(nowUtc).Compile()(sanction),
            sanction.EvidenceJson, sanction.RevokedByUserId, sanction.RevokeReason, sanction.UserServiceSyncedAtUtc,
            sanction.ParkingSyncedAtUtc, sanction.NotificationSentAtUtc, riskFlagIds, timeline));
    }

    public Task<IReadOnlyList<SanctionSummaryDto>> ListByIdsAsync(IReadOnlyCollection<int> ids, DateTime nowUtc, CancellationToken cancellationToken)
    {
        IReadOnlyList<SanctionSummaryDto> result = Sanctions.AsQueryable().Where(s => ids.Contains(s.Id)).OrderBy(s => s.Id)
            .Select(SanctionQueryFilter.ToSummary(nowUtc)).ToList();
        return Task.FromResult(result);
    }

    public Task<SanctionStatsDto> GetStatsAsync(DateTime nowUtc, CancellationToken cancellationToken)
    {
        var active = SanctionQueryFilter.InEffect(Sanctions.AsQueryable(), nowUtc).ToList();
        int Of(SanctionLevel level) => active.Count(s => s.Level == level);
        var all = Sanctions.AsQueryable();
        return Task.FromResult(new SanctionStatsDto(active.Count, Of(SanctionLevel.Warning), Of(SanctionLevel.RankingPenalty),
            Of(SanctionLevel.TemporarySuspension), Of(SanctionLevel.PermanentBan),
            all.Count(SanctionQueryFilter.AnySyncIs(AdminService.Domain.Enums.SanctionSyncStatus.Failed)),
            all.Count(SanctionQueryFilter.AnySyncIs(AdminService.Domain.Enums.SanctionSyncStatus.Pending)),
            SanctionQueryFilter.EndingWithin(all, nowUtc, TimeSpan.FromDays(7)).Count()));
    }

    public Task<IReadOnlyList<SanctionSummaryDto>> ListNotRevokedForOwnerSinceAsync(int ownerProfileId, DateTime sinceUtc, DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<SanctionSummaryDto> result = SanctionQueryFilter.NewestFirst(Sanctions.AsQueryable()
                .Where(s => s.OwnerProfileId == ownerProfileId && s.Status != SanctionStatus.Revoked && s.StartsAtUtc >= sinceUtc))
            .Select(SanctionQueryFilter.ToSummary(nowUtc)).ToList();
        return Task.FromResult(result);
    }
}
