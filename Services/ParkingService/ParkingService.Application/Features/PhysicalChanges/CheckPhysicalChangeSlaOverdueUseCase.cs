using ParkingService.Application.Abstractions;

namespace ParkingService.Application.Features.PhysicalChanges;

public interface ICheckPhysicalChangeSlaOverdueUseCase
{
    Task<IReadOnlyList<LotPhysicalChangeDto>> ExecuteAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// USE CASE: Quét tự động các đợt thay đổi vật lý đang InProgress đã vượt quá hạn chót 48 giờ (Tiêu chí 1 & 2, US-061).
/// - Tự động chuyển trạng thái sang Overdue.
/// </summary>
public sealed class CheckPhysicalChangeSlaOverdueUseCase(ILotPhysicalChangeRepository repository) : ICheckPhysicalChangeSlaOverdueUseCase
{
    public async Task<IReadOnlyList<LotPhysicalChangeDto>> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var nowUtc = DateTime.UtcNow;
        var overdueCandidates = await repository.GetOverdueCandidatesAsync(nowUtc, cancellationToken);

        var result = new List<LotPhysicalChangeDto>(overdueCandidates.Count);

        foreach (var candidate in overdueCandidates)
        {
            if (candidate.CheckAndMarkOverdue(nowUtc))
            {
                await repository.UpdateAsync(candidate, cancellationToken);
            }
            result.Add(candidate.ToDto(nowUtc));
        }

        if (overdueCandidates.Count > 0)
        {
            await repository.SaveChangesAsync(cancellationToken);
        }

        return result;
    }
}
