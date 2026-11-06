using Microsoft.Extensions.Logging;
using ParkingManagement.SharedKernel.Exceptions;
using ParkingService.Application.Abstractions;
using ParkingService.Domain.Entities;

namespace ParkingService.Application.Features.OperatingHours;

public interface IGetLotOperatingHoursUseCase
{
    Task<LotOperatingHoursWeeklyDto> ExecuteAsync(int lotId, int? ownerProfileId = null, CancellationToken cancellationToken = default);
}

/// <summary>
/// USE CASE: Lấy lịch hoạt động của bãi đỗ xe theo 7 ngày trong tuần (US-062).
/// Tự động ghép đủ 7 ngày (từ Thứ Hai đến Chủ Nhật). Nếu ngày nào chưa được cấu hình riêng,
/// hệ thống tự động kế thừa giờ mở/đóng cửa mặc định của bãi xe (IsDefault = true).
/// </summary>
public sealed class GetLotOperatingHoursUseCase(
    IParkingLotManagementRepository parkingLotRepository,
    ILotOperatingHourRepository operatingHourRepository,
    ILogger<GetLotOperatingHoursUseCase> logger) : IGetLotOperatingHoursUseCase
{
    private static readonly DayOfWeek[] OrderedDaysOfWeek =
    [
        DayOfWeek.Monday,
        DayOfWeek.Tuesday,
        DayOfWeek.Wednesday,
        DayOfWeek.Thursday,
        DayOfWeek.Friday,
        DayOfWeek.Saturday,
        DayOfWeek.Sunday
    ];

    public async Task<LotOperatingHoursWeeklyDto> ExecuteAsync(int lotId, int? ownerProfileId = null, CancellationToken cancellationToken = default)
    {
        if (lotId <= 0)
            throw new ValidationException("Mã bãi đỗ (lotId) không hợp lệ.");

        var lot = await parkingLotRepository.GetByIdAsync(lotId, cancellationToken)
            ?? throw new NotFoundException("Bãi đỗ xe", lotId);

        if (ownerProfileId.HasValue && ownerProfileId.Value > 0 && lot.OwnerProfileId != ownerProfileId.Value)
        {
            throw new ConflictException("Bạn không có quyền truy cập thông tin bãi đỗ xe này.");
        }

        var configuredHours = await operatingHourRepository.GetByParkingLotIdAsync(lotId, trackChanges: false, cancellationToken);
        var configuredMap = configuredHours.ToDictionary(h => h.DayOfWeek);

        var scheduleItems = new List<LotOperatingHourItemDto>(7);

        foreach (var day in OrderedDaysOfWeek)
        {
            if (configuredMap.TryGetValue(day, out var specific))
            {
                var isOvernight = !specific.IsClosed && specific.OpenTime > specific.CloseTime;
                var is24Hours = !specific.IsClosed && specific.OpenTime == specific.CloseTime;

                scheduleItems.Add(new LotOperatingHourItemDto(
                    DayOfWeek: day,
                    DayOfWeekNumber: (int)day,
                    DayName: GetDayName(day),
                    OpenTime: specific.OpenTime,
                    CloseTime: specific.CloseTime,
                    IsClosed: specific.IsClosed,
                    IsOvernight: isOvernight,
                    Is24Hours: is24Hours,
                    IsDefault: false));
            }
            else
            {
                // Fallback về giờ mặc định của bãi đỗ
                var isOvernight = lot.OpenTime > lot.CloseTime;
                var is24Hours = lot.OpenTime == lot.CloseTime;

                scheduleItems.Add(new LotOperatingHourItemDto(
                    DayOfWeek: day,
                    DayOfWeekNumber: (int)day,
                    DayName: GetDayName(day),
                    OpenTime: lot.OpenTime,
                    CloseTime: lot.CloseTime,
                    IsClosed: false,
                    IsOvernight: isOvernight,
                    Is24Hours: is24Hours,
                    IsDefault: true));
            }
        }

        logger.LogInformation("Đã lấy lịch hoạt động cho bãi xe {LotId} ({LotName}), gồm {ConfiguredCount}/7 ngày tùy chỉnh",
            lot.Id, lot.Name, configuredHours.Count);

        return new LotOperatingHoursWeeklyDto(
            ParkingLotId: lot.Id,
            ParkingLotName: lot.Name,
            DefaultOpenTime: lot.OpenTime,
            DefaultCloseTime: lot.CloseTime,
            Schedule: scheduleItems);
    }

    private static string GetDayName(DayOfWeek day) => day switch
    {
        DayOfWeek.Monday => "Thứ Hai",
        DayOfWeek.Tuesday => "Thứ Ba",
        DayOfWeek.Wednesday => "Thứ Tư",
        DayOfWeek.Thursday => "Thứ Năm",
        DayOfWeek.Friday => "Thứ Sáu",
        DayOfWeek.Saturday => "Thứ Bảy",
        DayOfWeek.Sunday => "Chủ Nhật",
        _ => day.ToString()
    };
}
