using Microsoft.Extensions.Logging;
using ParkingManagement.SharedKernel.Exceptions;
using ParkingService.Application.Abstractions;

namespace ParkingService.Application.Features.Layouts;

public interface IGetFloorLayoutVersionsUseCase
{
    Task<IReadOnlyList<LayoutVersionSummaryDto>> ExecuteAsync(int floorId, CancellationToken cancellationToken = default);
}

/// <summary>
/// USE CASE: Lấy danh sách lịch sử các phiên bản sơ đồ của tầng (US-059).
/// Sắp xếp giảm dần theo số phiên bản VersionNo.
/// </summary>
public sealed class GetFloorLayoutVersionsUseCase(
    IFloorRepository floorRepository,
    ILayoutVersionRepository layoutVersionRepository,
    ILogger<GetFloorLayoutVersionsUseCase> logger) : IGetFloorLayoutVersionsUseCase
{
    public async Task<IReadOnlyList<LayoutVersionSummaryDto>> ExecuteAsync(int floorId, CancellationToken cancellationToken = default)
    {
        if (floorId <= 0)
            throw new ValidationException("FloorId không hợp lệ.");

        _ = await floorRepository.GetByIdAsync(floorId, cancellationToken)
            ?? throw new NotFoundException("Tầng (Floor)", floorId);

        var versions = await layoutVersionRepository.GetAllVersionsByFloorIdAsync(floorId, cancellationToken);

        logger.LogInformation("Lấy danh sách {Count} phiên bản sơ đồ của Floor Id={FloorId}", versions.Count, floorId);

        return versions.Select(v => new LayoutVersionSummaryDto(
            Id: v.Id,
            FloorId: v.FloorId,
            VersionNo: v.VersionNo,
            IsPublished: v.IsPublished,
            PublishedAtUtc: v.PublishedAtUtc,
            CreatedByUserId: v.CreatedByUserId,
            ChangeNote: v.ChangeNote,
            CreatedAtUtc: v.CreatedAtUtc
        )).ToList();
    }
}
