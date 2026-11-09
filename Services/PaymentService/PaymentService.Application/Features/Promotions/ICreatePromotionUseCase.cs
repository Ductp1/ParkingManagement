namespace PaymentService.Application.Features.Promotions;

/// <summary>
/// Cổng ca sử dụng tạo mới mã khuyến mãi (US-073).
/// </summary>
public interface ICreatePromotionUseCase
{
    /// <summary>Tạo mới mã khuyến mãi với đầy đủ điều kiện áp dụng và bên tài trợ.</summary>
    Task<PromotionDto> ExecuteAsync(CreatePromotionCommand command, CancellationToken cancellationToken = default);
}

