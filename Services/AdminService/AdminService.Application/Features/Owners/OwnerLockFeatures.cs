using System.Text.Json;
using AdminService.Application.Features.Users;
using AdminService.Domain.Entities;
using AdminService.Domain.Enums;
using Microsoft.Extensions.Logging;
using ParkingManagement.SharedKernel.Enums;
using ParkingManagement.SharedKernel.Exceptions;

namespace AdminService.Application.Features.Owners;

// ===== COMMAND =====
/// <summary>
/// Khóa chủ bãi (US-096). OwnerUserId = user sở hữu hồ sơ chủ bãi, dùng để xác minh hồ sơ có thật qua UserService.
/// LockedUntilUtc = null là khóa vô thời hạn. Admin thao tác không nằm trong body: controller lấy từ JWT.
/// </summary>
public sealed record LockOwnerRequest(int OwnerUserId, string? Reason, DateTime? LockedUntilUtc = null);

/// <summary>Mở khóa chủ bãi (US-096). Reason = lý do mở khóa, lưu trong audit log và (US-097) RevokeReason của chế tài.</summary>
public sealed record UnlockOwnerRequest(string? Reason);

// ===== PORT (ghi) =====
public interface IOwnerLockRepository
{
    /// <summary>Chế tài khóa cấp chủ bãi đang Active (tối đa 1 – unique index), entity có tracking để cập nhật.</summary>
    Task<Sanction?> FindActiveOwnerLockTrackedAsync(int ownerProfileId, CancellationToken cancellationToken);
    /// <summary>
    /// Ghi chế tài khóa mới + audit log trong CÙNG 1 lần SaveChanges. Chế tài cũ vừa bị đóng (quá hạn / bị thay thế) được ghi
    /// trước, chung transaction. Hai request khóa cùng lúc: request đến sau bị unique index chặn → ConflictException.
    /// </summary>
    Task AddLockAsync(Sanction sanction, AuditLog auditLog, CancellationToken cancellationToken);
    /// <summary>Chế tài khóa cấp chủ bãi mới nhất (mọi trạng thái), entity có tracking để cập nhật.</summary>
    Task<Sanction?> FindLatestOwnerLockTrackedAsync(int ownerProfileId, CancellationToken cancellationToken);
    /// <summary>Ghi thay đổi của chế tài đang tracking + audit log trong CÙNG 1 lần SaveChanges.</summary>
    Task SaveWithAuditAsync(AuditLog auditLog, CancellationToken cancellationToken);
    Task SaveAsync(CancellationToken cancellationToken);
}

// ===== USE CASE (ghi) =====
public interface ILockOwnerUseCase
{
    Task<SanctionDto> ExecuteAsync(int ownerProfileId, LockOwnerRequest request, int performedByUserId, CancellationToken cancellationToken = default);
}

/// <summary>
/// US-096 (UC-40): Admin khóa tài khoản chủ bãi, bắt buộc có lý do.
/// Thứ tự: xác minh chủ bãi qua UserService → ghi Sanction + AuditLog vào pm_admin (1 lần SaveChanges) → gọi UserService khóa
/// → ghi kết quả đồng bộ (Synced / Failed). UserService lỗi KHÔNG làm mất chế tài: gọi lại chính API này để thử lại,
/// không tạo chế tài trùng.
/// </summary>
public sealed class LockOwnerUseCase(IUserServiceClient userService, IOwnerLockRepository repository,
    TimeProvider timeProvider, ILogger<LockOwnerUseCase> logger) : ILockOwnerUseCase
{
    private readonly OwnerLockCore core = new(userService, timeProvider, logger);

    public async Task<SanctionDto> ExecuteAsync(int ownerProfileId, LockOwnerRequest request, int performedByUserId, CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;

        if (ownerProfileId <= 0) throw new ValidationException("ownerProfileId phải là số nguyên dương.");
        if (request.OwnerUserId <= 0) throw new ValidationException("ownerUserId phải là số nguyên dương.");
        if (performedByUserId <= 0) throw new ValidationException("performedByUserId phải là số nguyên dương.");
        if (string.IsNullOrWhiteSpace(request.Reason)) throw new ValidationException("Lý do khóa không được để trống.");
        var reason = request.Reason.Trim();
        if (reason.Length > Sanction.ReasonMaxLength) throw new ValidationException("Lý do khóa tối đa 1000 ký tự.");
        var lockedUntilUtc = UtcDateTime.ToUtc(request.LockedUntilUtc);
        if (lockedUntilUtc <= now) throw new ValidationException("lockedUntilUtc phải sau thời điểm hiện tại.");

        // Xác minh hồ sơ chủ bãi có thật trước khi ghi – không lưu chế tài cho OwnerProfileId không tồn tại.
        await core.VerifyOwnerAsync(ownerProfileId, request.OwnerUserId, cancellationToken);

        var sanction = await repository.FindActiveOwnerLockTrackedAsync(ownerProfileId, cancellationToken);
        if (sanction is not null && sanction.IsInEffect(now))
        {
            if (sanction.UserServiceSyncStatus == SanctionSyncStatus.Synced)
                throw new ConflictException($"Chủ bãi {ownerProfileId} đang bị khóa (chế tài #{sanction.Id}).");
            // Pending / Failed: gọi lại = thử đồng bộ lại chế tài đã có, giữ nguyên lý do và thời hạn đã lưu.
        }
        else
        {
            sanction?.Expire();
            sanction = Sanction.CreateOwnerLock(ownerProfileId, reason, performedByUserId, now, lockedUntilUtc);
            sanction.OwnerUserId = request.OwnerUserId;      // US-097: lưu người sở hữu hồ sơ đã xác minh
            await repository.AddLockAsync(sanction, OwnerLockCore.BuildLockAuditLog(sanction, request.OwnerUserId), cancellationToken);
        }

        await core.SyncLockAsync(sanction, cancellationToken);
        await repository.SaveAsync(cancellationToken);
        return SanctionMapper.ToDto(sanction);
    }
}

public interface IUnlockOwnerUseCase
{
    Task<SanctionDto> ExecuteAsync(int ownerProfileId, UnlockOwnerRequest request, int performedByUserId, CancellationToken cancellationToken = default);
}

/// <summary>
/// US-096 (UC-40): Admin mở khóa tài khoản chủ bãi, bắt buộc có lý do.
/// Thứ tự: chuyển chế tài khóa sang Revoked + ghi AuditLog (1 lần SaveChanges) → gọi UserService mở khóa → ghi kết quả đồng bộ.
/// UserService lỗi: chế tài vẫn Revoked, gọi lại chính API này để thử lại (không ghi thêm audit log).
/// </summary>
public sealed class UnlockOwnerUseCase(IUserServiceClient userService, IOwnerLockRepository repository,
    TimeProvider timeProvider, ILogger<UnlockOwnerUseCase> logger) : IUnlockOwnerUseCase
{
    private readonly OwnerLockCore core = new(userService, timeProvider, logger);

    public async Task<SanctionDto> ExecuteAsync(int ownerProfileId, UnlockOwnerRequest request, int performedByUserId, CancellationToken cancellationToken = default)
    {
        if (ownerProfileId <= 0) throw new ValidationException("ownerProfileId phải là số nguyên dương.");
        if (performedByUserId <= 0) throw new ValidationException("performedByUserId phải là số nguyên dương.");
        if (string.IsNullOrWhiteSpace(request.Reason)) throw new ValidationException("Lý do mở khóa không được để trống.");
        var reason = request.Reason.Trim();
        if (reason.Length > Sanction.ReasonMaxLength) throw new ValidationException("Lý do mở khóa tối đa 1000 ký tự.");

        var sanction = await repository.FindLatestOwnerLockTrackedAsync(ownerProfileId, cancellationToken);
        if (sanction is { Status: SanctionStatus.Active })
        {
            var auditLog = OwnerLockCore.BuildUnlockAuditLog(sanction, reason, performedByUserId);   // chụp trạng thái trước khi gỡ
            // US-097: ghi thêm người / lúc / lý do gỡ trên chính chế tài; luồng mở khóa không đổi.
            sanction.Revoke(reason, performedByUserId, timeProvider.GetUtcNow().UtcDateTime);
            await repository.SaveWithAuditAsync(auditLog, cancellationToken);
        }
        else if (sanction is not { Status: SanctionStatus.Revoked, UserServiceSyncStatus: SanctionSyncStatus.Pending or SanctionSyncStatus.Failed })
        {
            throw new ConflictException($"Chủ bãi {ownerProfileId} hiện không bị khóa.");
        }
        // Còn lại: đã Revoked nhưng UserService chưa nhận lệnh mở khóa → gọi lại = thử đồng bộ lại.

        await core.SyncUnlockAsync(sanction, reason, performedByUserId, cancellationToken);
        await repository.SaveAsync(cancellationToken);
        return SanctionMapper.ToDto(sanction);
    }
}

/// <summary>
/// Lõi dùng chung của khóa / mở khóa chủ bãi (US-096), tái dùng cho chế tài cấp 3 toàn chủ bãi / cấp 4 của US-097 thay vì
/// nhân bản: xác minh chủ bãi qua UserService, dựng audit log, gọi UserService và ghi kết quả đồng bộ lên chế tài.
/// Không tự lưu – use case gọi SaveAsync sau khi đồng bộ.
/// </summary>
internal sealed class OwnerLockCore(IUserServiceClient userService, TimeProvider timeProvider, ILogger logger)
{
    private static readonly JsonSerializerOptions AuditJson = new(JsonSerializerDefaults.Web);

    /// <summary>OwnerUserId phải là user sở hữu đúng hồ sơ chủ bãi: không có user → 404, sai hồ sơ → 400.</summary>
    public async Task VerifyOwnerAsync(int ownerProfileId, int ownerUserId, CancellationToken cancellationToken)
    {
        var owner = await userService.FindUserByIdAsync(ownerUserId, cancellationToken)
            ?? throw new NotFoundException("Người dùng", ownerUserId);
        if (owner.OwnerProfile?.Id != ownerProfileId)
            throw new ValidationException($"Người dùng {ownerUserId} không sở hữu hồ sơ chủ bãi {ownerProfileId}.");
    }

    public async Task SyncLockAsync(Sanction sanction, CancellationToken cancellationToken)
    {
        try
        {
            var account = await userService.LockOwnerAsync(sanction.OwnerProfileId, sanction.Reason, sanction.EndsAtUtc,
                sanction.IssuedByUserId, cancellationToken);
            if (account is null)
            {
                logger.LogWarning("UserService không tìm thấy chủ bãi {OwnerProfileId} khi khóa.", sanction.OwnerProfileId);
                sanction.MarkSyncFailed();
                return;
            }

            sanction.MarkSynced(timeProvider.GetUtcNow().UtcDateTime);
        }
        // Thay đổi đã lưu trong pm_admin: UserService không gọi được hay từ chối token đều chỉ làm lần đồng bộ này thất bại.
        catch (Exception ex) when (ex is DependencyUnavailableException or AuthenticationException or ForbiddenException)
        {
            logger.LogWarning(ex, "Chưa đồng bộ được lệnh khóa chủ bãi {OwnerProfileId} sang UserService.", sanction.OwnerProfileId);
            sanction.MarkSyncFailed();
        }
    }

    public async Task SyncUnlockAsync(Sanction sanction, string reason, int performedByUserId, CancellationToken cancellationToken)
    {
        try
        {
            var account = await userService.UnlockOwnerAsync(sanction.OwnerProfileId, reason, performedByUserId, cancellationToken);
            if (account is null)
            {
                logger.LogWarning("UserService không tìm thấy chủ bãi {OwnerProfileId} khi mở khóa.", sanction.OwnerProfileId);
                sanction.MarkSyncFailed();
                return;
            }

            sanction.MarkSynced(timeProvider.GetUtcNow().UtcDateTime);
        }
        // Thay đổi đã lưu trong pm_admin: UserService không gọi được hay từ chối token đều chỉ làm lần đồng bộ này thất bại.
        catch (Exception ex) when (ex is DependencyUnavailableException or AuthenticationException or ForbiddenException)
        {
            logger.LogWarning(ex, "Chưa đồng bộ được lệnh mở khóa chủ bãi {OwnerProfileId} sang UserService.", sanction.OwnerProfileId);
            sanction.MarkSyncFailed();
        }
    }

    /// <summary>supersededSanctionId: khóa tạm vừa được thay bằng khóa vĩnh viễn (US-097); null ở luồng US-096 (nội dung giữ nguyên).</summary>
    public static AuditLog BuildLockAuditLog(Sanction sanction, int ownerUserId, int? supersededSanctionId = null) => new()
    {
        SourceService = "AdminService",
        UserId = sanction.IssuedByUserId,
        OwnerProfileId = sanction.OwnerProfileId,
        Action = "Owner.Locked",
        EntityName = "OwnerProfile",
        EntityId = sanction.OwnerProfileId.ToString(),
        OldValuesJson = supersededSanctionId is null
            ? JsonSerializer.Serialize(new { isLocked = false }, AuditJson)
            : JsonSerializer.Serialize(new { isLocked = true, sanctionId = supersededSanctionId }, AuditJson),
        NewValuesJson = JsonSerializer.Serialize(
            new { isLocked = true, ownerUserId, level = sanction.Level.ToString(), lockedUntilUtc = sanction.EndsAtUtc }, AuditJson),
        Reason = sanction.Reason,
    };

    public static AuditLog BuildUnlockAuditLog(Sanction sanction, string reason, int performedByUserId) => new()
    {
        SourceService = "AdminService",
        UserId = performedByUserId,
        OwnerProfileId = sanction.OwnerProfileId,
        Action = "Owner.Unlocked",
        EntityName = "OwnerProfile",
        EntityId = sanction.OwnerProfileId.ToString(),
        OldValuesJson = JsonSerializer.Serialize(
            new { isLocked = true, sanctionId = sanction.Id, level = sanction.Level.ToString(), lockedUntilUtc = sanction.EndsAtUtc }, AuditJson),
        NewValuesJson = JsonSerializer.Serialize(new { isLocked = false }, AuditJson),
        Reason = reason,
    };
}
