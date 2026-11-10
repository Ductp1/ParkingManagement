using Microsoft.Extensions.Logging;
using ParkingManagement.SharedKernel.Enums;
using ParkingManagement.SharedKernel.Exceptions;
using ParkingService.Application.Abstractions;
using ParkingService.Domain.Entities;

namespace ParkingService.Application.Features.Closures;

public interface ICreateClosureScheduleUseCase
{
    Task<ClosureScheduleDto> ExecuteAsync(CreateClosureScheduleCommand command, CancellationToken cancellationToken = default);
}

/// <summary>
/// USE CASE: Tạo lịch tạm đóng một phần hoặc toàn bộ bãi đỗ (Zone / Floor / Slot / Lot) (US-063).
/// - Xác thực target phân cấp thuộc bãi xe.
/// - Ngăn chặn lịch đóng trùng lặp thời gian.
/// - Bảo vệ booking active & xe đang đỗ: Chặn 409 Conflict nếu có xe đỗ (Occupied) hoặc giữ chỗ (Reserved) khi không phải Emergency.
/// - Khi đóng khẩn cấp (IsEmergency = true): Cưỡng chế đóng ngay, ghi nhận slot bị ảnh hưởng để gửi thông báo.
/// - Tự động chuyển trạng thái các ô đỗ trống sang Maintenance, ghi SlotStateLogs và giảm trừ AvailableSlots của bãi.
/// </summary>
public sealed class CreateClosureScheduleUseCase(
    IParkingLotManagementRepository lotRepository,
    IZoneRepository zoneRepository,
    IFloorRepository floorRepository,
    ISlotRepository slotRepository,
    IClosureScheduleRepository closureScheduleRepository,
    ILogger<CreateClosureScheduleUseCase> logger) : ICreateClosureScheduleUseCase
{
    public async Task<ClosureScheduleDto> ExecuteAsync(CreateClosureScheduleCommand command, CancellationToken cancellationToken = default)
    {
        if (command.ParkingLotId <= 0)
            throw new ValidationException("ParkingLotId không hợp lệ.");

        if (command.TargetId <= 0)
            throw new ValidationException("TargetId không hợp lệ.");

        if (string.IsNullOrWhiteSpace(command.Reason))
            throw new ValidationException("Lý do tạm đóng không được để trống.");

        if (command.Reason.Trim().Length > 500)
            throw new ValidationException("Lý do tạm đóng không được vượt quá 500 ký tự.");

        var now = DateTime.UtcNow;
        var startsAtUtc = command.IsEmergency ? now : command.StartsAtUtc;

        if (!command.IsEmergency && startsAtUtc < now.AddMinutes(-5))
            throw new ValidationException("Thời gian bắt đầu tạm đóng không được nằm trong quá khứ.");

        if (command.EndsAtUtc.HasValue && command.EndsAtUtc.Value <= startsAtUtc)
            throw new ValidationException("Thời gian kết thúc phải lớn hơn thời gian bắt đầu.");

        // 1. Kiểm tra tồn tại của bãi đỗ xe
        var lot = await lotRepository.GetByIdAsync(command.ParkingLotId, cancellationToken)
            ?? throw new NotFoundException("Bãi đỗ xe (ParkingLot)", command.ParkingLotId);

        // 2. Xác thực cấu trúc phân cấp và tập hợp các ô đỗ thuộc target
        var (targetName, targetSlots, targetZone, targetFloor) = await ResolveTargetAsync(
            command.ParkingLotId, command.TargetType, command.TargetId, cancellationToken);

        // 3. Kiểm tra trùng lặp lịch đóng
        var hasOverlap = await closureScheduleRepository.HasOverlappingClosureAsync(
            command.ParkingLotId,
            command.TargetType,
            command.TargetId,
            startsAtUtc,
            command.EndsAtUtc,
            excludeId: null,
            cancellationToken);

        if (hasOverlap)
            throw new ConflictException($"Đã tồn tại lịch tạm đóng còn hiệu lực trùng lặp thời gian cho {targetName}.");

        // 4. RÀNG BUỘC AN TOÀN BOOKING ACTIVE & XE ĐANG ĐỖ (US-063)
        var activeSlots = targetSlots
            .Where(s => s.IsActive && (s.State == SlotState.Occupied || s.State == SlotState.Reserved))
            .ToList();

        if (activeSlots.Count > 0 && !command.IsEmergency)
        {
            var sampleCodes = string.Join(", ", activeSlots.Take(5).Select(s => s.Code));
            throw new ConflictException(
                $"Không thể tạm đóng {command.TargetType} '{targetName}': Có {activeSlots.Count} ô đỗ đang có phương tiện đỗ hoặc đang được giữ chỗ ({sampleCodes}). Vui lòng chọn thời gian đóng sau khi các xe đã rời bãi hoặc kích hoạt chế độ khẩn cấp (Emergency).");
        }

        // 5. CẬP NHẬT TRẠNG THÁI NẾU LỊCH ĐÓNG CÓ HIỆU LỰC NGAY LẬP TỨC
        var isEffectiveNow = startsAtUtc <= now && (command.EndsAtUtc == null || command.EndsAtUtc.Value > now);
        var stateLogs = new List<SlotStateLog>();
        var affectedSlotsCount = targetSlots.Count(s => s.IsActive);

        if (isEffectiveNow)
        {
            if (targetZone != null)
            {
                targetZone.IsClosed = true;
                targetZone.ClosedReason = command.Reason;
                await zoneRepository.UpdateAsync(targetZone, cancellationToken);
                await zoneRepository.SaveChangesAsync(cancellationToken);
            }

            if (targetFloor != null)
            {
                targetFloor.IsClosed = true;
                await floorRepository.UpdateAsync(targetFloor, cancellationToken);
                await floorRepository.SaveChangesAsync(cancellationToken);
            }

            // Chuyển các slot Available sang Maintenance
            var availableSlotsToClose = targetSlots
                .Where(s => s.IsActive && s.State == SlotState.Available)
                .ToList();

            foreach (var slot in availableSlotsToClose)
            {
                slot.State = SlotState.Maintenance;
                slot.StateChangedAtUtc = now;
                await slotRepository.UpdateAsync(slot, cancellationToken);

                stateLogs.Add(new SlotStateLog
                {
                    SlotId = slot.Id,
                    FromState = SlotState.Available,
                    ToState = SlotState.Maintenance,
                    Source = command.IsEmergency ? SlotStateSource.System : SlotStateSource.StaffOverride,
                    ChangedByUserId = command.UserId > 0 ? command.UserId : null,
                    Reason = command.Reason
                });
            }

            if (availableSlotsToClose.Count > 0)
            {
                await slotRepository.SaveChangesAsync(cancellationToken);
                await closureScheduleRepository.AddStateLogsAsync(stateLogs, cancellationToken);

                // Cập nhật lại số lượng chỗ trống khả dụng của bãi
                var newAvailableSlots = Math.Max(0, lot.AvailableSlots - availableSlotsToClose.Count);
                lot.SetSlotCounters(lot.TotalSlots, newAvailableSlots);
                await lotRepository.UpdateAsync(lot, cancellationToken);
                await lotRepository.SaveChangesAsync(cancellationToken);
            }

            logger.LogInformation(
                "Đã kích hoạt tạm đóng ngay {TargetType} '{TargetName}' (LotId={LotId}): {ClosedCount} ô đỗ chuyển sang Maintenance",
                command.TargetType, targetName, command.ParkingLotId, availableSlotsToClose.Count);
        }

        // 6. Tạo bản ghi ClosureSchedule
        var schedule = new ClosureSchedule
        {
            ParkingLotId = command.ParkingLotId,
            TargetType = command.TargetType,
            TargetId = command.TargetId,
            StartsAtUtc = startsAtUtc,
            EndsAtUtc = command.EndsAtUtc,
            Reason = command.Reason.Trim(),
            IsEmergency = command.IsEmergency,
            AffectedBookingsNotified = false,
            CreatedByUserId = command.UserId
        };

        await closureScheduleRepository.AddAsync(schedule, cancellationToken);
        await closureScheduleRepository.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Đã tạo ClosureSchedule Id={Id} cho {TargetType} Id={TargetId} của Lot Id={LotId} (Emergency={IsEmergency})",
            schedule.Id, schedule.TargetType, schedule.TargetId, schedule.ParkingLotId, schedule.IsEmergency);

        var status = isEffectiveNow ? "Active" : (startsAtUtc > now ? "Upcoming" : "Past");

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
            status,
            affectedSlotsCount,
            schedule.CreatedAtUtc);
    }

    private async Task<(string targetName, IReadOnlyList<Slot> slots, Zone? zone, Floor? floor)> ResolveTargetAsync(
        int parkingLotId, ClosureTargetType targetType, int targetId, CancellationToken cancellationToken)
    {
        switch (targetType)
        {
            case ClosureTargetType.Slot:
            {
                var slot = await slotRepository.GetByIdAsync(targetId, cancellationToken)
                    ?? throw new NotFoundException("Ô đỗ (Slot)", targetId);

                var floor = await floorRepository.GetByIdAsync(slot.FloorId, cancellationToken)
                    ?? throw new NotFoundException("Tầng (Floor)", slot.FloorId);

                var zone = await zoneRepository.GetByIdAsync(floor.ZoneId, cancellationToken)
                    ?? throw new NotFoundException("Khu vực (Zone)", floor.ZoneId);

                if (zone.ParkingLotId != parkingLotId)
                    throw new ValidationException($"Ô đỗ '{slot.Code}' không thuộc bãi đỗ xe Id={parkingLotId}.");

                return ($"Ô {slot.Code}", [slot], null, null);
            }

            case ClosureTargetType.Floor:
            {
                var floor = await floorRepository.GetByIdAsync(targetId, cancellationToken)
                    ?? throw new NotFoundException("Tầng (Floor)", targetId);

                var zone = await zoneRepository.GetByIdAsync(floor.ZoneId, cancellationToken)
                    ?? throw new NotFoundException("Khu vực (Zone)", floor.ZoneId);

                if (zone.ParkingLotId != parkingLotId)
                    throw new ValidationException($"Tầng '{floor.Name}' không thuộc bãi đỗ xe Id={parkingLotId}.");

                var slots = await slotRepository.GetByFloorIdAsync(targetId, cancellationToken);
                return ($"Tầng {floor.Name}", slots, null, floor);
            }

            case ClosureTargetType.Zone:
            {
                var zone = await zoneRepository.GetByIdAsync(targetId, cancellationToken)
                    ?? throw new NotFoundException("Khu vực (Zone)", targetId);

                if (zone.ParkingLotId != parkingLotId)
                    throw new ValidationException($"Khu vực '{zone.Name}' không thuộc bãi đỗ xe Id={parkingLotId}.");

                var floors = await floorRepository.GetByZoneIdAsync(targetId, cancellationToken);
                var allSlots = new List<Slot>();
                foreach (var f in floors)
                {
                    var floorSlots = await slotRepository.GetByFloorIdAsync(f.Id, cancellationToken);
                    allSlots.AddRange(floorSlots);
                }

                return ($"Khu {zone.Name} ({zone.Code})", allSlots, zone, null);
            }

            case ClosureTargetType.Lot:
            {
                if (targetId != parkingLotId)
                    throw new ValidationException("TargetId của loại đóng Lot phải trùng với ParkingLotId.");

                var lot = await lotRepository.GetByIdAsync(parkingLotId, cancellationToken)
                    ?? throw new NotFoundException("Bãi đỗ xe (ParkingLot)", parkingLotId);

                var zones = await zoneRepository.GetByParkingLotIdAsync(parkingLotId, cancellationToken);
                var allSlots = new List<Slot>();
                foreach (var z in zones)
                {
                    var floors = await floorRepository.GetByZoneIdAsync(z.Id, cancellationToken);
                    foreach (var f in floors)
                    {
                        var floorSlots = await slotRepository.GetByFloorIdAsync(f.Id, cancellationToken);
                        allSlots.AddRange(floorSlots);
                    }
                }

                return ($"Bãi {lot.Name}", allSlots, null, null);
            }

            default:
                throw new ValidationException($"Loại mục tiêu tạm đóng '{targetType}' không được hỗ trợ.");
        }
    }
}
