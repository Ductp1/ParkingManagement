using ParkingManagement.SharedKernel.Enums;
using ParkingManagement.SharedKernel.Exceptions;
using ParkingService.Application.Abstractions;
using ParkingService.Domain.Entities;

namespace ParkingService.Application.Features.PhysicalChanges;

public interface IGetPhysicalChangeSlaSummaryUseCase
{
    Task<PhysicalChangeSlaSummaryDto> ExecuteAsync(int lotId, CancellationToken cancellationToken = default);
}

/// <summary>
/// USE CASE: Báo cáo tóm tắt tỷ lệ tuân thủ SLA 48h, sự cố đặt nhầm và các vi phạm SLA Cấp 2 của bãi xe (US-061).
/// </summary>
public sealed class GetPhysicalChangeSlaSummaryUseCase(ILotPhysicalChangeRepository repository) : IGetPhysicalChangeSlaSummaryUseCase
{
    public async Task<PhysicalChangeSlaSummaryDto> ExecuteAsync(int lotId, CancellationToken cancellationToken = default)
    {
        var lot = await repository.GetLotByIdAsync(lotId, cancellationToken);
        if (lot == null)
            throw new NotFoundException("Bãi đỗ xe", lotId);

        var list = await repository.GetByLotIdAsync(lotId, cancellationToken);
        var nowUtc = DateTime.UtcNow;

        int total = list.Count;
        int inProgress = 0;
        int completedOnTime = 0;
        int overdue = 0;
        int completedLate = 0;
        int totalIncidents = 0;
        int level2Violations = 0;
        decimal totalCompensation = 0;

        foreach (var c in list)
        {
            if (c.Status == PhysicalChangeStatus.InProgress)
            {
                if (nowUtc > c.DeadlineUtc) overdue++;
                else inProgress++;
            }
            else if (c.Status == PhysicalChangeStatus.Completed)
            {
                completedOnTime++;
            }
            else if (c.Status == PhysicalChangeStatus.Overdue)
            {
                overdue++;
            }
            else if (c.Status == PhysicalChangeStatus.CompletedLate)
            {
                completedLate++;
            }

            totalIncidents += c.BookingIncidentCount;
            if (c.ViolationLevel == SanctionLevel.RankingPenalty)
            {
                level2Violations++;
            }
            totalCompensation += c.TotalCompensationAmount;
        }

        double complianceRate = total == 0
            ? 100.0
            : Math.Round(completedOnTime * 100.0 / total, 1);

        return new PhysicalChangeSlaSummaryDto(
            lotId,
            total,
            inProgress,
            completedOnTime,
            overdue,
            completedLate,
            totalIncidents,
            level2Violations,
            totalCompensation,
            complianceRate);
    }
}
