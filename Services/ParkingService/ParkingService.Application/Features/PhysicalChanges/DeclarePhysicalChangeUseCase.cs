using System.Text.Json;
using ParkingManagement.SharedKernel.Exceptions;
using ParkingService.Application.Abstractions;
using ParkingService.Domain.Entities;

namespace ParkingService.Application.Features.PhysicalChanges;

public interface IDeclarePhysicalChangeUseCase
{
    Task<LotPhysicalChangeDto> ExecuteAsync(
        int lotId,
        DeclarePhysicalChangeRequest request,
        int? ownerProfileId = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// USE CASE: Khai báo đợt thay đổi vật lý tại hiện trường bãi xe (Tiêu chí 1, US-061).
/// - Tự động kích hoạt bộ đếm ngược 48 giờ: DeadlineUtc = OccurredAtUtc + 48h.
/// - Lưu trữ danh sách mã ô đỗ bị ảnh hưởng (AffectedSlotCodes) để đối chiếu sự cố đặt nhầm (Tiêu chí 2).
/// </summary>
public sealed class DeclarePhysicalChangeUseCase(ILotPhysicalChangeRepository repository) : IDeclarePhysicalChangeUseCase
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<LotPhysicalChangeDto> ExecuteAsync(
        int lotId,
        DeclarePhysicalChangeRequest request,
        int? ownerProfileId = null,
        CancellationToken cancellationToken = default)
    {
        var lot = await repository.GetLotByIdAsync(lotId, cancellationToken);
        if (lot == null)
            throw new NotFoundException("Bãi đỗ xe", lotId);

        if (ownerProfileId.HasValue && ownerProfileId.Value > 0 && lot.OwnerProfileId != ownerProfileId.Value)
            throw new ForbiddenException("Bạn không có quyền thao tác trên bãi xe này.");

        if (string.IsNullOrWhiteSpace(request.Title))
            throw new ValidationException("Tiêu đề đợt thay đổi vật lý không được để trống.");

        if (!Enum.TryParse<PhysicalChangeScope>(request.Scope, true, out var scope))
            scope = PhysicalChangeScope.Floor;

        if (!Enum.TryParse<PhysicalChangeType>(request.ChangeType, true, out var changeType))
            changeType = PhysicalChangeType.LayoutRenovation;

        var nowUtc = DateTime.UtcNow;
        var occurredAtUtc = request.OccurredAtUtc ?? nowUtc;
        var deadlineUtc = occurredAtUtc.AddHours(48);

        string? affectedSlotsJson = null;
        if (request.AffectedSlotCodes != null && request.AffectedSlotCodes.Count > 0)
        {
            var cleanCodes = request.AffectedSlotCodes
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .Select(c => c.Trim().ToUpperInvariant())
                .Distinct()
                .ToList();

            if (cleanCodes.Count > 0)
            {
                affectedSlotsJson = JsonSerializer.Serialize(cleanCodes, JsonOptions);
            }
        }

        var change = new LotPhysicalChange
        {
            ParkingLotId = lotId,
            Scope = scope,
            TargetId = request.TargetId,
            AffectedSlotCodesJson = affectedSlotsJson,
            ChangeType = changeType,
            Title = request.Title.Trim(),
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            OccurredAtUtc = occurredAtUtc,
            DeadlineUtc = deadlineUtc,
            Status = PhysicalChangeStatus.InProgress,
            ReminderCount = 0,
            HasBookingIncident = false,
            BookingIncidentCount = 0,
            OwnerLiabilityPercent = 0,
            TotalCompensationAmount = 0
        };

        await repository.AddAsync(change, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);

        return change.ToDto(nowUtc);
    }
}
