using System.Text.Json;
using AdminService.Application.Features.Users;
using AdminService.Domain.Entities;
using AdminService.Domain.Enums;
using Microsoft.Extensions.Logging;
using ParkingManagement.SharedKernel.Exceptions;

namespace AdminService.Application.Features.Owners;

// ===== COMMAND =====
/// <summary>
/// Khóa chủ bãi (US-096). OwnerUserId = user sở hữu hồ sơ chủ bãi, dùng để xác minh hồ sơ có thật qua UserService.
/// PerformedByUserId = admin thao tác (tạm nhận qua body, sẽ lấy từ JWT khi T-102 merge). LockedUntilUtc = null là khóa vô thời hạn.
/// </summary>
public sealed record LockOwnerRequest(int OwnerUserId, string? Reason, int PerformedByUserId, DateTime? LockedUntilUtc = null);

// ===== PORT (ghi) =====
public interface IOwnerLockRepository
{
    /// <summary>Chế tài khóa cấp chủ bãi đang Active (tối đa 1 – unique index), entity có tracking để cập nhật.</summary>
    Task<Sanction?> FindActiveOwnerLockTrackedAsync(int ownerProfileId, CancellationToken cancellationToken);
    /// <summary>
    /// Ghi chế tài khóa mới + audit log trong CÙNG 1 lần SaveChanges. Chế tài cũ vừa bị đóng (quá hạn) được ghi trước,
    /// chung transaction. Hai request khóa cùng lúc: request đến sau bị unique index chặn → ConflictException.
    /// </summary>
    Task AddLockAsync(Sanction sanction, AuditLog auditLog, CancellationToken cancellationToken);
    Task SaveAsync(CancellationToken cancellationToken);
}

// ===== USE CASE (ghi) =====
public interface ILockOwnerUseCase
{
    Task<SanctionDto> ExecuteAsync(int ownerProfileId, LockOwnerRequest request, CancellationToken cancellationToken = default);
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
    private static readonly JsonSerializerOptions AuditJson = new(JsonSerializerDefaults.Web);

    public async Task<SanctionDto> ExecuteAsync(int ownerProfileId, LockOwnerRequest request, CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;

        if (ownerProfileId <= 0) throw new ValidationException("ownerProfileId phải là số nguyên dương.");
        if (request.OwnerUserId <= 0) throw new ValidationException("ownerUserId phải là số nguyên dương.");
        if (request.PerformedByUserId <= 0) throw new ValidationException("performedByUserId phải là số nguyên dương.");
        if (string.IsNullOrWhiteSpace(request.Reason)) throw new ValidationException("Lý do khóa không được để trống.");
        var reason = request.Reason.Trim();
        if (reason.Length > 1000) throw new ValidationException("Lý do khóa tối đa 1000 ký tự.");
        var lockedUntilUtc = OwnerLockTime.ToUtc(request.LockedUntilUtc);
        if (lockedUntilUtc <= now) throw new ValidationException("lockedUntilUtc phải sau thời điểm hiện tại.");

        // Xác minh hồ sơ chủ bãi có thật trước khi ghi – không lưu chế tài cho OwnerProfileId không tồn tại.
        var owner = await userService.FindUserByIdAsync(request.OwnerUserId, cancellationToken)
            ?? throw new NotFoundException("Người dùng", request.OwnerUserId);
        if (owner.OwnerProfile?.Id != ownerProfileId)
            throw new ValidationException($"Người dùng {request.OwnerUserId} không sở hữu hồ sơ chủ bãi {ownerProfileId}.");

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
            sanction = Sanction.CreateOwnerLock(ownerProfileId, reason, request.PerformedByUserId, now, lockedUntilUtc);
            await repository.AddLockAsync(sanction, BuildAuditLog(sanction, request.OwnerUserId), cancellationToken);
        }

        await SyncToUserServiceAsync(sanction, cancellationToken);
        await repository.SaveAsync(cancellationToken);
        return SanctionMapper.ToDto(sanction);
    }

    private async Task SyncToUserServiceAsync(Sanction sanction, CancellationToken cancellationToken)
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
        catch (UserServiceUnavailableException ex)
        {
            logger.LogWarning(ex, "Chưa đồng bộ được lệnh khóa chủ bãi {OwnerProfileId} sang UserService.", sanction.OwnerProfileId);
            sanction.MarkSyncFailed();
        }
    }

    private static AuditLog BuildAuditLog(Sanction sanction, int ownerUserId) => new()
    {
        SourceService = "AdminService",
        UserId = sanction.IssuedByUserId,
        OwnerProfileId = sanction.OwnerProfileId,
        Action = "Owner.Locked",
        EntityName = "OwnerProfile",
        EntityId = sanction.OwnerProfileId.ToString(),
        OldValuesJson = JsonSerializer.Serialize(new { isLocked = false }, AuditJson),
        NewValuesJson = JsonSerializer.Serialize(
            new { isLocked = true, ownerUserId, level = sanction.Level.ToString(), lockedUntilUtc = sanction.EndsAtUtc }, AuditJson),
        Reason = sanction.Reason,
    };
}

internal static class OwnerLockTime
{
    /// <summary>Mốc thời gian nhận từ client luôn được hiểu là UTC (giá trị không ghi múi giờ coi như đã là UTC).</summary>
    public static DateTime? ToUtc(DateTime? value) => value switch
    {
        null => null,
        { Kind: DateTimeKind.Unspecified } v => DateTime.SpecifyKind(v, DateTimeKind.Utc),
        { } v => v.ToUniversalTime(),
    };
}
