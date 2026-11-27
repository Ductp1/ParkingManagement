using ParkingManagement.SharedKernel.Exceptions;
using PaymentService.Application.Interfaces;

namespace PaymentService.Application.Features.Promotions;

/// <summary>
/// Ca sử dụng tra cứu danh sách và chi tiết mã khuyến mãi (US-073).
/// </summary>
public sealed class GetPromotionsUseCase(IPromotionRepository repo) : IGetPromotionsUseCase
{
    public async Task<IReadOnlyList<PromotionDto>> ListAsync(
        int? parkingLotId = null, 
        bool? isActiveOnly = null, 
        CancellationToken cancellationToken = default)
    {
        if (parkingLotId.HasValue && parkingLotId.Value <= 0)
            throw new ValidationException("ParkingLotId phải là số nguyên dương.");

        var list = await repo.ListAsync(parkingLotId, isActiveOnly, cancellationToken);
        return list.Select(CreatePromotionUseCase.MapToDto).ToList();
    }

    public async Task<PromotionDto> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        if (id <= 0)
            throw new ValidationException("PromotionId phải là số nguyên dương.");

        var found = await repo.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException("Mã khuyến mãi", id);

        return CreatePromotionUseCase.MapToDto(found);
    }
}

