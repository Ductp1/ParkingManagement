using ParkingManagement.SharedKernel.Enums;

namespace PaymentService.Application.Features.Promotions;

/// <summary>
/// DTO chứa đầy đủ thông tin chi tiết của một mã khuyến mãi (US-073).
/// </summary>
public sealed record PromotionDto(
    int Id,
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
    int? UsageLimit,
    int UsedCount,
    bool IsActive);

