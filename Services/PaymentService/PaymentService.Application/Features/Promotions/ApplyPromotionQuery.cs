namespace PaymentService.Application.Features.Promotions;

/// <summary>
/// DTO gửi lên để kiểm tra và áp dụng mã khuyến mãi tại Checkout (US-073 AC3).
/// </summary>
public sealed record ApplyPromotionQuery(
    string Code,
    int ParkingLotId,
    decimal OriginalAmount);

