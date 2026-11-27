using Microsoft.Extensions.Logging;
using ParkingManagement.SharedKernel.Enums;
using ParkingManagement.SharedKernel.Exceptions;
using ParkingService.Application.Abstractions;
using ParkingService.Domain.Entities;

namespace ParkingService.Application.Features.Closures;

public interface IValidateClosureImpactUseCase
{
    Task<ClosureImpactCheckResultDto> ExecuteAsync(ClosureImpactCheckCommand command, CancellationToken cancellationToken = default);
}

/// <summary>
/// USE CASE: Pre-flight check kiểm tra trước tác động an toàn của lịch đóng bãi dự kiến (US-063).
/// Giúp Client hiển thị cảnh báo trực quan trước khi người dùng nhấn xác nhận tạm đóng.
/// </summary>
public sealed class ValidateClosureImpactUseCase(
    IParkingLotManagementRepository lotRepository,
    IZoneRepository zoneRepository,
    IFloorRepository floorRepository,
    ISlotRepository slotRepository,
    IClosureScheduleRepository closureScheduleRepository,
    ILogger<ValidateClosureImpactUseCase> logger) : IValidateClosureImpactUseCase
{
    public async Task<ClosureImpactCheckResultDto> ExecuteAsync(ClosureImpactCheckCommand command, CancellationToken cancellationToken = default)
    {
        if (command.ParkingLotId <= 0)
            throw new ValidationException("ParkingLotId không hợp lệ.");

        if (command.TargetId <= 0)
            throw new ValidationException("TargetId không hợp lệ.");

        var now = DateTime.UtcNow;
        var startsAtUtc = command.IsEmergency ? now : command.StartsAtUtc;

        if (command.EndsAtUtc.HasValue && command.EndsAtUtc.Value <= startsAtUtc)
            throw new ValidationException("Thời gian kết thúc phải lớn hơn thời gian bắt đầu.");

        _ = await lotRepository.GetByIdAsync(command.ParkingLotId, cancellationToken)
            ?? throw new NotFoundException("Bãi đỗ xe (ParkingLot)", command.ParkingLotId);

        var (targetName, targetSlots) = await ResolveSlotsAsync(
            command.ParkingLotId, command.TargetType, command.TargetId, cancellationToken);

        var activeSlots = targetSlots.Where(s => s.IsActive).ToList();
        var totalSlots = activeSlots.Count;
        var availableSlots = activeSlots.Count(s => s.State == SlotState.Available);
        var occupiedSlots = activeSlots.Count(s => s.State == SlotState.Occupied);
        var reservedSlots = activeSlots.Count(s => s.State == SlotState.Reserved);

        var atRiskSlots = activeSlots
            .Where(s => s.State == SlotState.Occupied || s.State == SlotState.Reserved)
            .Select(s => s.Code)
            .ToList();

        var blockers = new List<string>();
        var warnings = new List<string>();

        // Kiểm tra trùng lặp lịch đóng
        var hasOverlap = await closureScheduleRepository.HasOverlappingClosureAsync(
            command.ParkingLotId,
            command.TargetType,
            command.TargetId,
            startsAtUtc,
            command.EndsAtUtc,
            excludeId: null,
            cancellationToken);

        if (hasOverlap)
        {
            blockers.Add($"Đã có lịch tạm đóng còn hiệu lực trùng lặp thời gian trên {targetName}.");
        }

        // Đánh giá mức độ an toàn
        var canSafelyClose = true;

        if (atRiskSlots.Count > 0)
        {
            if (!command.IsEmergency)
            {
                canSafelyClose = false;
                blockers.Add($"Có {atRiskSlots.Count} ô đỗ đang có phương tiện đỗ ({occupiedSlots}) hoặc đang được giữ chỗ ({reservedSlots}). Không thể đóng ở chế độ thông thường.");
            }
            else
            {
                warnings.Add($"Cảnh báo khẩn cấp: {atRiskSlots.Count} phương tiện/booking sẽ bị ảnh hưởng trực tiếp khi kích hoạt đóng khẩn cấp.");
            }
        }

        if (blockers.Count > 0)
        {
            canSafelyClose = false;
        }

        if (availableSlots > 0)
        {
            warnings.Add($"Có {availableSlots} ô đỗ trống sẽ chuyển sang trạng thái Bảo trì (Maintenance) và tạm thời không thể đặt chỗ.");
        }

        logger.LogInformation(
            "Kiểm tra tác động đóng {TargetType} '{TargetName}': Total={Total}, Avail={Avail}, Occ={Occ}, Res={Res}, CanClose={CanClose}",
            command.TargetType, targetName, totalSlots, availableSlots, occupiedSlots, reservedSlots, canSafelyClose);

        return new ClosureImpactCheckResultDto(
            canSafelyClose,
            totalSlots,
            availableSlots,
            occupiedSlots,
            reservedSlots,
            atRiskSlots,
            blockers,
            warnings);
    }

    private async Task<(string targetName, IReadOnlyList<Slot> slots)> ResolveSlotsAsync(
        int parkingLotId, ClosureTargetType targetType, int targetId, CancellationToken cancellationToken)
    {
        switch (targetType)
        {
            case ClosureTargetType.Slot:
            {
                var slot = await slotRepository.GetByIdAsync(targetId, cancellationToken)
                    ?? throw new NotFoundException("Ô đỗ (Slot)", targetId);
                return ($"Ô {slot.Code}", [slot]);
            }

            case ClosureTargetType.Floor:
            {
                var floor = await floorRepository.GetByIdAsync(targetId, cancellationToken)
                    ?? throw new NotFoundException("Tầng (Floor)", targetId);
                var slots = await slotRepository.GetByFloorIdAsync(targetId, cancellationToken);
                return ($"Tầng {floor.Name}", slots);
            }

            case ClosureTargetType.Zone:
            {
                var zone = await zoneRepository.GetByIdAsync(targetId, cancellationToken)
                    ?? throw new NotFoundException("Khu vực (Zone)", targetId);
                var floors = await floorRepository.GetByZoneIdAsync(targetId, cancellationToken);
                var allSlots = new List<Slot>();
                foreach (var f in floors)
                {
                    var floorSlots = await slotRepository.GetByFloorIdAsync(f.Id, cancellationToken);
                    allSlots.AddRange(floorSlots);
                }
                return ($"Khu {zone.Name} ({zone.Code})", allSlots);
            }

            case ClosureTargetType.Lot:
            {
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
                return ($"Bãi {lot.Name}", allSlots);
            }

            default:
                throw new ValidationException($"Loại mục tiêu '{targetType}' không được hỗ trợ.");
        }
    }
}
