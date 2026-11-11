using ParkingManagement.SharedKernel.Exceptions;
using ParkingService.Application.Abstractions;
using ParkingService.Domain.Enums;

namespace ParkingService.Application.Features.SanctionStates;

public interface IGetLotSanctionStatusUseCase
{
    Task<LotSanctionStatusDto> ExecuteAsync(int parkingLotId, CancellationToken cancellationToken = default);
}

/// <summary>
/// US-097: trạng thái công khai cho tài xế / BookingService – chỉ cho biết bãi có đang bị tạm ngưng nhận đặt chỗ mới không.
/// Không lộ chế tài hạ thứ hạng (chỉ Admin xem qua <see cref="IGetLotSanctionStateUseCase"/>).
/// </summary>
public sealed class GetLotSanctionStatusUseCase(ISanctionStateQueries queries, TimeProvider timeProvider) : IGetLotSanctionStatusUseCase
{
    public async Task<LotSanctionStatusDto> ExecuteAsync(int parkingLotId, CancellationToken cancellationToken = default)
    {
        var (lot, effects) = await LotSanctionLoader.LoadAsync(queries, parkingLotId, timeProvider.GetUtcNow().UtcDateTime, cancellationToken);
        var suspended = effects.IsListingSuspended(lot.Id, lot.OwnerProfileId);
        return new LotSanctionStatusDto(suspended,
            suspended ? effects.Until(lot.Id, lot.OwnerProfileId, SanctionStateKind.ListingSuspension) : null);
    }
}

/// <summary>Đọc bãi + chế tài đang hiệu lực (theo bãi và theo chủ bãi) cho hai use case trạng thái.</summary>
internal static class LotSanctionLoader
{
    public static async Task<(SanctionLotRow Lot, LotSanctionEffects Effects)> LoadAsync(ISanctionStateQueries queries, int parkingLotId,
        DateTime nowUtc, CancellationToken cancellationToken)
    {
        if (parkingLotId <= 0) throw new ValidationException("Mã bãi không hợp lệ.");
        var lot = await queries.FindLotAsync(parkingLotId, cancellationToken)
                  ?? throw new NotFoundException("Bãi đỗ xe", parkingLotId);
        var states = await queries.ListInEffectAsync([lot.Id], [lot.OwnerProfileId], nowUtc, cancellationToken);
        return (lot, new LotSanctionEffects(states));
    }
}
