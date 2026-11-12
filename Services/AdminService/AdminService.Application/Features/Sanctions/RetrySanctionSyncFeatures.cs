using System.Text.Json;
using AdminService.Application.Features.Owners;
using AdminService.Application.Features.Users;
using AdminService.Domain.Entities;
using Microsoft.Extensions.Logging;
using ParkingManagement.SharedKernel.Enums;
using ParkingManagement.SharedKernel.Exceptions;

namespace AdminService.Application.Features.Sanctions;

public interface IRetrySanctionSyncUseCase
{
    Task<SanctionDetailDto> ExecuteAsync(int sanctionId, int performedByUserId, CancellationToken cancellationToken = default);
}

/// <summary>
/// US-097: POST /api/v1/admin/sanctions/{id}/retry-sync – Admin gửi lại mọi lệnh đang Pending / Failed của chế tài, dùng JWT của chính Admin:
///  - UserService (khóa chủ bãi): Active → khóa; Revoked / Expired → mở khóa (lõi US-096).
///  - ParkingService: Active → áp; Revoked → gỡ.  - Thông báo: theo trạng thái hiện tại (áp / gỡ / hết hạn).
/// Chế tài Active đã quá hạn được đóng trước (như job). Kết quả + audit "Sanction.SyncRetried" ghi trong 1 lần SaveChanges.
/// Không còn gì để đồng bộ → 409.
/// </summary>
public sealed class RetrySanctionSyncUseCase(
    ISanctionRepository repository,
    IUserServiceClient userService,
    IParkingSanctionClient parking,
    INotificationClient notifications,
    ISanctionAdminQueries queries,
    TimeProvider timeProvider,
    ILogger<RetrySanctionSyncUseCase> logger) : IRetrySanctionSyncUseCase
{
    private static readonly JsonSerializerOptions AuditJson = new(JsonSerializerDefaults.Web);
    private readonly OwnerLockCore lockCore = new(userService, timeProvider, logger);
    private readonly SanctionSyncCore sync = new(parking, notifications, timeProvider, logger);

    public async Task<SanctionDetailDto> ExecuteAsync(int sanctionId, int performedByUserId, CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        if (sanctionId <= 0) throw new ValidationException("sanctionId phải là số nguyên dương.");
        if (performedByUserId <= 0) throw new ValidationException("performedByUserId phải là số nguyên dương.");

        var sanction = await repository.FindTrackedAsync(sanctionId, cancellationToken)
            ?? throw new NotFoundException("Chế tài", sanctionId);

        if (sanction.Status == SanctionStatus.Active && !sanction.IsInEffect(now)
            && !await SanctionExpiry.TryExpireAsync(repository, sanction, performedByUserId, cancellationToken))
            throw new ConflictException($"Chế tài #{sanctionId} vừa được hệ thống đóng do hết hạn. Tải lại rồi thử lại.");

        if (!sanction.HasPendingSync)
            throw new ConflictException($"Chế tài #{sanctionId} không có lệnh đồng bộ nào đang chờ hoặc lỗi.");

        var before = Snapshot(sanction);
        if (sanction.IsOwnerLock && Sanction.IsPendingOrFailed(sanction.UserServiceSyncStatus))
        {
            if (sanction.Status == SanctionStatus.Active)
                await lockCore.SyncLockAsync(sanction, cancellationToken);
            else
                await lockCore.SyncUnlockAsync(sanction, sanction.RevokeReason ?? $"Chế tài #{sanction.Id} hết hạn.", performedByUserId,
                    cancellationToken);
        }
        await sync.SyncAsync(sanction, cancellationToken);

        await repository.SaveWithAuditAsync(new AuditLog
        {
            SourceService = "AdminService",
            UserId = performedByUserId,
            OwnerProfileId = sanction.OwnerProfileId,
            Action = "Sanction.SyncRetried",
            EntityName = "Sanction",
            EntityId = sanction.Id.ToString(),
            OldValuesJson = JsonSerializer.Serialize(before, AuditJson),
            NewValuesJson = JsonSerializer.Serialize(Snapshot(sanction), AuditJson),
            Reason = "Admin đồng bộ lại chế tài.",
        }, cancellationToken);

        return await queries.GetDetailAsync(sanctionId, now, cancellationToken) ?? throw new NotFoundException("Chế tài", sanctionId);
    }

    private static object Snapshot(Sanction s) => new
    {
        userService = s.UserServiceSyncStatus.ToString(),
        parking = s.ParkingSyncStatus.ToString(),
        notification = s.NotificationSyncStatus.ToString(),
    };
}
