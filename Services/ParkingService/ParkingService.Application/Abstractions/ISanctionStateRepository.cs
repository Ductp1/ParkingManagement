using ParkingService.Domain.Entities;

namespace ParkingService.Application.Abstractions;

/// <summary>PORT ghi cho hiệu lực chế tài SLA (US-097).</summary>
public interface ISanctionStateRepository
{
    /// <summary>Có tracking.</summary>
    Task<SanctionState?> FindBySanctionIdAsync(int sanctionId, CancellationToken cancellationToken = default);

    void Add(SanctionState state);

    /// <summary>Trùng SanctionId (hai PUT đồng thời) → ConflictException.</summary>
    Task SaveAsync(CancellationToken cancellationToken = default);
}
