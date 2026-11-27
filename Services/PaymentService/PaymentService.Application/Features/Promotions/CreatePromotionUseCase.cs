using ParkingManagement.SharedKernel.Enums;
using ParkingManagement.SharedKernel.Exceptions;
using PaymentService.Application.Interfaces;
using PaymentService.Domain.Entities;

namespace PaymentService.Application.Features.Promotions;

/// <summary>
/// Ca sử dụng tạo mới mã khuyến mãi với điều kiện và bên tài trợ (US-073 AC1, AC2).
/// </summary>
public sealed class CreatePromotionUseCase(IPromotionRepository repo) : ICreatePromotionUseCase
{
    public async Task<PromotionDto> ExecuteAsync(CreatePromotionCommand command, CancellationToken cancellationToken = default)
    {
        // 1. Kiểm tra tính hợp lệ cơ bản của mã code và tên
        if (string.IsNullOrWhiteSpace(command.Code))
            throw new ValidationException("Mã khuyến mãi (Code) không được để trống.");

        var normalizedCode = command.Code.Trim().ToUpperInvariant();
        if (normalizedCode.Length > 30)
            throw new ValidationException("Mã khuyến mãi không được vượt quá 30 ký tự.");

        if (string.IsNullOrWhiteSpace(command.Name))
            throw new ValidationException("Tên chương trình khuyến mãi không được để trống.");

        // 2. Kiểm tra tính hợp lệ của thời gian hiệu lực
        if (command.EndsAtUtc <= command.StartsAtUtc)
            throw new ValidationException("Thời điểm kết thúc (EndsAtUtc) phải lớn hơn thời điểm bắt đầu (StartsAtUtc).");

        // 3. Kiểm tra loại giảm và giá trị giảm
        if (command.DiscountValue <= 0)
            throw new ValidationException("Giá trị giảm giá phải là số dương lớn hơn 0.");

        if (command.DiscountType == DiscountType.Percentage && command.DiscountValue > 100)
            throw new ValidationException("Giảm giá theo phần trăm không được vượt quá 100%.");

        if (command.MaxDiscountAmount.HasValue && command.MaxDiscountAmount.Value < 0)
            throw new ValidationException("Mức giảm tối đa không được là số âm.");

        if (command.MinOrderAmount.HasValue && command.MinOrderAmount.Value < 0)
            throw new ValidationException("Giá trị đơn tối thiểu không được là số âm.");

        if (command.UsageLimit.HasValue && command.UsageLimit.Value <= 0)
            throw new ValidationException("Số lượt sử dụng tối đa phải lớn hơn 0 nếu được thiết lập.");

        // 4. US-073 AC2: Kiểm tra quy tắc bên tài trợ
        if (command.Sponsor == PromotionSponsor.LotOwner)
        {
            if (!command.ParkingLotId.HasValue || command.ParkingLotId.Value <= 0)
                throw new ValidationException("Khuyến mãi do Chủ bãi tài trợ bắt buộc phải gắn với một bãi xe cụ thể (ParkingLotId).");

            if (!command.OwnerProfileId.HasValue || command.OwnerProfileId.Value <= 0)
                throw new ValidationException("Khuyến mãi do Chủ bãi tài trợ bắt buộc phải có mã chủ bãi (OwnerProfileId).");
        }

        // 5. Kiểm tra trùng lặp mã trong hệ thống
        var codeExists = await repo.ExistsByCodeAsync(normalizedCode, null, cancellationToken);
        if (codeExists)
            throw new ValidationException($"Mã khuyến mãi '{normalizedCode}' đã tồn tại trong hệ thống.");

        // 6. Khởi tạo thực thể và lưu xuống cơ sở dữ liệu
        var promotion = new Promotion
        {
            Code = normalizedCode,
            Name = command.Name.Trim(),
            ParkingLotId = command.ParkingLotId,
            OwnerProfileId = command.Sponsor == PromotionSponsor.LotOwner ? command.OwnerProfileId : null,
            Sponsor = command.Sponsor,
            DiscountType = command.DiscountType,
            DiscountValue = command.DiscountValue,
            MaxDiscountAmount = command.MaxDiscountAmount,
            MinOrderAmount = command.MinOrderAmount,
            StartsAtUtc = command.StartsAtUtc,
            EndsAtUtc = command.EndsAtUtc,
            UsageLimit = command.UsageLimit,
            UsedCount = 0,
            IsActive = true
        };

        await repo.AddAsync(promotion, cancellationToken);

        return MapToDto(promotion);
    }

    public static PromotionDto MapToDto(Promotion p)
    {
        return new PromotionDto(
            p.Id,
            p.Code,
            p.Name,
            p.ParkingLotId,
            p.OwnerProfileId,
            p.Sponsor,
            p.DiscountType,
            p.DiscountValue,
            p.MaxDiscountAmount,
            p.MinOrderAmount,
            p.StartsAtUtc,
            p.EndsAtUtc,
            p.UsageLimit,
            p.UsedCount,
            p.IsActive);
    }
}

