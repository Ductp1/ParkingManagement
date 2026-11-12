using ParkingManagement.SharedKernel.Exceptions;
using ParkingService.Application.Abstractions;
using ParkingService.Domain.Entities;

namespace ParkingService.Application.Features.PhysicalChanges;

public interface IRecordPhysicalChangeBookingErrorUseCase
{
    Task<LotPhysicalChangeDto> ExecuteAsync(
        int lotId,
        int changeId,
        RecordPhysicalChangeBookingErrorRequest request,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// USE CASE: Xử lý sự cố khách đặt nhầm ô do quá 48 giờ chưa cập nhật sơ đồ (Tiêu chí 2, US-061).
/// - Điều kiện: Đợt thay đổi vật lý đã quá hạn 48 giờ (Status == Overdue hoặc CompletedLate).
/// - Chế tài tài chính: Chủ bãi chịu 100% chi phí bồi thường (OwnerLiabilityPercent = 100%).
/// - Chế tài vận hành: Tự động ghi nhận vi phạm SLA Cấp 2 (SanctionLevel.RankingPenalty = 2).
/// </summary>
public sealed class RecordPhysicalChangeBookingErrorUseCase(ILotPhysicalChangeRepository repository) : IRecordPhysicalChangeBookingErrorUseCase
{
    public async Task<LotPhysicalChangeDto> ExecuteAsync(
        int lotId,
        int changeId,
        RecordPhysicalChangeBookingErrorRequest request,
        CancellationToken cancellationToken = default)
    {
        var lot = await repository.GetLotByIdAsync(lotId, cancellationToken);
        if (lot == null)
            throw new NotFoundException("Bãi đỗ xe", lotId);

        var change = await repository.GetByIdAsync(changeId, cancellationToken);
        if (change == null || change.ParkingLotId != lotId)
            throw new NotFoundException("Đợt thay đổi vật lý", changeId);

        if (string.IsNullOrWhiteSpace(request.BookingCode))
            throw new ValidationException("Mã booking không được để trống.");

        if (request.CompensationAmount < 0)
            throw new ValidationException("Số tiền bồi thường không hợp lệ.");

        var nowUtc = DateTime.UtcNow;
        change.CheckAndMarkOverdue(nowUtc);

        // TIÊU CHÍ 2: Chỉ áp dụng khi đợt thay đổi vật lý đã quá 48 giờ chưa cập nhật sơ đồ
        if (change.Status != PhysicalChangeStatus.Overdue && change.Status != PhysicalChangeStatus.CompletedLate)
        {
            throw new ConflictException(
                $"Chỉ áp dụng chế tài Tiêu chí 2 (100% bồi thường & vi phạm SLA Cấp 2) khi đợt thay đổi vật lý đã quá 48 giờ chưa cập nhật sơ đồ. Trạng thái hiện tại: {change.Status}.");
        }

        change.RecordBookingIncident(request.BookingCode.Trim().ToUpperInvariant(), request.CompensationAmount);

        await repository.UpdateAsync(change, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);

        return change.ToDto(nowUtc);
    }
}
