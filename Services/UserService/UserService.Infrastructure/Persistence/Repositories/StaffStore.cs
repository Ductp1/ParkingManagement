using Microsoft.EntityFrameworkCore;
using ParkingManagement.SharedKernel.Enums;
using UserService.Application.Features.Privacy;
using UserService.Application.Features.Staff;
using UserService.Domain.Entities;

namespace UserService.Infrastructure.Persistence.Repositories;

public sealed class StaffStore(UserDbContext db) : IStaffStore
{
    public async Task<IReadOnlyList<StaffItem>> ListActiveAsync(int ownerProfileId, int page, int pageSize, CancellationToken ct)
        => await db.StaffAssignments.AsNoTracking()
            .Where(a => a.OwnerProfileId == ownerProfileId && a.IsActive)
            .OrderByDescending(a => a.Id).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(a => new StaffItem(a.Id, a.StaffUserId, a.StaffUser.FullName, a.ParkingLotId, a.StaffUser.Status.ToString()))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<StaffAssignment>> AssignmentsOfUserAsync(int staffUserId, CancellationToken ct)
        => await db.StaffAssignments.AsNoTracking().Where(a => a.StaffUserId == staffUserId).ToListAsync(ct);

    public Task<StaffAssignment?> FindAssignmentAsync(int id, int ownerProfileId, CancellationToken ct)
        => db.StaffAssignments.FirstOrDefaultAsync(a => a.Id == id && a.OwnerProfileId == ownerProfileId, ct);
}

public sealed class DataRequestStore(UserDbContext db) : IDataRequestStore
{
    public async Task<IReadOnlyList<DataRequestItem>> ListForUserAsync(int userId, int page, int pageSize, CancellationToken ct)
        => await db.DataSubjectRequests.AsNoTracking().Where(r => r.UserId == userId)
            .OrderByDescending(r => r.Id).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(r => new DataRequestItem(r.Id, r.RequestType.ToString(), r.Status.ToString(), r.CreatedAtUtc, r.DueAtUtc,
                r.Reason, r.ResultNote, r.CompletedAtUtc))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<AdminDataRequestItem>> ListAsync(DataRequestStatus? status, DataRequestType? type,
        int page, int pageSize, CancellationToken ct)
    {
        var query = db.DataSubjectRequests.AsNoTracking();
        if (status is { } s) query = query.Where(r => r.Status == s);
        if (type is { } t) query = query.Where(r => r.RequestType == t);
        // Yêu cầu sắp tới hạn xử lý lên trước.
        return await query.OrderBy(r => r.DueAtUtc).ThenBy(r => r.Id).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(r => new AdminDataRequestItem(r.Id, r.UserId, r.RequestType.ToString(), r.Status.ToString(),
                r.CreatedAtUtc, r.DueAtUtc, r.CompletedAtUtc, r.HandledByUserId, r.ResultNote))
            .ToListAsync(ct);
    }

    public Task<DataSubjectRequest?> FindOpenAsync(int userId, DataRequestType type, CancellationToken ct)
        => db.DataSubjectRequests.FirstOrDefaultAsync(r => r.UserId == userId && r.RequestType == type
            && (r.Status == DataRequestStatus.Submitted || r.Status == DataRequestStatus.InProgress), ct);

    public Task<DataSubjectRequest?> FindAsync(int id, CancellationToken ct)
        => db.DataSubjectRequests.FirstOrDefaultAsync(r => r.Id == id, ct);

    public async Task DeactivateStaffAssignmentsAsync(int userId, DateTime now, CancellationToken ct)
        => await db.StaffAssignments.Where(a => a.StaffUserId == userId && a.IsActive)
            .ExecuteUpdateAsync(s => s.SetProperty(a => a.IsActive, false)
                .SetProperty(a => a.RevokedAtUtc, (DateTime?)now).SetProperty(a => a.UpdatedAtUtc, (DateTime?)now), ct);

    public Task<int> ActiveSessionCountAsync(int userId, DateTime now, CancellationToken ct)
        => db.RefreshTokens.CountAsync(t => t.UserId == userId && t.RevokedAtUtc == null && t.ExpiresAtUtc > now, ct);

    public async Task<IReadOnlyList<SecurityActivity>> RecentSecurityEventsAsync(int userId, int take, CancellationToken ct)
        => await db.SecurityEvents.AsNoTracking().Where(e => e.UserId == userId)
            .OrderByDescending(e => e.CreatedAtUtc).ThenByDescending(e => e.Id).Take(take)
            .Select(e => new SecurityActivity(e.EventType.ToString(), e.CreatedAtUtc, e.IpAddress))
            .ToListAsync(ct);
}
