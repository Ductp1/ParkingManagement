using PaymentService.Domain.Entities;

namespace PaymentService.Application.Interfaces;

/// <summary>
/// Cổng lưu trữ và quản lý danh mục ngày nghỉ lễ phục vụ tính phụ thu giá đỗ xe (US-071).
/// </summary>
public interface IHolidayManagementRepository
{
    /// <summary>
    /// Lấy danh sách toàn bộ ngày nghỉ lễ, có thể lọc theo năm.
    /// </summary>
    Task<IReadOnlyList<Holiday>> GetAllAsync(int? year, CancellationToken cancellationToken);

    /// <summary>
    /// Tìm ngày nghỉ lễ theo khóa chính ID.
    /// </summary>
    Task<Holiday?> GetByIdAsync(int id, CancellationToken cancellationToken);

    /// <summary>
    /// Kiểm tra ngày nghỉ lễ đã tồn tại hay chưa (trùng ngày và thành phố).
    /// </summary>
    Task<bool> ExistsAsync(DateOnly date, string? city, CancellationToken cancellationToken);

    /// <summary>
    /// Thêm một ngày nghỉ lễ mới vào cơ sở dữ liệu.
    /// </summary>
    Task AddAsync(Holiday holiday, CancellationToken cancellationToken);

    /// <summary>
    /// Xóa ngày nghỉ lễ khỏi cơ sở dữ liệu.
    /// </summary>
    Task DeleteAsync(Holiday holiday, CancellationToken cancellationToken);
}

