using ParkingManagement.SharedKernel.Exceptions;
using PaymentService.Application.Interfaces;

namespace PaymentService.Application.Features.Promotions;

/// <summary>
/// Ca sử dụng cập nhật thông tin và bật/tắt mã khuyến mãi (US-073).
/// </summary>
public sealed class UpdatePromotionUseCase(IPromotionRepository repo) : IUpdatePromotionUseCase
{
    public async Task<PromotionDto> ExecuteAsync(UpdatePromotionCommand command, CancellationToken cancellationToken = default)
    {
        if (command.Id <= 0)
            throw new ValidationException("PromotionId phải là số nguyên dương.");

        if (string.IsNullOrWhiteSpace(command.Name))
            throw new ValidationException("Tên chương trình khuyến mãi không được để trống.");

        if (command.EndsAtUtc <= command.StartsAtUtc)
            throw new ValidationException("Thời điểm kết thúc (EndsAtUtc) phải lớn hơn thời điểm bắt đầu (StartsAtUtc).");

        if (command.MaxDiscountAmount.HasValue && command.MaxDiscountAmount.Value < 0)
            throw new ValidationException("Mức giảm tối đa không được là số âm.");

        if (command.MinOrderAmount.HasValue && command.MinOrderAmount.Value < 0)
            throw new ValidationException("Giá trị đơn tối thiểu không được là số âm.");

        if (command.UsageLimit.HasValue && command.UsageLimit.Value <= 0)
            throw new ValidationException("Số lượt sử dụng tối đa phải lớn hơn 0 nếu được thiết lập.");

        var promo = await repo.GetByIdAsync(command.Id, cancellationToken)
            ?? throw new NotFoundException("Mã khuyến mãi", command.Id);

        promo.Name = command.Name.Trim();
        promo.MaxDiscountAmount = command.MaxDiscountAmount;
        promo.MinOrderAmount = command.MinOrderAmount;
        promo.StartsAtUtc = command.StartsAtUtc;
        promo.EndsAtUtc = command.EndsAtUtc;
        promo.UsageLimit = command.UsageLimit;
        promo.IsActive = command.IsActive;

        await repo.UpdateAsync(promo, cancellationToken);

        return CreatePromotionUseCase.MapToDto(promo);
    }
}

