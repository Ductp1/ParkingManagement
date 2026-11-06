using ParkingManagement.SharedKernel.Enums;
using ParkingManagement.SharedKernel.Exceptions;
using PaymentService.Application.Interfaces;
using PaymentService.Domain.Entities;

namespace PaymentService.Application.Features.RateCards;

/// <summary>
/// DTO chứa cấu hình của 1 block giá bậc thang theo loại xe (US-070).
/// </summary>
public sealed record CreateRateRuleRequest(
    VehicleType VehicleType,
    int FromMinute,
    int? ToMinute,
    int BlockMinutes,
    decimal PricePerBlock,
    int SortOrder);

/// <summary>
/// DTO gửi lên để tạo mới bảng giá kèm toàn bộ mức phụ thu và block giá (US-070, US-071).
/// </summary>
public sealed record CreateRateCardCommand(
    int ParkingLotId,
    int OwnerProfileId,
    string Name,
    DateTime EffectiveFromUtc,
    DateTime? EffectiveToUtc,
    decimal OvernightSurcharge,
    decimal WeekendMultiplier,
    decimal HolidayMultiplier,
    decimal OversizedMultiplier,
    decimal OverstayMultiplier,
    decimal? MaxDailyAmount,
    IReadOnlyList<CreateRateRuleRequest> Rules);

/// <summary>
/// DTO trả về kết quả chi tiết của bảng giá.
/// </summary>
public sealed record RateCardDetailDto(
    int Id,
    int ParkingLotId,
    int OwnerProfileId,
    string Name,
    DateTime EffectiveFromUtc,
    DateTime? EffectiveToUtc,
    bool IsActive,
    decimal OvernightSurcharge,
    decimal WeekendMultiplier,
    decimal HolidayMultiplier,
    decimal OversizedMultiplier,
    decimal OverstayMultiplier,
    decimal? MaxDailyAmount,
    IReadOnlyList<RateRuleDto> Rules);

/// <summary>
/// DTO mô tả một dòng quy tắc giá block giờ.
/// </summary>
public sealed record RateRuleDto(
    int Id,
    string VehicleType,
    int FromMinute,
    int? ToMinute,
    int BlockMinutes,
    decimal PricePerBlock,
    int SortOrder);

/// <summary>
/// Cổng ca sử dụng tạo mới bảng giá.
/// </summary>
public interface ICreateRateCardUseCase
{
    Task<RateCardDetailDto> ExecuteAsync(CreateRateCardCommand command, CancellationToken cancellationToken = default);
}

/// <summary>
/// Cài đặt ca sử dụng tạo mới bảng giá độc lập (US-070, US-071).
/// </summary>
public sealed class CreateRateCardUseCase(IRateCardManagementRepository repo) : ICreateRateCardUseCase
{
    public async Task<RateCardDetailDto> ExecuteAsync(CreateRateCardCommand command, CancellationToken cancellationToken = default)
    {
        // 1. Kiểm tra tính hợp lệ cơ bản của bãi xe và chủ bãi
        if (command.ParkingLotId <= 0)
            throw new ValidationException("ParkingLotId phải là số nguyên dương.");

        if (command.OwnerProfileId <= 0)
            throw new ValidationException("OwnerProfileId phải là số nguyên dương.");

        if (string.IsNullOrWhiteSpace(command.Name))
            throw new ValidationException("Tên bảng giá không được để trống.");

        // 2. Kiểm tra các hệ số phụ thu (US-071)
        if (command.OvernightSurcharge < 0)
            throw new ValidationException("Phụ thu qua đêm không được là số âm.");

        if (command.WeekendMultiplier < 1.0m || command.WeekendMultiplier > 3.0m)
            throw new ValidationException("Hệ số cuối tuần phải nằm trong khoảng từ 1.0 đến 3.0.");

        if (command.HolidayMultiplier < 1.0m || command.HolidayMultiplier > 3.0m)
            throw new ValidationException("Hệ số ngày lễ phải nằm trong khoảng từ 1.0 đến 3.0.");

        if (command.OverstayMultiplier < 1.0m || command.OverstayMultiplier > 3.0m)
            throw new ValidationException("Hệ số phạt quá giờ phải nằm trong khoảng từ 1.0 đến 3.0.");

        if (command.OversizedMultiplier < 1.0m || command.OversizedMultiplier > 3.0m)
            throw new ValidationException("Hệ số xe quá khổ phải nằm trong khoảng từ 1.0 đến 3.0.");

        // 3. Kiểm tra danh sách quy tắc block giờ (US-070)
        if (command.Rules == null || command.Rules.Count == 0)
            throw new ValidationException("Bảng giá phải có ít nhất 1 quy tắc giá theo block giờ.");

        foreach (var rule in command.Rules)
        {
            if (rule.BlockMinutes <= 0)
                throw new ValidationException("Số phút của một block (BlockMinutes) phải lớn hơn 0.");

            if (rule.PricePerBlock < 0)
                throw new ValidationException("Đơn giá mỗi block (PricePerBlock) không được là số âm.");

            if (rule.FromMinute < 0)
                throw new ValidationException("Số phút bắt đầu (FromMinute) không được là số âm.");

            if (rule.ToMinute.HasValue && rule.ToMinute.Value <= rule.FromMinute)
                throw new ValidationException($"Khoảng thời gian không hợp lệ: ToMinute ({rule.ToMinute.Value}) phải lớn hơn FromMinute ({rule.FromMinute}).");
        }

        // 4. Nếu bãi đang có bảng giá hiệu lực tại mốc thời gian này, tự động đóng hiệu lực bảng giá cũ để kích hoạt bảng giá mới
        var currentActive = await repo.GetCurrentActiveAsync(command.ParkingLotId, command.EffectiveFromUtc, cancellationToken);
        if (currentActive != null)
        {
            currentActive.EffectiveToUtc = command.EffectiveFromUtc;
            await repo.UpdateAsync(currentActive, cancellationToken);
        }

        // 5. Khởi tạo thực thể RateCard mới cùng danh sách RateRule
        var newCard = new RateCard
        {
            ParkingLotId = command.ParkingLotId,
            OwnerProfileId = command.OwnerProfileId,
            Name = command.Name.Trim(),
            EffectiveFromUtc = command.EffectiveFromUtc,
            EffectiveToUtc = command.EffectiveToUtc,
            IsActive = true,
            OvernightSurcharge = command.OvernightSurcharge,
            WeekendMultiplier = command.WeekendMultiplier,
            HolidayMultiplier = command.HolidayMultiplier,
            OversizedMultiplier = command.OversizedMultiplier,
            OverstayMultiplier = command.OverstayMultiplier,
            MaxDailyAmount = command.MaxDailyAmount
        };

        foreach (var r in command.Rules)
        {
            newCard.Rules.Add(new RateRule
            {
                VehicleType = r.VehicleType,
                FromMinute = r.FromMinute,
                ToMinute = r.ToMinute,
                BlockMinutes = r.BlockMinutes,
                PricePerBlock = r.PricePerBlock,
                SortOrder = r.SortOrder
            });
        }

        // 6. Lưu xuống cơ sở dữ liệu
        await repo.AddAsync(newCard, cancellationToken);

        // 7. Ánh xạ sang DTO trả về
        return MapToDto(newCard);
    }

    /// <summary>
    /// Phương thức tiện ích ánh xạ từ Entity sang DTO.
    /// </summary>
    public static RateCardDetailDto MapToDto(RateCard card)
    {
        return new RateCardDetailDto(
            card.Id,
            card.ParkingLotId,
            card.OwnerProfileId,
            card.Name,
            card.EffectiveFromUtc,
            card.EffectiveToUtc,
            card.IsActive,
            card.OvernightSurcharge,
            card.WeekendMultiplier,
            card.HolidayMultiplier,
            card.OversizedMultiplier,
            card.OverstayMultiplier,
            card.MaxDailyAmount,
            card.Rules.Select(r => new RateRuleDto(
                r.Id,
                r.VehicleType.ToString(),
                r.FromMinute,
                r.ToMinute,
                r.BlockMinutes,
                r.PricePerBlock,
                r.SortOrder
            )).ToList()
        );
    }
}

