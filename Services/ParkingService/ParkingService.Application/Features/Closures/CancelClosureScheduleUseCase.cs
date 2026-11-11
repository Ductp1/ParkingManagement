using Microsoft.Extensions.Logging;
using ParkingManagement.SharedKernel.Enums;
using ParkingManagement.SharedKernel.Exceptions;
using ParkingService.Application.Abstractions;
using ParkingService.Domain.Entities;

namespace ParkingService.Application.Features.Closures;

public interface ICancelClosureScheduleUseCase
{
    Task<ClosureScheduleDto> ExecuteAsync(CancelClosureScheduleCommand command, CancellationToken cancellationToken = default);
}

/// <summary>
/// USE CASE: Hủy lịch tạm đóng / Mở lại một phần bãi trước hạn (US-063).
/// - Đánh dấu CancelledAtUtc cho ClosureSchedule.
/// - Nếu lịch đóng này đang có hiệu lực:
///   + Mở lại Zone (IsClosed = false) và Floor (IsClosed = false).
///   + Chuyển các ô đỗ đang ở Maintenance về Available.
///   + Ghi nhận SlotStateLogs.
///   + Tăng lại số chỗ khả dụng AvailableSlots của bãi.
/// </summary>
public sealed class CancelClosureScheduleUseCase(
    IParkingLotManagementRepository lotRepository,
    IZoneRepository zoneRepository,
    IFloorRepository floorRepository,
    ISlotRepository slotRepository,
    IClosureScheduleRepository closureScheduleRepository,
    ILogger<CancelClosureScheduleUseCase> logger) : ICancelClosureScheduleUseCase
{
    public async Task<ClosureScheduleDto> ExecuteAsync(CancelClosureScheduleCommand command, CancellationToken cancellationToken = default)
    {
        if (command.ParkingLotId <= 0)
            throw new ValidationException("ParkingLotId không hợp lệ.");

        if (command.ClosureScheduleId <= 0)
            throw new ValidationException("ClosureScheduleId không hợp lệ.");

        var lot = await lotRepository.GetByIdAsync(command.ParkingLotId, cancellationToken)
            ?? throw new NotFoundException("Bãi đỗ xe (ParkingLot)", command.ParkingLotId);

        var schedule = await closureScheduleRepository.GetByIdAsync(command.ClosureScheduleId, cancellationToken)
            ?? throw new NotFoundException("Lịch tạm đóng (ClosureSchedule)", command.ClosureScheduleId);

        if (schedule.ParkingLotId != command.ParkingLotId)
            throw new ValidationException($"Lịch tạm đóng Id={schedule.Id} không thuộc bãi xe Id={command.ParkingLotId}.");

        if (schedule.CancelledAtUtc.HasValue)
            throw new ConflictException("Lịch tạm đóng này đã được hủy bỏ trước đó.");

        var now = DateTime.UtcNow;
        schedule.CancelledAtUtc = now;
        await closureScheduleRepository.UpdateAsync(schedule, cancellationToken);

        // Kiểm tra xem lịch đóng có đang trong khoảng thời gian hiệu lực không
        var wasActive = schedule.StartsAtUtc <= now && (schedule.EndsAtUtc == null || schedule.EndsAtUtc.Value > now);

        var (targetName, targetSlots, targetZone, targetFloor) = await ResolveTargetAsync(
            command.ParkingLotId, schedule.TargetType, schedule.TargetId, cancellationToken);

        var restoredSlotsCount = 0;

        if (wasActive)
        {
            // Kiểm tra xem còn lịch đóng active nào khác trên cùng target không
            var otherActiveClosures = await closureScheduleRepository.HasOverlappingClosureAsync(
                command.ParkingLotId,
                schedule.TargetType,
                schedule.TargetId,
                now,
                now.AddSeconds(1),
                excludeId: schedule.Id,
                cancellationToken);

            if (!otherActiveClosures)
            {
                if (targetZone != null && targetZone.IsClosed)
                {
                    targetZone.IsClosed = false;
                    targetZone.ClosedReason = null;
                    await zoneRepository.UpdateAsync(targetZone, cancellationToken);
                    await zoneRepository.SaveChangesAsync(cancellationToken);
                }

                if (targetFloor != null && targetFloor.IsClosed)
                {
                    targetFloor.IsClosed = false;
                    await floorRepository.UpdateAsync(targetFloor, cancellationToken);
                    await floorRepository.SaveChangesAsync(cancellationToken);
                }

                var stateLogs = new List<SlotStateLog>();
                var slotsToRestore = targetSlots
                    .Where(s => s.IsActive && s.State == SlotState.Maintenance)
                    .ToList();

                foreach (var slot in slotsToRestore)
                {
                    slot.State = SlotState.Available;
                    slot.StateChangedAtUtc = now;
                    await slotRepository.UpdateAsync(slot, cancellationToken);

                    stateLogs.Add(new SlotStateLog
                    {
                        SlotId = slot.Id,
                        FromState = SlotState.Maintenance,
                        ToState = SlotState.Available,
                        Source = SlotStateSource.StaffOverride,
                        ChangedByUserId = command.UserId > 0 ? command.UserId : null,
                        Reason = string.IsNullOrWhiteSpace(command.Reason)
                            ? $"Mở lại bãi trước hạn (Hủy lịch đóng #{schedule.Id})"
                            : command.Reason
                    });
                }

                if (slotsToRestore.Count > 0)
                {
                    await slotRepository.SaveChangesAsync(cancellationToken);
                    await closureScheduleRepository.AddStateLogsAsync(stateLogs, cancellationToken);

                    restoredSlotsCount = slotsToRestore.Count;
                    var newAvailable = Math.Min(lot.TotalSlots, lot.AvailableSlots + restoredSlotsCount);
                    lot.SetSlotCounters(lot.TotalSlots, newAvailable);
                    await lotRepository.UpdateAsync(lot, cancellationToken);
                    await lotRepository.SaveChangesAsync(cancellationToken);
                }

                logger.LogInformation(
                    "Đã hoàn nguyên mở lại {TargetType} '{TargetName}': {RestoredCount} ô đỗ khôi phục Available",
                    schedule.TargetType, targetName, restoredSlotsCount);
            }
        }

        await closureScheduleRepository.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Đã hủy ClosureSchedule Id={Id} cho bãi Id={LotId}", schedule.Id, command.ParkingLotId);

        return new ClosureScheduleDto(
            schedule.Id,
            schedule.ParkingLotId,
            schedule.TargetType.ToString(),
            schedule.TargetId,
            targetName,
            schedule.StartsAtUtc,
            schedule.EndsAtUtc,
            schedule.Reason,
            schedule.IsEmergency,
            schedule.AffectedBookingsNotified,
            schedule.CreatedByUserId,
            schedule.CancelledAtUtc,
            "Cancelled",
            targetSlots.Count(s => s.IsActive),
            schedule.CreatedAtUtc);
    }

    private async Task<(string targetName, IReadOnlyList<Slot> slots, Zone? zone, Floor? floor)> ResolveTargetAsync(
        int parkingLotId, ClosureTargetType targetType, int targetId, CancellationToken cancellationToken)
    {
        switch (targetType)
        {
            case ClosureTargetType.Slot:
            {
                var slot = await slotRepository.GetByIdAsync(targetId, cancellationToken);
                return (slot != null ? $"Ô {slot.Code}" : $"Slot #{targetId}", slot != null ? [slot] : [], null, null);
            }

            case ClosureTargetType.Floor:
            {
                var floor = await floorRepository.GetByIdAsync(targetId, cancellationToken);
                var slots = floor != null ? await slotRepository.GetByFloorIdAsync(targetId, cancellationToken) : [];
                return (floor != null ? $"Tầng {floor.Name}" : $"Floor #{targetId}", slots, null, floor);
            }

            case ClosureTargetType.Zone:
            {
                var zone = await zoneRepository.GetByIdAsync(targetId, cancellationToken);
                var allSlots = new List<Slot>();
                if (zone != null)
                {
                    var floors = await floorRepository.GetByZoneIdAsync(targetId, cancellationToken);
                    foreach (var f in floors)
                    {
                        var floorSlots = await slotRepository.GetByFloorIdAsync(f.Id, cancellationToken);
                        allSlots.AddRange(floorSlots);
                    }
                }
                return (zone != null ? $"Khu {zone.Name} ({zone.Code})" : $"Zone #{targetId}", allSlots, zone, null);
            }

            case ClosureTargetType.Lot:
            {
                var lot = await lotRepository.GetByIdAsync(parkingLotId, cancellationToken);
                var allSlots = new List<Slot>();
                if (lot != null)
                {
                    var zones = await zoneRepository.GetByParkingLotIdAsync(parkingLotId, cancellationToken);
                    foreach (var z in zones)
                    {
                        var floors = await floorRepository.GetByZoneIdAsync(z.Id, cancellationToken);
                        foreach (var f in floors)
                        {
                            var floorSlots = await slotRepository.GetByFloorIdAsync(f.Id, cancellationToken);
                            allSlots.AddRange(floorSlots);
                        }
                    }
                }
                return (lot != null ? $"Bãi {lot.Name}" : $"Lot #{parkingLotId}", allSlots, null, null);
            }

            default:
                return ($"Target #{targetId}", [], null, null);
        }
    }
}
