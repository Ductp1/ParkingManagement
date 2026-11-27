using Microsoft.Extensions.Logging;
using ParkingManagement.SharedKernel.Enums;
using ParkingManagement.SharedKernel.Exceptions;
using ParkingService.Application.Abstractions;
using ParkingService.Domain.Entities;

namespace ParkingService.Application.Features.Layouts;

public interface IValidateFloorLayoutSafetyUseCase
{
    Task<FloorLayoutSafetyCheckResultDto> ExecuteAsync(FloorLayoutSafetyCheckCommand command, CancellationToken cancellationToken = default);
}

/// <summary>
/// USE CASE: Kiểm tra trước tác động an toàn (Pre-flight Safety Check) của bản vẽ dự kiến đối với các slot active trong DB (US-060).
/// Giúp Client hiển thị cảnh báo trực quan trước khi chủ bãi nhấn xuất bản hoặc lưu.
/// </summary>
public sealed class ValidateFloorLayoutSafetyUseCase(
    IFloorRepository floorRepository,
    ISlotRepository slotRepository,
    ILogger<ValidateFloorLayoutSafetyUseCase> logger) : IValidateFloorLayoutSafetyUseCase
{
    public async Task<FloorLayoutSafetyCheckResultDto> ExecuteAsync(FloorLayoutSafetyCheckCommand command, CancellationToken cancellationToken = default)
    {
        if (command.FloorId <= 0)
            throw new ValidationException("FloorId không hợp lệ.");

        var floor = await floorRepository.GetByIdAsync(command.FloorId, cancellationToken)
            ?? throw new NotFoundException("Tầng (Floor)", command.FloorId);

        var existingDbSlots = await slotRepository.GetByFloorIdAsync(command.FloorId, cancellationToken);
        var activeDbSlots = existingDbSlots.Where(s => s.IsActive).ToList();

        var slotCells = (command.Cells ?? [])
            .Where(c => string.Equals(c.Type, "slot", StringComparison.OrdinalIgnoreCase))
            .ToList();

        var newLayoutSlotMap = slotCells
            .Where(c => !string.IsNullOrWhiteSpace(c.Code))
            .GroupBy(c => c.Code!.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var safetyBlockers = new List<string>();
        var warnings = new List<string>();
        var atRiskSlots = new List<string>();
        var removedSlotsCount = 0;

        foreach (var dbSlot in activeDbSlots)
        {
            var code = dbSlot.Code.Trim();
            var isActiveBooking = dbSlot.State == SlotState.Occupied || dbSlot.State == SlotState.Reserved;

            if (!newLayoutSlotMap.TryGetValue(code, out var targetCell))
            {
                // Slot bị gỡ khỏi bản vẽ mới
                removedSlotsCount++;
                if (isActiveBooking)
                {
                    atRiskSlots.Add(code);
                    var reason = dbSlot.State == SlotState.Occupied ? "đang có phương tiện đỗ" : "đang được giữ chỗ";
                    safetyBlockers.Add($"Ô đỗ '{code}' {reason} nhưng bị xóa khỏi bản vẽ mới.");
                }
                else
                {
                    warnings.Add($"Ô đỗ '{code}' không có trong bản vẽ mới và sẽ bị vô hiệu hóa khi xuất bản.");
                }
            }
            else
            {
                // Slot có mặt trong bản vẽ mới -> kiểm tra hạ cấp loại xe
                if (isActiveBooking && targetCell.MaxVehicleType.HasValue)
                {
                    if (GetVehicleTypeRank(targetCell.MaxVehicleType.Value) < GetVehicleTypeRank(dbSlot.MaxVehicleType))
                    {
                        atRiskSlots.Add(code);
                        var reason = dbSlot.State == SlotState.Occupied ? "đang có phương tiện đỗ" : "đang được giữ chỗ";
                        safetyBlockers.Add($"Ô đỗ '{code}' {reason} nhưng bị hạ cấp loại xe từ '{dbSlot.MaxVehicleType}' xuống '{targetCell.MaxVehicleType.Value}'.");
                    }
                }
            }
        }

        var canProceed = safetyBlockers.Count == 0;

        logger.LogInformation("Kiểm tra an toàn Floor Id={FloorId}: CanProceed={CanProceed}, Blockers={BlockersCount}, Warnings={WarningsCount}",
            command.FloorId, canProceed, safetyBlockers.Count, warnings.Count);

        return new FloorLayoutSafetyCheckResultDto(
            CanProceed: canProceed,
            TotalActiveSlotsInDb: activeDbSlots.Count,
            TotalSlotsInNewLayout: slotCells.Count,
            RemovedSlotsCount: removedSlotsCount,
            ActiveBookingAtRiskSlots: atRiskSlots,
            SafetyBlockers: safetyBlockers,
            Warnings: warnings);
    }

    private static int GetVehicleTypeRank(VehicleType vehicleType) => vehicleType switch
    {
        VehicleType.Motorbike => 1,
        VehicleType.Sedan => 2,
        VehicleType.Suv => 3,
        VehicleType.Pickup => 4,
        VehicleType.Van => 5,
        VehicleType.Oversized => 6,
        _ => 2
    };
}
