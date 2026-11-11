using Microsoft.Extensions.Logging;
using ParkingManagement.SharedKernel.Enums;
using ParkingManagement.SharedKernel.Exceptions;
using ParkingService.Application.Abstractions;
using ParkingService.Domain.Entities;

namespace ParkingService.Application.Features.Closures;

public interface IGetClosureScheduleByIdUseCase
{
    Task<ClosureScheduleDto> ExecuteAsync(int parkingLotId, int closureId, CancellationToken cancellationToken = default);
}

/// <summary>
/// USE CASE: Lấy chi tiết một lịch tạm đóng của bãi xe (US-063).
/// </summary>
public sealed class GetClosureScheduleByIdUseCase(
    IParkingLotManagementRepository lotRepository,
    IZoneRepository zoneRepository,
    IFloorRepository floorRepository,
    ISlotRepository slotRepository,
    IClosureScheduleRepository closureScheduleRepository,
    ILogger<GetClosureScheduleByIdUseCase> logger) : IGetClosureScheduleByIdUseCase
{
    public async Task<ClosureScheduleDto> ExecuteAsync(int parkingLotId, int closureId, CancellationToken cancellationToken = default)
    {
        if (parkingLotId <= 0)
            throw new ValidationException("ParkingLotId không hợp lệ.");

        if (closureId <= 0)
            throw new ValidationException("ClosureId không hợp lệ.");

        _ = await lotRepository.GetByIdAsync(parkingLotId, cancellationToken)
            ?? throw new NotFoundException("Bãi đỗ xe (ParkingLot)", parkingLotId);

        var s = await closureScheduleRepository.GetByIdAsync(closureId, cancellationToken)
            ?? throw new NotFoundException("Lịch tạm đóng (ClosureSchedule)", closureId);

        if (s.ParkingLotId != parkingLotId)
            throw new ValidationException($"Lịch tạm đóng Id={closureId} không thuộc bãi xe Id={parkingLotId}.");

        var now = DateTime.UtcNow;
        var (targetName, affectedSlotsCount) = await ResolveTargetInfoAsync(
            parkingLotId, s.TargetType, s.TargetId, cancellationToken);

        string status;
        if (s.CancelledAtUtc.HasValue)
        {
            status = "Cancelled";
        }
        else if (s.StartsAtUtc <= now && (s.EndsAtUtc == null || s.EndsAtUtc.Value > now))
        {
            status = "Active";
        }
        else if (s.StartsAtUtc > now)
        {
            status = "Upcoming";
        }
        else
        {
            status = "Past";
        }

        logger.LogInformation("Lấy chi tiết ClosureSchedule Id={Id} cho bãi Id={LotId}", closureId, parkingLotId);

        return new ClosureScheduleDto(
            s.Id,
            s.ParkingLotId,
            s.TargetType.ToString(),
            s.TargetId,
            targetName,
            s.StartsAtUtc,
            s.EndsAtUtc,
            s.Reason,
            s.IsEmergency,
            s.AffectedBookingsNotified,
            s.CreatedByUserId,
            s.CancelledAtUtc,
            status,
            affectedSlotsCount,
            s.CreatedAtUtc);
    }

    private async Task<(string targetName, int affectedSlotsCount)> ResolveTargetInfoAsync(
        int parkingLotId, ClosureTargetType targetType, int targetId, CancellationToken cancellationToken)
    {
        switch (targetType)
        {
            case ClosureTargetType.Slot:
            {
                var slot = await slotRepository.GetByIdAsync(targetId, cancellationToken);
                return (slot != null ? $"Ô {slot.Code}" : $"Slot #{targetId}", slot != null && slot.IsActive ? 1 : 0);
            }

            case ClosureTargetType.Floor:
            {
                var floor = await floorRepository.GetByIdAsync(targetId, cancellationToken);
                var slots = floor != null ? await slotRepository.GetByFloorIdAsync(targetId, cancellationToken) : [];
                return (floor != null ? $"Tầng {floor.Name}" : $"Floor #{targetId}", slots.Count(s => s.IsActive));
            }

            case ClosureTargetType.Zone:
            {
                var zone = await zoneRepository.GetByIdAsync(targetId, cancellationToken);
                var total = 0;
                if (zone != null)
                {
                    var floors = await floorRepository.GetByZoneIdAsync(targetId, cancellationToken);
                    foreach (var f in floors)
                    {
                        var slots = await slotRepository.GetByFloorIdAsync(f.Id, cancellationToken);
                        total += slots.Count(s => s.IsActive);
                    }
                }
                return (zone != null ? $"Khu {zone.Name} ({zone.Code})" : $"Zone #{targetId}", total);
            }

            case ClosureTargetType.Lot:
            {
                var lot = await lotRepository.GetByIdAsync(parkingLotId, cancellationToken);
                return (lot != null ? $"Bãi {lot.Name}" : $"Lot #{parkingLotId}", lot?.TotalSlots ?? 0);
            }

            default:
                return ($"Target #{targetId}", 0);
        }
    }
}
