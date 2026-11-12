using ParkingManagement.SharedKernel.Enums;

namespace PaymentService.Application.Features.Promotions;

/// <summary>
/// DTO gửi lên để tạo mới một mã khuyến mãi (US-073 AC1).
/// </summary>
public sealed record CreatePromotionCommand(
    string Code,
    string Name,
    int? ParkingLotId,
    int? OwnerProfileId,
    PromotionSponsor Sponsor,
    DiscountType DiscountType,
    decimal DiscountValue,
    decimal? MaxDiscountAmount,
    decimal? MinOrderAmount,
    DateTime StartsAtUtc,
    DateTime EndsAtUtc,
    int? UsageLimit);

