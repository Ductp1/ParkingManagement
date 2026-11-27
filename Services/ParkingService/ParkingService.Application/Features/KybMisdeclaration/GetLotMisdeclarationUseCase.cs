using ParkingManagement.SharedKernel.Exceptions;
using ParkingService.Application.Abstractions;

namespace ParkingService.Application.Features.KybMisdeclaration;

public interface IGetLotMisdeclarationUseCase
{
    Task<LotMisdeclarationDto> ExecuteAsync(int lotId, CancellationToken cancellationToken = default);
}

/// <summary>US-056: bãi (kể cả đang bị tạm khóa) + case đang mở + lịch sử các lần cảnh báo / tạm khóa của bãi.</summary>
public sealed class GetLotMisdeclarationUseCase(ILotMisdeclarationQueries queries, KybMisdeclarationSettings settings,
    TimeProvider timeProvider) : IGetLotMisdeclarationUseCase
{
    public Task<LotMisdeclarationDto> ExecuteAsync(int lotId, CancellationToken cancellationToken = default)
    {
        if (lotId <= 0) throw new ValidationException("lotId phải là số nguyên dương.");
        return MisdeclarationSupport.ReloadAsync(queries, settings, lotId, timeProvider.GetUtcNow().UtcDateTime, cancellationToken);
    }
}
