using GateService.Application.Features.GateEvents;
using ParkingManagement.SharedKernel.Enums;
using ParkingManagement.SharedKernel.Exceptions;
using ParkingManagement.SharedKernel.Rules;

namespace GateService.Application.Features.GateConsole;

// ===== US-049 (UC-25): STAFF GHI ĐÈ TRẠNG THÁI SLOT KHI CẢM BIẾN HỎNG =====
public sealed record OverrideSlotStatusCommand(int ParkingLotId, int SlotId, SlotState NewState, int StaffUserId, string Reason);

public sealed record OverrideSlotStatusResultDto(int SlotId, string SlotCode, string FromState, string ToState,
    string Source, int StaffUserId, DateTime ChangedAtUtc, string AuditNote);

public interface IOverrideSlotStatusUseCase
{
    Task<OverrideSlotStatusResultDto> ExecuteAsync(OverrideSlotStatusCommand command, CancellationToken cancellationToken = default);
}

/// <summary>
/// US-049 / UC-25: Staff ghi đè trạng thái slot trên sơ đồ 2D khi cảm biến hỏng hoặc sai lệch thực tế.
///  - Trạng thái mới: Trống (Available) / Đang đỗ (Occupied) / Bảo trì (Maintenance) + lý do bắt buộc.
///  - Integrate with ParkingService: GateService gọi ParkingService (chủ sở hữu dữ liệu Slot) qua
///    IParkingSlotClient – ParkingService cập nhật Slot.State + SlotStateLog (Source=StaffOverride,
///    ChangedByUserId, Reason) và đẩy availability real-time cho Driver App / Provider Dashboard.
///  - Record override reason and staff information: GateService ghi thêm GateEvents (ManualSlotOverride).
/// UC-25 E1: slot đang có booking Confirmed → ParkingService trả cảnh báo trong kết quả (staff/chủ bãi xử lý chuyển booking).
/// </summary>
public sealed class OverrideSlotStatusUseCase(
    IParkingSlotClient slotClient,
    IGateEventWriter gateEvents,
    TimeProvider timeProvider) : IOverrideSlotStatusUseCase
{
    public async Task<OverrideSlotStatusResultDto> ExecuteAsync(OverrideSlotStatusCommand command, CancellationToken cancellationToken = default)
    {
        if (command.ParkingLotId <= 0) throw new ValidationException("parkingLotId phải là số nguyên dương.");
        if (command.SlotId <= 0) throw new ValidationException("slotId phải là số nguyên dương.");
        if (command.StaffUserId <= 0) throw new ValidationException("staffUserId phải là số nguyên dương.");
        if (string.IsNullOrWhiteSpace(command.Reason))
            throw new ValidationException("Bắt buộc nhập lý do ghi đè trạng thái slot (cảm biến hỏng / sai lệch thực tế).");
        if (command.NewState is SlotState.Reserved)
            throw new ValidationException("Không thể ghi đè slot sang Reserved – Reserved chỉ do hệ thống booking quản lý.");

        // Gọi ParkingService – nguồn dữ liệu slot duy nhất (Slot.State + SlotStateLog trước/sau).
        var result = await slotClient.OverrideSlotStateAsync(command.ParkingLotId, command.SlotId, command.NewState,
            SlotStateSource.StaffOverride, command.StaffUserId, command.Reason.Trim(), cancellationToken);

        // Audit tại cổng: ghi rõ ai đổi, từ trạng thái nào sang trạng thái nào, lý do gì.
        var note = $"Staff ghi đè slot {result.SlotCode}: {result.FromState} → {result.ToState}. Lý do: {command.Reason.Trim()}";
        await gateEvents.WriteAsync(new GateEventDraft(command.ParkingLotId, GateEventType.ManualSlotOverride,
            PlateNumber: null, PerformedByUserId: command.StaffUserId, Note: note), cancellationToken);

        return new OverrideSlotStatusResultDto(result.SlotId, result.SlotCode, result.FromState, result.ToState,
            result.Source, command.StaffUserId, result.ChangedAtUtc, note);
    }
}