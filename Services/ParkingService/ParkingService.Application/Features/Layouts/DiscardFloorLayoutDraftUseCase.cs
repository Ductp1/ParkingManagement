using Microsoft.Extensions.Logging;
using ParkingManagement.SharedKernel.Exceptions;
using ParkingService.Application.Abstractions;

namespace ParkingService.Application.Features.Layouts;

public interface IDiscardFloorLayoutDraftUseCase
{
    Task ExecuteAsync(int floorId, CancellationToken cancellationToken = default);
}

/// <summary>
/// USE CASE: Hủy / xóa bản nháp dở dang của tầng (US-060).
/// Nếu tầng có bản nháp chưa xuất bản (IsPublished = false), xóa bản nháp đó để quay về sơ đồ xuất bản hiện tại.
/// </summary>
public sealed class DiscardFloorLayoutDraftUseCase(
    IFloorRepository floorRepository,
    ILayoutVersionRepository layoutVersionRepository,
    ILogger<DiscardFloorLayoutDraftUseCase> logger) : IDiscardFloorLayoutDraftUseCase
{
    public async Task ExecuteAsync(int floorId, CancellationToken cancellationToken = default)
    {
        if (floorId <= 0)
            throw new ValidationException("FloorId không hợp lệ.");

        _ = await floorRepository.GetByIdAsync(floorId, cancellationToken)
            ?? throw new NotFoundException("Tầng (Floor)", floorId);

        var existingDraft = await layoutVersionRepository.GetLatestDraftByFloorIdAsync(floorId, cancellationToken);
        if (existingDraft != null)
        {
            await layoutVersionRepository.DeleteAsync(existingDraft, cancellationToken);
            await layoutVersionRepository.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Đã hủy bỏ bản nháp sơ đồ v{VersionNo} cho Floor Id={FloorId}", existingDraft.VersionNo, floorId);
        }
        else
        {
            logger.LogInformation("Floor Id={FloorId} không có bản nháp nào cần hủy", floorId);
        }
    }
}
