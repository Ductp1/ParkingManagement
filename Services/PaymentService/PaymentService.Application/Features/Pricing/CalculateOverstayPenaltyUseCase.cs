using ParkingManagement.SharedKernel.Enums;
using ParkingManagement.SharedKernel.Exceptions;
using PaymentService.Domain.Entities;
using PaymentService.Domain.Services;

namespace PaymentService.Application.Features.Pricing;

/// <summary>
/// DTO chứa các tham số đầu vào để yêu cầu tính phí phụ thu quá giờ khi xe ra bãi trễ (US-047, UC-22).
/// Hỗ trợ cả 2 tên trường BookedEndAtUtc và ScheduledExitAtUtc để tương thích tối đa với Client / Postman / Kiosk.
/// </summary>
/// <param name="ParkingLotId">Mã định danh duy nhất của bãi đỗ xe.</param>
/// <param name="VehicleType">Phân loại phương tiện gửi xe (Sedan, SUV, Oversized...).</param>
/// <param name="BookedEndAtUtc">Thời điểm kết thúc đỗ xe theo đơn đặt chỗ ban đầu (UTC).</param>
/// <param name="ActualExitAtUtc">Thời điểm xe thực tế ra qua cổng (UTC).</param>
/// <param name="CustomOverstayMultiplier">Hệ số phạt ghi đè nếu có từ hợp đồng đặt chỗ ban đầu (từ 1.5 đến 2.0).</param>
/// <param name="GracePeriodMinutes">Số phút ân hạn cho phép ra trễ miễn phạt (mặc định 15 phút).</param>
/// <param name="ScheduledExitAtUtc">Tên gọi thay thế của BookedEndAtUtc tương thích với Kiosk và Postman.</param>
public sealed record CalculateOverstayPenaltyQuery(
    int ParkingLotId,
    string VehicleType,
    DateTime BookedEndAtUtc = default,
    DateTime ActualExitAtUtc = default,
    decimal? CustomOverstayMultiplier = null,
    int? GracePeriodMinutes = null,
    DateTime? ScheduledExitAtUtc = null)
{
    /// <summary>
    /// Thời điểm kết thúc đặt chỗ thực tế được suy luận linh hoạt từ BookedEndAtUtc hoặc ScheduledExitAtUtc.
    /// </summary>
    public DateTime EffectiveBookedEndAtUtc =>
        (BookedEndAtUtc != default && BookedEndAtUtc > DateTime.MinValue)
            ? BookedEndAtUtc
            : (ScheduledExitAtUtc ?? DateTime.MinValue);
}

/// <summary>
/// DTO trả về kết quả chi tiết phí phụ thu quá giờ để Kiosk cổng hoặc ứng dụng hiển thị minh bạch cho khách trước khi thu tiền (US-047 AC3).
/// </summary>
/// <param name="ParkingLotId">Mã định danh bãi xe.</param>
/// <param name="RateCardId">Mã biểu giá áp dụng tính cước.</param>
/// <param name="VehicleType">Loại phương tiện gửi xe.</param>
/// <param name="OverstayMinutes">Tổng số phút đỗ vượt quá mốc đã đặt.</param>
/// <param name="IsWithinGracePeriod">Đánh dấu lượt xe có nằm trong 15 phút ân hạn miễn phạt hay không.</param>
/// <param name="OverstayMultiplier">Hệ số phạt áp dụng thực tế.</param>
/// <param name="BaseRateAmount">Tiền cước tính theo đơn giá thông thường trước khi nhân hệ số.</param>
/// <param name="PenaltyAmount">Tổng số tiền phạt thực tế phải nộp.</param>
/// <param name="Description">Mô tả chi tiết công thức tính toán.</param>
/// <param name="Lines">Danh sách chi tiết từng dòng bậc giá theo block.</param>
public sealed record OverstayPenaltyDto(
    int ParkingLotId,
    int RateCardId,
    string VehicleType,
    int OverstayMinutes,
    bool IsWithinGracePeriod,
    decimal OverstayMultiplier,
    decimal BaseRateAmount,
    decimal PenaltyAmount,
    string Description,
    IReadOnlyList<PriceLine> Lines);

/// <summary>
/// Cổng (Port) giao diện ca sử dụng tính phí phụ thu quá giờ (US-047).
/// </summary>
public interface ICalculateOverstayPenaltyUseCase
{
    /// <summary>
    /// Thực thi tính toán phí phụ thu quá giờ khi xe ra bãi trễ so với giờ đã đặt.
    /// </summary>
    Task<OverstayPenaltyDto> ExecuteAsync(CalculateOverstayPenaltyQuery query, CancellationToken cancellationToken = default);
}

/// <summary>
/// Ca sử dụng tính phí phạt quá giờ độc lập theo nguyên lý Single Responsibility (US-047, UC-22).
/// </summary>
public sealed class CalculateOverstayPenaltyUseCase(IRateCardRepository rateCards) : ICalculateOverstayPenaltyUseCase
{
    public async Task<OverstayPenaltyDto> ExecuteAsync(CalculateOverstayPenaltyQuery query, CancellationToken cancellationToken = default)
    {
        var bookedEndUtc = query.EffectiveBookedEndAtUtc;

        // 1. Kiểm tra tính hợp lệ của tham số đầu vào
        if (query.ParkingLotId <= 0)
            throw new ValidationException("parkingLotId phải là số nguyên dương.");

        if (!Enum.TryParse<VehicleType>(query.VehicleType, ignoreCase: true, out var vehicleType))
            throw new ValidationException($"Loại xe '{query.VehicleType}' không hợp lệ.");

        if (bookedEndUtc <= DateTime.MinValue || bookedEndUtc.Year < 2000)
            throw new ValidationException("Thời điểm kết thúc đặt chỗ (bookedEndAtUtc hoặc scheduledExitAtUtc) không hợp lệ hoặc bị thiếu.");

        if (query.ActualExitAtUtc <= DateTime.MinValue || query.ActualExitAtUtc.Year < 2000)
            throw new ValidationException("Thời điểm ra thực tế (actualExitAtUtc) không hợp lệ hoặc bị thiếu.");

        if (query.ActualExitAtUtc < bookedEndUtc.AddDays(-1))
            throw new ValidationException("Thời gian ra thực tế không hợp lệ so với giờ kết thúc đặt chỗ.");

        // 2. Lấy biểu giá đang có hiệu lực của bãi xe tại thời điểm kết thúc đặt chỗ
        var card = await rateCards.GetActiveAsync(query.ParkingLotId, bookedEndUtc, cancellationToken)
            ?? throw new NotFoundException("Bảng giá của bãi xe", query.ParkingLotId);

        var graceMinutes = query.GracePeriodMinutes ?? PriceCalculator.DefaultGracePeriodMinutes;

        // 3. Gọi công cụ tính toán số học độc lập trong Domain
        var result = PriceCalculator.CalculateOverstay(
            card,
            vehicleType,
            bookedEndUtc,
            query.ActualExitAtUtc,
            query.CustomOverstayMultiplier,
            graceMinutes);

        return new OverstayPenaltyDto(
            query.ParkingLotId,
            card.Id,
            vehicleType.ToString(),
            result.OverstayMinutes,
            result.IsWithinGracePeriod,
            result.OverstayMultiplier,
            result.BaseRateAmount,
            result.PenaltyAmount,
            result.Description,
            result.Lines);
    }
}
