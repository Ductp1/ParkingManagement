using ParkingManagement.SharedKernel.Enums;

namespace PaymentService.Application.Features.Promotions;

/// <summary>
/// DTO trả về kết quả chiết khấu khi tài xế nhập mã khuyến mãi tại Checkout (US-073 AC3).
/// </summary>
public sealed record AppliedPromotionDto(
    int PromotionId,
    string Code,
    string Name,
    PromotionSponsor Sponsor,
    DiscountType DiscountType,
    decimal OriginalAmount,
    decimal DiscountAmount,
    decimal FinalAmount,
    string Message);

