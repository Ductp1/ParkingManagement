namespace PaymentService.Application.Features.Promotions;

/// <summary>
/// Cổng ca sử dụng tra cứu danh sách và chi tiết mã khuyến mãi (US-073).
/// </summary>
public interface IGetPromotionsUseCase
{
    /// <summary>Lấy danh sách mã khuyến mãi theo bãi đỗ xe hoặc toàn sàn.</summary>
    Task<IReadOnlyList<PromotionDto>> ListAsync(int? parkingLotId = null, bool? isActiveOnly = null, CancellationToken cancellationToken = default);

    /// <summary>Xem chi tiết một mã khuyến mãi theo ID.</summary>
    Task<PromotionDto> GetByIdAsync(int id, CancellationToken cancellationToken = default);
}

