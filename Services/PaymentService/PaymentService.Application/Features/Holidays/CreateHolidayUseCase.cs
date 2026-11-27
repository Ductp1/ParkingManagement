using ParkingManagement.SharedKernel.Exceptions;
using PaymentService.Application.Interfaces;
using PaymentService.Domain.Entities;

namespace PaymentService.Application.Features.Holidays;

/// <summary>
/// DTO chứa thông tin thêm mới ngày nghỉ lễ (US-071).
/// </summary>
/// <param name="Date">Ngày nghỉ lễ (định dạng YYYY-MM-DD).</param>
/// <param name="Name">Tên gọi của dịp lễ (ví dụ: Tết Nguyên Đán, Giỗ Tổ Hùng Vương).</param>
/// <param name="City">Thành phố áp dụng (để trống hoặc null nếu áp dụng trên toàn quốc).</param>
public sealed record CreateHolidayCommand(
    DateOnly Date,
    string Name,
    string? City = null);

/// <summary>
/// DTO trả về thông tin chi tiết của ngày lễ.
/// </summary>
public sealed record HolidayDto(
    int Id,
    DateOnly Date,
    string Name,
    string? City);

/// <summary>
/// Cổng ca sử dụng thêm mới ngày nghỉ lễ.
/// </summary>
public interface ICreateHolidayUseCase
{
    Task<HolidayDto> ExecuteAsync(CreateHolidayCommand command, CancellationToken cancellationToken = default);
}

/// <summary>
/// Cài đặt ca sử dụng thêm mới ngày lễ độc lập (US-071).
/// </summary>
public sealed class CreateHolidayUseCase(IHolidayManagementRepository repo) : ICreateHolidayUseCase
{
    public async Task<HolidayDto> ExecuteAsync(CreateHolidayCommand command, CancellationToken cancellationToken = default)
    {
        // 1. Kiểm tra tính hợp lệ cơ bản
        if (command.Date == default || command.Date.Year < 2000)
            throw new ValidationException("Ngày nghỉ lễ không hợp lệ hoặc bị thiếu.");

        if (string.IsNullOrWhiteSpace(command.Name))
            throw new ValidationException("Tên dịp lễ không được để trống.");

        // 2. Kiểm tra tránh trùng lặp ngày lễ cho cùng một phạm vi áp dụng
        var trimmedCity = string.IsNullOrWhiteSpace(command.City) ? null : command.City.Trim();
        var exists = await repo.ExistsAsync(command.Date, trimmedCity, cancellationToken);
        if (exists)
        {
            throw new ValidationException(
                $"Ngày lễ '{command.Date:yyyy-MM-dd}' {(trimmedCity != null ? $"tại {trimmedCity}" : "trên toàn quốc")} đã tồn tại trong hệ thống.");
        }

        // 3. Khởi tạo thực thể Holiday
        var holiday = new Holiday
        {
            Date = command.Date,
            Name = command.Name.Trim(),
            City = trimmedCity
        };

        // 4. Lưu vào Database
        await repo.AddAsync(holiday, cancellationToken);

        return new HolidayDto(holiday.Id, holiday.Date, holiday.Name, holiday.City);
    }
}

