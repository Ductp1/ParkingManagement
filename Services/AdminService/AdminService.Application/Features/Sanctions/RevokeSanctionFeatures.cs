using System.Text.Json;
using AdminService.Application.Features.Owners;
using AdminService.Domain.Entities;
using Microsoft.Extensions.Logging;
using ParkingManagement.SharedKernel.Enums;
using ParkingManagement.SharedKernel.Exceptions;

namespace AdminService.Application.Features.Sanctions;

public interface IRevokeSanctionUseCase
{
    Task<SanctionDetailDto> ExecuteAsync(int sanctionId, RevokeSanctionRequest request, int performedByUserId, CancellationToken cancellationToken = default);
}

/// <summary>
/// US-097: Admin gỡ chế tài còn hiệu lực, bắt buộc có lý do.
///  - Khóa chủ bãi (cấp 3 toàn chủ bãi / cấp 4): đi qua luồng mở khóa US-096 (IUnlockOwnerUseCase) – cùng audit "Owner.Unlocked"
///    và đồng bộ UserService.
///  - Còn lại: Revoked + ghi người / lúc / lý do + audit "Sanction.Revoked" trong 1 lần SaveChanges; service đã nhận lệnh áp
///    (ParkingService / thông báo) về chờ đồng bộ lệnh gỡ.
/// Sau khi lưu: gỡ bên ParkingService + báo chủ bãi, ghi kết quả (Failed → Admin đồng bộ lại).
/// Chế tài đã gỡ / hết hạn → 409.
/// </summary>
public sealed class RevokeSanctionUseCase(ISanctionRepository repository, IUnlockOwnerUseCase unlockOwner, ISanctionAdminQueries queries,
    IParkingSanctionClient parking, INotificationClient notifications, TimeProvider timeProvider, ILogger<RevokeSanctionUseCase> logger)
    : IRevokeSanctionUseCase
{
    private static readonly JsonSerializerOptions AuditJson = new(JsonSerializerDefaults.Web);
    private readonly SanctionSyncCore sync = new(parking, notifications, timeProvider, logger);

    public async Task<SanctionDetailDto> ExecuteAsync(int sanctionId, RevokeSanctionRequest request, int performedByUserId,
        CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        if (sanctionId <= 0) throw new ValidationException("sanctionId phải là số nguyên dương.");
        if (performedByUserId <= 0) throw new ValidationException("performedByUserId phải là số nguyên dương.");
        var reason = SanctionRules.RequireReason(request.Reason, "gỡ chế tài");

        var sanction = await repository.FindTrackedAsync(sanctionId, cancellationToken)
            ?? throw new NotFoundException("Chế tài", sanctionId);
        if (sanction.Status == SanctionStatus.Revoked) throw new ConflictException($"Chế tài #{sanctionId} đã được gỡ.");
        if (!sanction.IsInEffect(now)) throw new ConflictException($"Chế tài #{sanctionId} đã hết hạn, không cần gỡ.");

        if (sanction.IsOwnerLock)
        {
            // Unique index: chủ bãi chỉ có 1 khóa Active → luồng mở khóa US-096 gỡ đúng chế tài này.
            await unlockOwner.ExecuteAsync(sanction.OwnerProfileId, new UnlockOwnerRequest(reason), performedByUserId, cancellationToken);
        }
        else
        {
            var auditLog = new AuditLog
            {
                SourceService = "AdminService",
                UserId = performedByUserId,
                OwnerProfileId = sanction.OwnerProfileId,
                Action = "Sanction.Revoked",
                EntityName = "Sanction",
                EntityId = sanction.Id.ToString(),
                OldValuesJson = JsonSerializer.Serialize(
                    new { status = sanction.Status.ToString(), level = sanction.Level.ToString(), parkingLotId = sanction.ParkingLotId,
                        endsAtUtc = sanction.EndsAtUtc }, AuditJson),
                NewValuesJson = JsonSerializer.Serialize(new { status = nameof(SanctionStatus.Revoked) }, AuditJson),
                Reason = reason,
            };
            sanction.Revoke(reason, performedByUserId, now);
            await repository.SaveWithAuditAsync(auditLog, cancellationToken);
        }

        // Khóa chủ bãi: luồng US-096 đã mở khóa bên UserService trên cùng entity đang tracking; ở đây chỉ còn ParkingService / thông báo.
        await sync.SyncAsync(sanction, cancellationToken);
        await repository.SaveAsync(cancellationToken);

        return await queries.GetDetailAsync(sanctionId, now, cancellationToken) ?? throw new NotFoundException("Chế tài", sanctionId);
    }
}
