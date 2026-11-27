using ParkingManagement.SharedKernel.Exceptions;
using PaymentService.Application.Interfaces;

namespace PaymentService.Application.Features.Holidays;

/// <summary>
/// Cổng ca sử dụng tra cứu danh sách hoặc xóa ngày nghỉ lễ.
/// </summary>
public interface IManageHolidaysUseCase
{
    /// <summary>
    /// Lấy danh sách toàn bộ ngày nghỉ lễ, có thể lọc theo năm.
    /// </summary>
    Task<IReadOnlyList<HolidayDto>> GetAllAsync(int? year, CancellationToken cancellationToken = default);

    /// <summary>
    /// Xóa một ngày nghỉ lễ khỏi hệ thống theo ID.
    /// </summary>
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);
}

/// <summary>
/// Cài đặt ca sử dụng tra cứu và xóa ngày lễ độc lập.
/// </summary>
public sealed class ManageHolidaysUseCase(IHolidayManagementRepository repo) : IManageHolidaysUseCase
{
    public async Task<IReadOnlyList<HolidayDto>> GetAllAsync(int? year, CancellationToken cancellationToken = default)
    {
        if (year.HasValue && (year.Value < 2000 || year.Value > 2100))
            throw new ValidationException("Năm tra cứu không hợp lệ (từ 2000 đến 2100).");

        var list = await repo.GetAllAsync(year, cancellationToken);
        return list.Select(h => new HolidayDto(h.Id, h.Date, h.Name, h.City)).ToList();
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        if (id <= 0)
            throw new ValidationException("Id phải là số nguyên dương.");

        var holiday = await repo.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException("Ngày lễ", id);

        await repo.DeleteAsync(holiday, cancellationToken);
    }
}

