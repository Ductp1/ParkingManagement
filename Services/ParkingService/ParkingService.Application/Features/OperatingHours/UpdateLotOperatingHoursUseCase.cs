using Microsoft.Extensions.Logging;
using ParkingManagement.SharedKernel.Exceptions;
using ParkingService.Application.Abstractions;
using ParkingService.Domain.Entities;

namespace ParkingService.Application.Features.OperatingHours;

public interface IUpdateLotOperatingHoursUseCase
{
    Task<LotOperatingHoursWeeklyDto> ExecuteAsync(UpdateLotOperatingHoursCommand command, CancellationToken cancellationToken = default);
}

public interface IDeleteLotOperatingHoursOverrideUseCase
{
    Task<LotOperatingHoursWeeklyDto> ExecuteAsync(int lotId, int year, int weekOfYear, int? ownerProfileId = null, CancellationToken cancellationToken = default);
}

/// <summary>
/// USE CASE: Cập nhật lịch hoạt động các ngày trong tuần cho bãi đỗ xe (US-062).
/// Hỗ trợ cả cập nhật Lịch tuần cơ sở và Lịch riêng theo tuần cụ thể (Week Overrides).
/// </summary>
public sealed class UpdateLotOperatingHoursUseCase(
    IParkingLotManagementRepository parkingLotRepository,
    ILotOperatingHourRepository operatingHourRepository,
    IGetLotOperatingHoursUseCase getLotOperatingHoursUseCase,
    ILogger<UpdateLotOperatingHoursUseCase> logger) : IUpdateLotOperatingHoursUseCase
{
    public async Task<LotOperatingHoursWeeklyDto> ExecuteAsync(UpdateLotOperatingHoursCommand command, CancellationToken cancellationToken = default)
    {
        if (command.ParkingLotId <= 0)
            throw new ValidationException("Mã bãi đỗ (ParkingLotId) không hợp lệ.");

        if (command.Schedule == null)
            throw new ValidationException("Danh sách lịch hoạt động (Schedule) không được để trống.");

        var lot = await parkingLotRepository.GetByIdAsync(command.ParkingLotId, cancellationToken)
            ?? throw new NotFoundException("Bãi đỗ xe", command.ParkingLotId);

        if (command.OwnerProfileId.HasValue && command.OwnerProfileId.Value > 0 && lot.OwnerProfileId != command.OwnerProfileId.Value)
        {
            throw new ConflictException("Bạn không có quyền chỉnh sửa cấu hình của bãi đỗ xe này.");
        }

        // 1. Kiểm tra tính hợp lệ của từng item
        foreach (var item in command.Schedule)
        {
            if (!Enum.IsDefined(typeof(DayOfWeek), item.DayOfWeek))
                throw new ValidationException($"Thứ trong tuần '{item.DayOfWeek}' không hợp lệ.");
        }

        // 2. Kiểm tra không được trùng lặp DayOfWeek trong request
        var duplicateDays = command.Schedule
            .GroupBy(s => s.DayOfWeek)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        if (duplicateDays.Count > 0)
        {
            throw new ValidationException($"Lịch hoạt động chứa các thứ bị trùng lặp: {string.Join(", ", duplicateDays)}.");
        }

        // 3. Phân nhánh: Lưu lịch riêng cho tuần cụ thể (Overrides) hoặc Lịch tuần cơ sở
        if (command.Year.HasValue && command.WeekOfYear.HasValue)
        {
            if (command.Year.Value <= 2000 || command.WeekOfYear.Value < 1 || command.WeekOfYear.Value > 53)
            {
                throw new ValidationException("Thông tin năm hoặc tuần trong năm không hợp lệ.");
            }

            var overrideEntities = command.Schedule.Select(item => new LotOperatingHourOverride
            {
                ParkingLotId = command.ParkingLotId,
                Year = command.Year.Value,
                WeekOfYear = command.WeekOfYear.Value,
                DayOfWeek = item.DayOfWeek,
                OpenTime = item.OpenTime,
                CloseTime = item.CloseTime,
                IsClosed = item.IsClosed,
                Reason = command.Reason
            }).ToList();

            await operatingHourRepository.ReplaceWeekOverridesAsync(
                command.ParkingLotId, command.Year.Value, command.WeekOfYear.Value, overrideEntities, cancellationToken);
            await operatingHourRepository.SaveChangesAsync(cancellationToken);

            logger.LogInformation("Đã cập nhật lịch ngoại lệ tuần {Year}-W{Week} cho bãi xe {LotId} ({LotName}) với {Count} ngày",
                command.Year.Value, command.WeekOfYear.Value, lot.Id, lot.Name, overrideEntities.Count);

            return await getLotOperatingHoursUseCase.ExecuteAsync(
                command.ParkingLotId, command.OwnerProfileId, command.Year, command.WeekOfYear, cancellationToken);
        }
        else
        {
            var entities = command.Schedule.Select(item => new LotOperatingHour
            {
                ParkingLotId = command.ParkingLotId,
                DayOfWeek = item.DayOfWeek,
                OpenTime = item.OpenTime,
                CloseTime = item.CloseTime,
                IsClosed = item.IsClosed
            }).ToList();

            await operatingHourRepository.ReplaceScheduleAsync(command.ParkingLotId, entities, cancellationToken);
            await operatingHourRepository.SaveChangesAsync(cancellationToken);

            logger.LogInformation("Đã cập nhật lịch hoạt động cơ sở cho bãi xe {LotId} ({LotName}) với {Count} ngày",
                lot.Id, lot.Name, entities.Count);

            return await getLotOperatingHoursUseCase.ExecuteAsync(
                command.ParkingLotId, command.OwnerProfileId, null, null, cancellationToken);
        }
    }
}

/// <summary>
/// USE CASE: Xóa cấu hình ngoại lệ của một tuần cụ thể để khôi phục về lịch cơ sở.
/// </summary>
public sealed class DeleteLotOperatingHoursOverrideUseCase(
    IParkingLotManagementRepository parkingLotRepository,
    ILotOperatingHourRepository operatingHourRepository,
    IGetLotOperatingHoursUseCase getLotOperatingHoursUseCase,
    ILogger<DeleteLotOperatingHoursOverrideUseCase> logger) : IDeleteLotOperatingHoursOverrideUseCase
{
    public async Task<LotOperatingHoursWeeklyDto> ExecuteAsync(
        int lotId, int year, int weekOfYear, int? ownerProfileId = null, CancellationToken cancellationToken = default)
    {
        if (lotId <= 0) throw new ValidationException("Mã bãi đỗ không hợp lệ.");
        var lot = await parkingLotRepository.GetByIdAsync(lotId, cancellationToken)
            ?? throw new NotFoundException("Bãi đỗ xe", lotId);

        if (ownerProfileId.HasValue && ownerProfileId.Value > 0 && lot.OwnerProfileId != ownerProfileId.Value)
        {
            throw new ConflictException("Bạn không có quyền chỉnh sửa cấu hình của bãi đỗ xe này.");
        }

        await operatingHourRepository.DeleteWeekOverridesAsync(lotId, year, weekOfYear, cancellationToken);
        await operatingHourRepository.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Đã xóa lịch riêng tuần {Year}-W{Week} cho bãi xe {LotId}, khôi phục về lịch cơ sở",
            year, weekOfYear, lotId);

        return await getLotOperatingHoursUseCase.ExecuteAsync(lotId, ownerProfileId, year, weekOfYear, cancellationToken);
    }
}
