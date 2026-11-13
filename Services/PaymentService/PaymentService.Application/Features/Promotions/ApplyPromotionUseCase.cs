using ParkingManagement.SharedKernel.Enums;
using ParkingManagement.SharedKernel.Exceptions;
using PaymentService.Application.Interfaces;

namespace PaymentService.Application.Features.Promotions;

/// <summary>
/// Ca sử dụng kiểm tra và áp dụng mã khuyến mãi tại Checkout (US-073 AC3).
/// </summary>
public sealed class ApplyPromotionUseCase : IApplyPromotionUseCase
{
    private readonly IPromotionRepository repo;
    private readonly TimeProvider timeProvider;

    public ApplyPromotionUseCase(IPromotionRepository repo, TimeProvider? timeProvider = null)
    {
        this.repo = repo;
        this.timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<AppliedPromotionDto> ExecuteAsync(ApplyPromotionQuery query, CancellationToken cancellationToken = default)
    {
        // 1. Kiểm tra tham số đầu vào
        if (query.OriginalAmount <= 0)
            throw new ValidationException("Giá trị đơn hàng ban đầu phải là số dương lớn hơn 0.");

        if (string.IsNullOrWhiteSpace(query.Code))
            throw new ValidationException("Mã khuyến mãi không được để trống.");

        if (query.ParkingLotId <= 0)
            throw new ValidationException("ParkingLotId phải là số nguyên dương.");

        // 2. Tìm mã khuyến mãi trong kho dữ liệu
        var promo = await repo.GetByCodeAsync(query.Code, cancellationToken)
            ?? throw new NotFoundException("Mã khuyến mãi", query.Code.Trim().ToUpperInvariant());

        // 3. Kiểm tra trạng thái kích hoạt của mã
        if (!promo.IsActive)
            throw new ValidationException("Mã khuyến mãi hiện đang tạm ngưng hoạt động.");

        // 4. Kiểm tra thời gian hiệu lực
        var now = timeProvider.GetUtcNow().UtcDateTime;
        if (now < promo.StartsAtUtc)
            throw new ValidationException($"Mã khuyến mãi chưa có hiệu lực (bắt đầu từ {promo.StartsAtUtc:dd/MM/yyyy HH:mm} UTC).");

        if (now > promo.EndsAtUtc)
            throw new ValidationException("Mã khuyến mãi đã hết hạn sử dụng.");

        // 5. Kiểm tra giới hạn số lượt sử dụng
        if (promo.UsageLimit.HasValue && promo.UsedCount >= promo.UsageLimit.Value)
            throw new ValidationException("Mã khuyến mãi đã hết lượt sử dụng.");

        // 6. Kiểm tra phạm vi bãi xe áp dụng
        if (promo.ParkingLotId.HasValue && promo.ParkingLotId.Value != query.ParkingLotId)
            throw new ValidationException("Mã khuyến mãi này không áp dụng cho bãi đỗ xe được chọn.");

        // 7. Kiểm tra giá trị đơn hàng tối thiểu
        if (promo.MinOrderAmount.HasValue && query.OriginalAmount < promo.MinOrderAmount.Value)
            throw new ValidationException($"Đơn hàng phải đạt giá trị tối thiểu {promo.MinOrderAmount.Value:N0} VNĐ để áp dụng mã này.");

        // 8. Tính toán số tiền được giảm giá
        decimal discount;
        if (promo.DiscountType == DiscountType.Percentage)
        {
            discount = (query.OriginalAmount * promo.DiscountValue) / 100m;
            // Áp trần giảm giá tối đa nếu có cấu hình
            if (promo.MaxDiscountAmount.HasValue && discount > promo.MaxDiscountAmount.Value)
            {
                discount = promo.MaxDiscountAmount.Value;
            }
        }
        else
        {
            // Giảm theo số tiền cố định nhưng không vượt quá tổng tiền đơn
            discount = Math.Min(promo.DiscountValue, query.OriginalAmount);
        }

        discount = Math.Round(discount, 2);
        var finalAmount = Math.Max(0m, query.OriginalAmount - discount);

        string msg = promo.DiscountType == DiscountType.Percentage
            ? $"Áp dụng thành công mã {promo.Code}: Giảm {promo.DiscountValue:N0}% (tiết kiệm {discount:N0} VNĐ)."
            : $"Áp dụng thành công mã {promo.Code}: Giảm trực tiếp {discount:N0} VNĐ.";

        return new AppliedPromotionDto(
            promo.Id,
            promo.Code,
            promo.Name,
            promo.Sponsor,
            promo.DiscountType,
            query.OriginalAmount,
            discount,
            finalAmount,
            msg);
    }
}

