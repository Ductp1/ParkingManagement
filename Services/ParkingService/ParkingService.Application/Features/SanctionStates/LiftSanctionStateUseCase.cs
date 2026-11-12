using ParkingManagement.SharedKernel.Exceptions;
using ParkingService.Application.Abstractions;

namespace ParkingService.Application.Features.SanctionStates;

public interface ILiftSanctionStateUseCase
{
    Task ExecuteAsync(int sanctionId, int adminUserId, CancellationToken cancellationToken = default);
}

/// <summary>
/// US-097: chế tài bị gỡ trước hạn bên AdminService → gỡ hiệu lực ở ParkingService. Idempotent: chưa từng áp hoặc đã gỡ → không làm gì
/// (AdminService có thể gỡ chế tài mà lần đẩy sang trước đó chưa thành công).
/// </summary>
public sealed class LiftSanctionStateUseCase(ISanctionStateRepository repository, TimeProvider timeProvider) : ILiftSanctionStateUseCase
{
    public async Task ExecuteAsync(int sanctionId, int adminUserId, CancellationToken cancellationToken = default)
    {
        if (sanctionId <= 0) throw new ValidationException("Mã chế tài không hợp lệ.");

        var state = await repository.FindBySanctionIdAsync(sanctionId, cancellationToken);
        if (state is null || state.LiftedAtUtc is not null) return;

        state.Lift(adminUserId, timeProvider.GetUtcNow().UtcDateTime);
        await repository.SaveAsync(cancellationToken);
    }
}
