namespace PaymentService.Application.Features.Promotions;

/// <summary>
/// Cổng ca sử dụng cập nhật thông tin và bật/tắt mã khuyến mãi (US-073).
/// </summary>
public interface IUpdatePromotionUseCase
{
    /// <summary>Cập nhật thông tin cấu hình và trạng thái của mã khuyến mãi.</summary>
    Task<PromotionDto> ExecuteAsync(UpdatePromotionCommand command, CancellationToken cancellationToken = default);
}

