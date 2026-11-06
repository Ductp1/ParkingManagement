using ParkingManagement.SharedKernel.Exceptions;
using PaymentService.Application.Interfaces;

namespace PaymentService.Application.Features.RateCards;

/// <summary>
/// Cổng ca sử dụng tra cứu bảng giá theo bãi xe hoặc theo ID.
/// </summary>
public interface IGetRateCardsByLotUseCase
{
    /// <summary>
    /// Lấy danh sách toàn bộ bảng giá của một bãi xe.
    /// </summary>
    Task<IReadOnlyList<RateCardDetailDto>> ExecuteAsync(int parkingLotId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lấy thông tin chi tiết một bảng giá theo khóa chính ID.
    /// </summary>
    Task<RateCardDetailDto> GetByIdAsync(int id, CancellationToken cancellationToken = default);
}

/// <summary>
/// Cài đặt ca sử dụng tra cứu bảng giá độc lập.
/// </summary>
public sealed class GetRateCardsByLotUseCase(IRateCardManagementRepository repo) : IGetRateCardsByLotUseCase
{
    public async Task<IReadOnlyList<RateCardDetailDto>> ExecuteAsync(int parkingLotId, CancellationToken cancellationToken = default)
    {
        if (parkingLotId <= 0)
            throw new ValidationException("ParkingLotId phải là số nguyên dương.");

        var cards = await repo.GetByParkingLotIdAsync(parkingLotId, cancellationToken);
        return cards.Select(CreateRateCardUseCase.MapToDto).ToList();
    }

    public async Task<RateCardDetailDto> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        if (id <= 0)
            throw new ValidationException("Id phải là số nguyên dương.");

        var card = await repo.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException("Bảng giá", id);

        return CreateRateCardUseCase.MapToDto(card);
    }
}

