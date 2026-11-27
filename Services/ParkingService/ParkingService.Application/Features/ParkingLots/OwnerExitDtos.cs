namespace ParkingService.Application.Features.ParkingLots;

public sealed record OwnerLotSummaryItemDto(
    int LotId,
    string Name,
    string Address,
    string Status,
    int TotalSlots,
    int AvailableSlots);

public sealed record OwnerExitImpactSummaryDto(
    int OwnerProfileId,
    int TotalLots,
    int ActiveLots,
    int SuspendedLots,
    int TotalSlots,
    IReadOnlyList<OwnerLotSummaryItemDto> Lots);

public sealed record SuspendOwnerLotsResultDto(
    int OwnerProfileId,
    int TotalLotsSuspended,
    IReadOnlyList<int> SuspendedLotIds,
    DateTime SuspendedAtUtc);
