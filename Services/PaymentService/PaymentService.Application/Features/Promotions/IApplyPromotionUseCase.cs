namespace PaymentService.Application.Features.Promotions;

/// <summary>
/// Cổng ca sử dụng kiểm tra và áp dụng mã khuyến mãi tại Checkout (US-073 AC3).
/// </summary>
public interface IApplyPromotionUseCase
{
    /// <summary>Kiểm tra tính hợp lệ của mã và tính toán số tiền giảm giá trước khi thanh toán.</summary>
    Task<AppliedPromotionDto> ExecuteAsync(ApplyPromotionQuery query, CancellationToken cancellationToken = default);
}

