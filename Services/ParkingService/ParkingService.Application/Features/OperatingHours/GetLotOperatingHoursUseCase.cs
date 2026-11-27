using System.Globalization;
using Microsoft.Extensions.Logging;
using ParkingManagement.SharedKernel.Exceptions;
using ParkingService.Application.Abstractions;
using ParkingService.Domain.Entities;

namespace ParkingService.Application.Features.OperatingHours;

public interface IGetLotOperatingHoursUseCase
{
    Task<LotOperatingHoursWeeklyDto> ExecuteAsync(
        int lotId,
        int? ownerProfileId = null,
        int? year = null,
        int? weekOfYear = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// USE CASE: Lấy lịch hoạt động của bãi đỗ xe theo 7 ngày trong tuần (US-062).
/// Hỗ trợ cả Lịch tuần cơ sở (Base recurring schedule) và Lịch ngoại lệ theo tuần cụ thể (Week overrides).
/// Quy tắc ưu tiên: Override tuần cụ thể > Lịch tuần cơ sở > Giờ mặc định của cơ sở bãi.
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

    public async Task<LotOperatingHoursWeeklyDto> ExecuteAsync(
        int lotId,
        int? ownerProfileId = null,
        int? year = null,
        int? weekOfYear = null,
        CancellationToken cancellationToken = default)
    {
        if (lotId <= 0)
            throw new ValidationException("Mã bãi đỗ (lotId) không hợp lệ.");

        var lot = await parkingLotRepository.GetByIdAsync(lotId, cancellationToken)
            ?? throw new NotFoundException("Bãi đỗ xe", lotId);

        if (ownerProfileId.HasValue && ownerProfileId.Value > 0 && lot.OwnerProfileId != ownerProfileId.Value)
        {
            throw new ConflictException("Bạn không có quyền truy cập thông tin bãi đỗ xe này.");
        }

        // 1. Lấy lịch tuần cơ sở (Base schedule)
        var configuredHours = await operatingHourRepository.GetByParkingLotIdAsync(lotId, trackChanges: false, cancellationToken);
        var baseMap = configuredHours.ToDictionary(h => h.DayOfWeek);

        // 2. Lấy danh sách override nếu có yêu cầu cho tuần cụ thể
        Dictionary<DayOfWeek, LotOperatingHourOverride> overrideMap = [];
        bool isCustomWeek = false;
        DateOnly? weekStartDate = null;
        DateOnly? weekEndDate = null;

        if (year.HasValue && weekOfYear.HasValue && year.Value > 2000 && weekOfYear.Value >= 1 && weekOfYear.Value <= 53)
        {
            var overrides = await operatingHourRepository.GetOverridesByWeekAsync(
                lotId, year.Value, weekOfYear.Value, trackChanges: false, cancellationToken);
            overrideMap = overrides.ToDictionary(o => o.DayOfWeek);
            isCustomWeek = overrides.Count > 0;

            try
            {
                weekStartDate = DateOnly.FromDateTime(ISOWeek.ToDateTime(year.Value, weekOfYear.Value, DayOfWeek.Monday));
                weekEndDate = DateOnly.FromDateTime(ISOWeek.ToDateTime(year.Value, weekOfYear.Value, DayOfWeek.Sunday));
            }
            catch
            {
                // Fallback nếu tuần không hợp lệ với năm
            }
        }

        var scheduleItems = new List<LotOperatingHourItemDto>(7);

        foreach (var day in OrderedDaysOfWeek)
        {
            if (overrideMap.TryGetValue(day, out var overrideHour))
            {
                // 1. Ưu tiên số 1: Lịch riêng tuần cụ thể (Override)
                var isOvernight = !overrideHour.IsClosed && overrideHour.OpenTime > overrideHour.CloseTime;
                var is24Hours = !overrideHour.IsClosed && overrideHour.OpenTime == overrideHour.CloseTime;

                scheduleItems.Add(new LotOperatingHourItemDto(
                    DayOfWeek: day,
                    DayOfWeekNumber: (int)day,
                    DayName: GetDayName(day),
                    OpenTime: overrideHour.OpenTime,
                    CloseTime: overrideHour.CloseTime,
                    IsClosed: overrideHour.IsClosed,
                    IsOvernight: isOvernight,
                    Is24Hours: is24Hours,
                    IsDefault: false,
                    IsOverride: true,
                    Reason: overrideHour.Reason));
            }
            else if (baseMap.TryGetValue(day, out var baseHour))
            {
                // 2. Ưu tiên số 2: Lịch cơ sở chuẩn của bãi
                var isOvernight = !baseHour.IsClosed && baseHour.OpenTime > baseHour.CloseTime;
                var is24Hours = !baseHour.IsClosed && baseHour.OpenTime == baseHour.CloseTime;

                scheduleItems.Add(new LotOperatingHourItemDto(
                    DayOfWeek: day,
                    DayOfWeekNumber: (int)day,
                    DayName: GetDayName(day),
                    OpenTime: baseHour.OpenTime,
                    CloseTime: baseHour.CloseTime,
                    IsClosed: baseHour.IsClosed,
                    IsOvernight: isOvernight,
                    Is24Hours: is24Hours,
                    IsDefault: false,
                    IsOverride: false,
                    Reason: null));
            }
            else
            {
                // 3. Fallback: Giờ mặc định của bãi đỗ xe
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
                    IsDefault: true,
                    IsOverride: false,
                    Reason: null));
            }
        }

        logger.LogInformation("Đã lấy lịch hoạt động cho bãi xe {LotId} ({LotName}) - Tuần: {Year}-W{Week} (Custom: {IsCustom})",
            lot.Id, lot.Name, year, weekOfYear, isCustomWeek);

        return new LotOperatingHoursWeeklyDto(
            ParkingLotId: lot.Id,
            ParkingLotName: lot.Name,
            DefaultOpenTime: lot.OpenTime,
            DefaultCloseTime: lot.CloseTime,
            Schedule: scheduleItems,
            Year: year,
            WeekOfYear: weekOfYear,
            IsCustomWeek: isCustomWeek,
            WeekStartDate: weekStartDate,
            WeekEndDate: weekEndDate);
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
