using Microsoft.Extensions.Logging;
using ParkingManagement.SharedKernel.Enums;
using ParkingManagement.SharedKernel.Exceptions;
using ParkingService.Application.Abstractions;
using ParkingService.Domain.Entities;

namespace ParkingService.Application.Features.Closures;

public interface IGetLotClosureSchedulesUseCase
{
    Task<IReadOnlyList<ClosureScheduleDto>> ExecuteAsync(
        int parkingLotId,
        ClosureTargetType? targetType = null,
        bool? activeOnly = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// USE CASE: Lấy danh sách lịch tạm đóng của một bãi xe (US-063).
/// Hỗ trợ lọc theo TargetType và lọc chỉ lấy các lịch đang có hiệu lực.
/// </summary>
public sealed class GetLotClosureSchedulesUseCase(
    IParkingLotManagementRepository lotRepository,
    IZoneRepository zoneRepository,
    IFloorRepository floorRepository,
    ISlotRepository slotRepository,
    IClosureScheduleRepository closureScheduleRepository,
    ILogger<GetLotClosureSchedulesUseCase> logger) : IGetLotClosureSchedulesUseCase
{
    public async Task<IReadOnlyList<ClosureScheduleDto>> ExecuteAsync(
        int parkingLotId,
        ClosureTargetType? targetType = null,
        bool? activeOnly = null,
        CancellationToken cancellationToken = default)
    {
        if (parkingLotId <= 0)
            throw new ValidationException("ParkingLotId không hợp lệ.");

        _ = await lotRepository.GetByIdAsync(parkingLotId, cancellationToken)
            ?? throw new NotFoundException("Bãi đỗ xe (ParkingLot)", parkingLotId);

        var schedules = await closureScheduleRepository.GetByParkingLotIdAsync(
            parkingLotId, targetType, activeOnly, cancellationToken);

        var now = DateTime.UtcNow;
        var result = new List<ClosureScheduleDto>();

        foreach (var s in schedules)
        {
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

            result.Add(new ClosureScheduleDto(
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
                s.CreatedAtUtc));
        }

        logger.LogInformation("Tìm thấy {Count} lịch đóng cho bãi Id={LotId}", result.Count, parkingLotId);
        return result;
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
