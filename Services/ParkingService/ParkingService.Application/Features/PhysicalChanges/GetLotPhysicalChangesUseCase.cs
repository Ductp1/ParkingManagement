using ParkingManagement.SharedKernel.Exceptions;
using ParkingService.Application.Abstractions;

namespace ParkingService.Application.Features.PhysicalChanges;

public interface IGetLotPhysicalChangesUseCase
{
    Task<IReadOnlyList<LotPhysicalChangeDto>> ExecuteAsync(int lotId, CancellationToken cancellationToken = default);
}

/// <summary>
/// USE CASE: Lấy danh sách các đợt thay đổi vật lý của bãi xe kèm thời gian đếm ngược 48h (Tiêu chí 1, US-061).
/// </summary>
public sealed class GetLotPhysicalChangesUseCase(ILotPhysicalChangeRepository repository) : IGetLotPhysicalChangesUseCase
{
    public async Task<IReadOnlyList<LotPhysicalChangeDto>> ExecuteAsync(int lotId, CancellationToken cancellationToken = default)
    {
        var lot = await repository.GetLotByIdAsync(lotId, cancellationToken);
        if (lot == null)
            throw new NotFoundException("Bãi đỗ xe", lotId);

        var list = await repository.GetByLotIdAsync(lotId, cancellationToken);
        var nowUtc = DateTime.UtcNow;

        var result = new List<LotPhysicalChangeDto>(list.Count);
        bool hasChanges = false;

        foreach (var item in list)
        {
            if (item.CheckAndMarkOverdue(nowUtc))
            {
                hasChanges = true;
            }
            result.Add(item.ToDto(nowUtc));
        }

        if (hasChanges)
        {
            await repository.SaveChangesAsync(cancellationToken);
        }

        return result;
    }
}
