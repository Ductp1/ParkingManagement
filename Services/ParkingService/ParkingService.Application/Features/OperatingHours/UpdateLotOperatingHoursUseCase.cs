using Microsoft.Extensions.Logging;
using ParkingManagement.SharedKernel.Exceptions;
using ParkingService.Application.Abstractions;
using ParkingService.Domain.Entities;

namespace ParkingService.Application.Features.OperatingHours;

public interface IUpdateLotOperatingHoursUseCase
{
    Task<LotOperatingHoursWeeklyDto> ExecuteAsync(UpdateLotOperatingHoursCommand command, CancellationToken cancellationToken = default);
}

/// <summary>
/// USE CASE: Cập nhật lịch hoạt động các ngày trong tuần cho bãi đỗ xe (US-062).
/// Hỗ trợ kiểm tra quyền sở hữu, validate tính hợp lệ của danh sách ngày (không trùng lặp, thời gian hợp lệ),
/// và đồng bộ (upsert/replace) vào cơ sở dữ liệu.
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

        // 3. Chuẩn bị entities để đồng bộ vào repository
        var entities = command.Schedule.Select(item => new LotOperatingHour
        {
            ParkingLotId = command.ParkingLotId,
            DayOfWeek = item.DayOfWeek,
            OpenTime = item.IsClosed ? new TimeOnly(0, 0) : item.OpenTime,
            CloseTime = item.IsClosed ? new TimeOnly(0, 0) : item.CloseTime,
            IsClosed = item.IsClosed
        }).ToList();

        await operatingHourRepository.ReplaceScheduleAsync(command.ParkingLotId, entities, cancellationToken);
        await operatingHourRepository.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Đã cập nhật lịch hoạt động cho bãi xe {LotId} ({LotName}) với {Count} ngày",
            lot.Id, lot.Name, entities.Count);

        // 4. Trả về lịch 7 ngày hoàn chỉnh đã được chuẩn hóa
        return await getLotOperatingHoursUseCase.ExecuteAsync(command.ParkingLotId, command.OwnerProfileId, cancellationToken);
    }
}
