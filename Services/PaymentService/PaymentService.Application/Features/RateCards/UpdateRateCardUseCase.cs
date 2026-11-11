using ParkingManagement.SharedKernel.Exceptions;
using PaymentService.Application.Interfaces;

namespace PaymentService.Application.Features.RateCards;

/// <summary>
/// DTO chứa các thông tin cập nhật cấu hình bảng giá và các mức phụ thu (US-070, US-071).
/// </summary>
public sealed record UpdateRateCardCommand(
    int Id,
    string Name,
    decimal OvernightSurcharge,
    decimal WeekendMultiplier,
    decimal HolidayMultiplier,
    decimal OversizedMultiplier,
    decimal OverstayMultiplier,
    decimal? MaxDailyAmount,
    bool IsActive);

/// <summary>
/// Cổng ca sử dụng cập nhật bảng giá.
/// </summary>
public interface IUpdateRateCardUseCase
{
    Task<RateCardDetailDto> ExecuteAsync(UpdateRateCardCommand command, CancellationToken cancellationToken = default);
}

/// <summary>
/// Cài đặt ca sử dụng cập nhật cấu hình bảng giá và mức phụ thu độc lập.
/// </summary>
public sealed class UpdateRateCardUseCase(IRateCardManagementRepository repo) : IUpdateRateCardUseCase
{
    public async Task<RateCardDetailDto> ExecuteAsync(UpdateRateCardCommand command, CancellationToken cancellationToken = default)
    {
        if (command.Id <= 0)
            throw new ValidationException("Id phải là số nguyên dương.");

        var card = await repo.GetByIdAsync(command.Id, cancellationToken)
            ?? throw new NotFoundException("Bảng giá", command.Id);

        if (string.IsNullOrWhiteSpace(command.Name))
            throw new ValidationException("Tên bảng giá không được để trống.");

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

        // Cập nhật các trường cấu hình của thực thể
        card.Name = command.Name.Trim();
        card.OvernightSurcharge = command.OvernightSurcharge;
        card.WeekendMultiplier = command.WeekendMultiplier;
        card.HolidayMultiplier = command.HolidayMultiplier;
        card.OversizedMultiplier = command.OversizedMultiplier;
        card.OverstayMultiplier = command.OverstayMultiplier;
        card.MaxDailyAmount = command.MaxDailyAmount;
        card.IsActive = command.IsActive;

        await repo.UpdateAsync(card, cancellationToken);

        return CreateRateCardUseCase.MapToDto(card);
    }
}

