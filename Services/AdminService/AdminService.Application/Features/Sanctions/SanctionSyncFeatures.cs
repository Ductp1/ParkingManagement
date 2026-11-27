using System.Globalization;
using System.Text.Json;
using AdminService.Domain.Entities;
using AdminService.Domain.Enums;
using Microsoft.Extensions.Logging;
using ParkingManagement.SharedKernel.Enums;
using ParkingManagement.SharedKernel.Exceptions;

namespace AdminService.Application.Features.Sanctions;

// ===== PORT (service khác) =====

/// <summary>Lệnh áp hiệu lực chế tài sang ParkingService (PUT …/parking-lots/sanction-states/{sanctionId}).</summary>
public sealed record ParkingSanctionCommand(int SanctionId, int OwnerProfileId, int? ParkingLotId, string Kind, DateTime StartsAtUtc,
    DateTime? EndsAtUtc);

/// <summary>
/// US-097: ParkingService thi hành hạ thứ hạng / tạm ngừng niêm yết. Cần JWT Admin (chuyển tiếp từ request).
/// Lỗi: DependencyUnavailableException (không gọi được / bị từ chối), AuthenticationException (401), ForbiddenException (403).
/// </summary>
public interface IParkingSanctionClient
{
    Task ApplyAsync(ParkingSanctionCommand command, CancellationToken cancellationToken);
    Task LiftAsync(int sanctionId, CancellationToken cancellationToken);
}

/// <summary>Thông báo in-app cho chủ bãi.</summary>
public sealed record OwnerNotification(int UserId, string TemplateKey, string Title, string Body, string DataJson);

/// <summary>US-097: NotificationService (POST /api/v1/notifications/send, chưa yêu cầu JWT – job gọi được). Lỗi như IParkingSanctionClient.</summary>
public interface INotificationClient
{
    Task SendAsync(OwnerNotification notification, CancellationToken cancellationToken);
}

// ===== LÕI ĐỒNG BỘ =====

/// <summary>
/// US-097: đẩy chế tài đã lưu sang ParkingService và gửi thông báo cho chủ bãi, ghi kết quả (Synced / Failed) lên chế tài.
/// Chỉ xử lý service đang Pending / Failed; lỗi service khác KHÔNG ném ra – chế tài đã lưu vẫn giữ, Admin bấm "đồng bộ lại".
/// Không tự lưu: use case gọi SaveAsync sau khi đồng bộ. UserService (khóa chủ bãi) đi qua OwnerLockCore của US-096.
/// </summary>
internal sealed class SanctionSyncCore(IParkingSanctionClient parking, INotificationClient notifications, TimeProvider timeProvider,
    ILogger logger)
{
    public async Task SyncAsync(Sanction sanction, CancellationToken cancellationToken)
    {
        await SyncParkingAsync(sanction, cancellationToken);
        await NotifyAsync(sanction, cancellationToken);
    }

    /// <summary>Active → áp (PUT), Revoked → gỡ (DELETE), Expired → không cần (ParkingService tự hết hiệu lực theo thời gian).</summary>
    public async Task SyncParkingAsync(Sanction sanction, CancellationToken cancellationToken)
    {
        if (!Sanction.IsPendingOrFailed(sanction.ParkingSyncStatus)) return;
        if (sanction.Status == SanctionStatus.Expired)
        {
            sanction.ParkingSyncStatus = SanctionSyncStatus.NotRequired;
            return;
        }

        try
        {
            if (sanction.Status == SanctionStatus.Revoked)
                await parking.LiftAsync(sanction.Id, cancellationToken);
            else
                await parking.ApplyAsync(ToParkingCommand(sanction), cancellationToken);
            sanction.MarkParkingSynced(timeProvider.GetUtcNow().UtcDateTime);
        }
        catch (Exception ex) when (IsSyncFailure(ex))
        {
            logger.LogWarning(ex, "Chưa đồng bộ được chế tài #{SanctionId} ({Status}) sang ParkingService.", sanction.Id, sanction.Status);
            sanction.MarkParkingSyncFailed();
        }
    }

    public async Task NotifyAsync(Sanction sanction, CancellationToken cancellationToken)
    {
        if (!Sanction.IsPendingOrFailed(sanction.NotificationSyncStatus)) return;
        if (sanction.OwnerUserId is not { } ownerUserId)
        {
            // Chế tài cũ không biết người nhận: không có gì để gửi.
            sanction.NotificationSyncStatus = SanctionSyncStatus.NotRequired;
            return;
        }

        try
        {
            await notifications.SendAsync(SanctionNotifications.Compose(sanction, ownerUserId), cancellationToken);
            sanction.MarkNotificationSent(timeProvider.GetUtcNow().UtcDateTime);
        }
        catch (Exception ex) when (IsSyncFailure(ex))
        {
            logger.LogWarning(ex, "Chưa gửi được thông báo chế tài #{SanctionId} cho user {UserId}.", sanction.Id, ownerUserId);
            sanction.MarkNotificationFailed();
        }
    }

    /// <summary>Hạ thứ hạng (bãi / toàn chủ bãi) và tạm ngừng niêm yết (bãi).</summary>
    public static ParkingSanctionCommand ToParkingCommand(Sanction sanction) => new(sanction.Id, sanction.OwnerProfileId, sanction.ParkingLotId,
        sanction.Level == SanctionLevel.RankingPenalty ? "RankingPenalty" : "ListingSuspension", sanction.StartsAtUtc, sanction.EndsAtUtc);

    private static bool IsSyncFailure(Exception ex)
        => ex is DependencyUnavailableException or AuthenticationException or ForbiddenException;
}

// ===== NỘI DUNG THÔNG BÁO =====

/// <summary>US-097: thông báo tiếng Việt cho chủ bãi – áp (kèm lý do, hạn khắc phục, tiền phạt, hạn khiếu nại), gỡ, hết hạn. Giờ hiển thị ICT.</summary>
public static class SanctionNotifications
{
    public const string IssuedTemplate = "SANCTION_ISSUED";
    public const string RevokedTemplate = "SANCTION_REVOKED";
    public const string ExpiredTemplate = "SANCTION_EXPIRED";

    private static readonly CultureInfo Vietnamese = CultureInfo.GetCultureInfo("vi-VN");
    private static readonly JsonSerializerOptions DataJson = new(JsonSerializerDefaults.Web);

    public static OwnerNotification Compose(Sanction sanction, int ownerUserId)
    {
        var label = SanctionRules.LevelLabel(sanction.Level);
        var scope = Scope(sanction);
        var (template, title, lines) = sanction.Status switch
        {
            SanctionStatus.Revoked => (RevokedTemplate, $"Đã gỡ chế tài #{sanction.Id}",
                new List<string> { $"Chế tài \"{label}\" áp lên {scope} đã được gỡ.", $"Lý do gỡ: {sanction.RevokeReason}" }),
            SanctionStatus.Expired => (ExpiredTemplate, $"Chế tài #{sanction.Id} đã hết hiệu lực", ExpiredLines(sanction, label, scope)),
            _ => (IssuedTemplate, $"Chế tài SLA: {label} – {scope}", IssuedLines(sanction)),
        };

        var data = JsonSerializer.Serialize(new
        {
            sanctionId = sanction.Id,
            level = sanction.Level.ToString(),
            parkingLotId = sanction.ParkingLotId,
            status = sanction.Status.ToString(),
        }, DataJson);
        return new OwnerNotification(ownerUserId, template, title, string.Join("\n", lines), data);
    }

    private static List<string> IssuedLines(Sanction sanction)
    {
        var lines = new List<string> { $"Lý do: {sanction.Reason}" };
        switch (sanction.Level)
        {
            case SanctionLevel.Warning:
                lines.Add($"Hạn khắc phục: {Ict(sanction.CorrectionDueAtUtc)}.");
                break;
            case SanctionLevel.RankingPenalty:
                lines.Add($"Bãi bị hạ thứ hạng trong kết quả tìm kiếm đến {Ict(sanction.EndsAtUtc)}.");
                break;
            case SanctionLevel.TemporarySuspension when sanction.ParkingLotId is not null:
                lines.Add($"Bãi tạm ngừng nhận đặt chỗ mới đến {Ict(sanction.EndsAtUtc)}; lượt đặt đã xác nhận vẫn được phục vụ.");
                break;
            case SanctionLevel.TemporarySuspension:
                lines.Add($"Tài khoản chủ bãi bị tạm khóa đến {Ict(sanction.EndsAtUtc)}.");
                break;
            case SanctionLevel.PermanentBan:
                lines.Add("Chấm dứt hợp tác: tài khoản chủ bãi bị khóa vĩnh viễn.");
                break;
        }
        if (sanction.PenaltyAmount is { } penalty and > 0)
            lines.Add($"Tiền phạt: {penalty.ToString("N0", Vietnamese)} đ (cấn trừ vào kỳ quyết toán).");
        if (sanction.AppealDeadlineUtc is { } appeal)
            lines.Add($"Bạn có thể khiếu nại đến {Ict(appeal)}.");
        return lines;
    }

    private static List<string> ExpiredLines(Sanction sanction, string label, string scope)
    {
        var lines = new List<string> { $"Chế tài \"{label}\" áp lên {scope} đã hết hạn lúc {Ict(sanction.EndsAtUtc)}." };
        if (sanction.IsOwnerLock) lines.Add("Tài khoản sẽ được mở khóa sau khi quản trị viên xác nhận.");
        return lines;
    }

    private static string Scope(Sanction sanction) => sanction.ParkingLotId is { } lotId
        ? $"bãi #{lotId}"
        : sanction.IsOwnerLock ? "tài khoản chủ bãi" : "toàn bộ bãi của bạn";

    /// <summary>Giờ Việt Nam (UTC+7, không DST).</summary>
    private static string Ict(DateTime? utc)
        => utc is { } value ? value.AddHours(7).ToString("HH:mm dd/MM/yyyy", CultureInfo.InvariantCulture) : "không thời hạn";
}
