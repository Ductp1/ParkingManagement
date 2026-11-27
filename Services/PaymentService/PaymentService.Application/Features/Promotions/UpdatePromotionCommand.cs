namespace PaymentService.Application.Features.Promotions;

/// <summary>
/// DTO gửi lên để cập nhật thông tin mã khuyến mãi hoặc đổi trạng thái kích hoạt (US-073).
/// </summary>
public sealed record UpdatePromotionCommand(
    int Id,
    string Name,
    decimal? MaxDiscountAmount,
    decimal? MinOrderAmount,
    DateTime StartsAtUtc,
    DateTime EndsAtUtc,
    int? UsageLimit,
    bool IsActive);

