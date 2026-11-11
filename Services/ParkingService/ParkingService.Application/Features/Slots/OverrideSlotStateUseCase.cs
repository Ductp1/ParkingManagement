using Microsoft.Extensions.Logging;
using ParkingManagement.SharedKernel.Enums;
using ParkingManagement.SharedKernel.Exceptions;
using ParkingService.Application.Abstractions;
using ParkingService.Domain.Entities;

namespace ParkingService.Application.Features.Slots;

/// <summary>
/// Lệnh đổi trạng thái vận hành của 1 slot do nguồn bên ngoài yêu cầu (GateService/Staff/PMS/Sensor).
/// Reason bắt buộc – là bằng chứng khi tranh chấp "bãi báo trống nhưng thực tế có xe".
/// </summary>
public sealed record OverrideSlotStateCommand(
    int SlotId,
    int ParkingLotId,
    SlotState NewState,
    SlotStateSource Source,
    int? ChangedByUserId,
    string Reason,
    int? BookingId = null,
    int? ParkingSessionId = null);

public sealed record SlotStateOverrideResultDto(int SlotId, string SlotCode, string FromState, string ToState,
    string Source, int? ChangedByUserId, DateTime ChangedAtUtc);

public interface IOverrideSlotStateUseCase
{
    Task<SlotStateOverrideResultDto> ExecuteAsync(OverrideSlotStateCommand command, CancellationToken cancellationToken = default);
}

/// <summary>
/// US-049 (UC-25) / US-039 (UC-24): đổi trạng thái slot + ghi SlotStateLog (trước/sau, nguồn, người đổi, lý do).
///  - Slot phải thuộc đúng bãi gửi yêu cầu và đang hoạt động.
///  - Staff ghi đè (StaffOverride) bắt buộc có ChangedByUserId và không được đặt Reserved (Reserved do booking quản lý).
///  - Trạng thái không đổi → trả kết quả hiện tại, không ghi log (idempotent – an toàn khi GateService retry).
///  - RowVersion chặn 2 request cùng đổi 1 slot (adapter ném ConflictException).
/// </summary>
public sealed class OverrideSlotStateUseCase(
    ISlotStateRepository repository,
    TimeProvider timeProvider,
    ILogger<OverrideSlotStateUseCase> logger) : IOverrideSlotStateUseCase
{
    public async Task<SlotStateOverrideResultDto> ExecuteAsync(OverrideSlotStateCommand command, CancellationToken cancellationToken = default)
    {
        Validate(command);

        var found = await repository.GetWithLotAsync(command.SlotId, cancellationToken)
            ?? throw new NotFoundException("Ô đỗ (Slot)", command.SlotId);
        if (found.ParkingLotId != command.ParkingLotId)
            throw new ForbiddenException($"Ô đỗ {found.Slot.Code} không thuộc bãi {command.ParkingLotId}.");

        var slot = found.Slot;
        if (!slot.IsActive)
            throw new ConflictException($"Ô đỗ {slot.Code} đã ngừng hoạt động, không thể đổi trạng thái.");

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var fromState = slot.State;
        if (fromState == command.NewState)
            return ToDto(slot, fromState, command, slot.StateChangedAtUtc ?? now);

        slot.State = command.NewState;
        slot.StateChangedAtUtc = now;
        await repository.AddLogAsync(new SlotStateLog
        {
            SlotId = slot.Id,
            FromState = fromState,
            ToState = command.NewState,
            Source = command.Source,
            BookingId = command.BookingId,
            ParkingSessionId = command.ParkingSessionId,
            ChangedByUserId = command.ChangedByUserId,
            Reason = command.Reason.Trim(),
        }, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Slot {SlotCode} (Id={SlotId}) {From} → {To} bởi {Source} (user {UserId}): {Reason}",
            slot.Code, slot.Id, fromState, command.NewState, command.Source, command.ChangedByUserId, command.Reason);

        return ToDto(slot, fromState, command, now);
    }

    private static void Validate(OverrideSlotStateCommand command)
    {
        if (command.SlotId <= 0) throw new ValidationException("slotId phải là số nguyên dương.");
        if (command.ParkingLotId <= 0) throw new ValidationException("parkingLotId phải là số nguyên dương.");
        if (!Enum.IsDefined(command.NewState)) throw new ValidationException($"Trạng thái slot '{command.NewState}' không hợp lệ.");
        if (!Enum.IsDefined(command.Source)) throw new ValidationException($"Nguồn thay đổi '{command.Source}' không hợp lệ.");
        if (string.IsNullOrWhiteSpace(command.Reason))
            throw new ValidationException("Bắt buộc nhập lý do đổi trạng thái slot.");
        if (command.Reason.Trim().Length > 500)
            throw new ValidationException("Lý do đổi trạng thái slot tối đa 500 ký tự.");

        if (command.Source == SlotStateSource.StaffOverride)
        {
            if (command.ChangedByUserId is null or <= 0)
                throw new ValidationException("Staff ghi đè trạng thái slot phải kèm staffUserId.");
            if (command.NewState == SlotState.Reserved)
                throw new ValidationException("Không thể ghi đè slot sang Reserved – Reserved chỉ do hệ thống booking quản lý.");
        }
    }

    private static SlotStateOverrideResultDto ToDto(Slot slot, SlotState fromState, OverrideSlotStateCommand command, DateTime at)
        => new(slot.Id, slot.Code, fromState.ToString(), slot.State.ToString(), command.Source.ToString(),
            command.ChangedByUserId, at);
}
