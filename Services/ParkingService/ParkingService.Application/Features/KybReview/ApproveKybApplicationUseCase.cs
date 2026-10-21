using ParkingManagement.SharedKernel.Enums;
using ParkingManagement.SharedKernel.Exceptions;
using ParkingService.Application.Abstractions;

namespace ParkingService.Application.Features.KybReview;

public interface IApproveKybApplicationUseCase
{
    Task<AdminKybApplicationDetailDto> ExecuteAsync(int id, ApproveKybApplicationRequest request, int performedByUserId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// US-055 AC2 + US-116 (design v9): duyệt KYB = hợp đồng bãi đã ký. AdminService gọi khi Admin tải bản hợp đồng đã ký lên:
/// UnderReview → Approved, bãi PendingApproval → Active; hồ sơ + bãi + AuditRecorded (lý do = số hợp đồng + ghi chú)
/// + ParkingLotApproved lưu trong một SaveChanges.
/// Idempotent: đã Approved và bãi đã Active → trả chi tiết, không ghi gì (AdminService thử lại sau lỗi vẫn an toàn);
/// đã Approved mà bãi còn chờ (dữ liệu từ design v8) → kích hoạt bãi.
/// </summary>
public sealed class ApproveKybApplicationUseCase(IKybReviewRepository repository, IAdminKybQueries queries, TimeProvider timeProvider)
    : IApproveKybApplicationUseCase
{
    public async Task<AdminKybApplicationDetailDto> ExecuteAsync(int id, ApproveKybApplicationRequest request, int performedByUserId,
        CancellationToken cancellationToken = default)
    {
        KybReviewSupport.EnsureIds(id, performedByUserId);
        var contractNumber = request.ContractNumber?.Trim();
        if (string.IsNullOrEmpty(contractNumber))
            throw new ValidationException("Số hợp đồng không được để trống – chỉ duyệt KYB khi hợp đồng bãi đã ký.");
        if (contractNumber.Length > 30) throw new ValidationException("Số hợp đồng tối đa 30 ký tự.");

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var app = await KybReviewSupport.LoadAsync(repository, id, cancellationToken);
        var activeSlots = await repository.CountActiveSlotsAsync(app.ParkingLotId, cancellationToken);
        var before = KybReviewEvents.Snapshot(app);

        if (app.Status == KybStatus.Approved)
        {
            if (!app.ActivateLot(app.ParkingLot, activeSlots))
                return await KybReviewSupport.ReloadAsync(queries, id, cancellationToken);
            await repository.SaveWithEventsAsync(
            [
                KybReviewEvents.Audit(app, "LotActivated", performedByUserId, before, contractNumber, now),
                KybReviewEvents.LotApproved(app.ParkingLot, now),
            ], cancellationToken);
            return await KybReviewSupport.ReloadAsync(queries, id, cancellationToken);
        }

        app.Approve(performedByUserId, app.ParkingLot, activeSlots, request.Note, now);
        var reason = app.ReviewNote is null ? contractNumber : $"{contractNumber} – {app.ReviewNote}";
        await repository.SaveWithEventsAsync(
        [
            KybReviewEvents.Audit(app, "Approved", performedByUserId, before, reason, now),
            KybReviewEvents.LotApproved(app.ParkingLot, now),
        ], cancellationToken);
        return await KybReviewSupport.ReloadAsync(queries, id, cancellationToken);
    }
}
