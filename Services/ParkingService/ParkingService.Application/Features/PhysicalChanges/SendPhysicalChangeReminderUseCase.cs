using ParkingManagement.SharedKernel.Exceptions;
using ParkingService.Application.Abstractions;
using ParkingService.Domain.Entities;

namespace ParkingService.Application.Features.PhysicalChanges;

public interface ISendPhysicalChangeReminderUseCase
{
    Task<PhysicalChangeReminderResultDto> ExecuteAsync(
        int lotId,
        int changeId,
        int? ownerProfileId = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// USE CASE: Gửi cảnh báo nhắc nhở chủ bãi cập nhật sơ đồ trước khi hết hạn 48 giờ (Tiêu chí 1, US-061).
/// </summary>
public sealed class SendPhysicalChangeReminderUseCase(ILotPhysicalChangeRepository repository) : ISendPhysicalChangeReminderUseCase
{
    public async Task<PhysicalChangeReminderResultDto> ExecuteAsync(
        int lotId,
        int changeId,
        int? ownerProfileId = null,
        CancellationToken cancellationToken = default)
    {
        var lot = await repository.GetLotByIdAsync(lotId, cancellationToken);
        if (lot == null)
            throw new NotFoundException("Bãi đỗ xe", lotId);

        if (ownerProfileId.HasValue && ownerProfileId.Value > 0 && lot.OwnerProfileId != ownerProfileId.Value)
            throw new ForbiddenException("Bạn không có quyền thao tác trên bãi xe này.");

        var change = await repository.GetByIdAsync(changeId, cancellationToken);
        if (change == null || change.ParkingLotId != lotId)
            throw new NotFoundException("Đợt thay đổi vật lý", changeId);

        if (change.Status is PhysicalChangeStatus.Completed
            or PhysicalChangeStatus.CompletedLate
            or PhysicalChangeStatus.Cancelled)
        {
            throw new ConflictException($"Không thể gửi nhắc nhở cho đợt thay đổi đã ở trạng thái {change.Status}.");
        }

        var nowUtc = DateTime.UtcNow;
        change.CheckAndMarkOverdue(nowUtc);
        change.RecordReminder(nowUtc);

        await repository.UpdateAsync(change, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);

        var remainingHours = Math.Max(0, Math.Round((change.DeadlineUtc - nowUtc).TotalHours, 1));

        string message = change.Status == PhysicalChangeStatus.Overdue
            ? $"[CẢNH BÁO KHẨN CẤP] Bãi xe {lot.Name} có thay đổi vật lý đã QUÁ HẠN 48 giờ chưa cập nhật sơ đồ (hạn chót: {change.DeadlineUtc:dd/MM/yyyy HH:mm}). Khách đặt nhầm sẽ bị áp chế tài SLA Cấp 2 và chủ bãi chịu 100% bồi thường!"
            : $"[NHẮC NHỞ SLA 48H] Bãi xe {lot.Name} có thay đổi vật lý tại {change.Scope}. Thời gian còn lại để cập nhật sơ đồ: {remainingHours:F1} giờ (hạn chót: {change.DeadlineUtc:dd/MM/yyyy HH:mm}). Vui lòng xuất bản sơ đồ mới đúng hạn để tránh rủi ro khách đặt nhầm.";

        return new PhysicalChangeReminderResultDto(
            change.Id,
            lotId,
            change.ReminderCount,
            nowUtc,
            remainingHours,
            message);
    }
}
