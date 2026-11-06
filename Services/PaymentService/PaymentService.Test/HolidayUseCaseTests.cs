using System.Reflection;
using ParkingManagement.SharedKernel.Domain;
using ParkingManagement.SharedKernel.Exceptions;
using PaymentService.Application.Features.Holidays;
using PaymentService.Application.Interfaces;
using PaymentService.Domain.Entities;
using Xunit;

namespace PaymentService.Test;

/// <summary>
/// Bộ kiểm thử đơn vị cho các ca sử dụng quản lý ngày nghỉ lễ (Holiday) (US-071).
/// Quy tắc đặt tên phương thức kiểm thử: TênHàm_TrạngThái_KếtQuả.
/// </summary>
public class HolidayUseCaseTests
{
    private sealed class FakeHolidayManagementRepository : IHolidayManagementRepository
    {
        public readonly List<Holiday> Holidays = [];
        private int _idSeq = 1;

        public Task<IReadOnlyList<Holiday>> GetAllAsync(int? year, CancellationToken cancellationToken)
        {
            var query = Holidays.AsEnumerable();
            if (year.HasValue)
            {
                query = query.Where(h => h.Date.Year == year.Value);
            }
            return Task.FromResult<IReadOnlyList<Holiday>>(query.OrderBy(h => h.Date).ToList());
        }

        public Task<Holiday?> GetByIdAsync(int id, CancellationToken cancellationToken)
        {
            var found = Holidays.FirstOrDefault(h => h.Id == id);
            return Task.FromResult(found);
        }

        public Task<bool> ExistsAsync(DateOnly date, string? city, CancellationToken cancellationToken)
        {
            var exists = Holidays.Any(h => h.Date == date && h.City == city);
            return Task.FromResult(exists);
        }

        public Task AddAsync(Holiday holiday, CancellationToken cancellationToken)
        {
            if (holiday.Id == 0)
            {
                typeof(BaseEntity).GetProperty(nameof(BaseEntity.Id))!.SetValue(holiday, _idSeq++);
            }
            Holidays.Add(holiday);
            return Task.CompletedTask;
        }

        public Task DeleteAsync(Holiday holiday, CancellationToken cancellationToken)
        {
            Holidays.Remove(holiday);
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task CreateAsync_DefaultDate_ThrowsValidationException()
    {
        // Arrange
        var repo = new FakeHolidayManagementRepository();
        var useCase = new CreateHolidayUseCase(repo);
        var command = new CreateHolidayCommand(default, "Tết Dương Lịch");

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(() => useCase.ExecuteAsync(command));
        Assert.Contains("Ngày nghỉ lễ không hợp lệ", ex.Message);
    }

    [Fact]
    public async Task CreateAsync_EmptyName_ThrowsValidationException()
    {
        // Arrange
        var repo = new FakeHolidayManagementRepository();
        var useCase = new CreateHolidayUseCase(repo);
        var command = new CreateHolidayCommand(new DateOnly(2026, 1, 1), "   ");

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(() => useCase.ExecuteAsync(command));
        Assert.Contains("Tên dịp lễ không được để trống", ex.Message);
    }

    [Fact]
    public async Task CreateAsync_DuplicateHoliday_ThrowsValidationException()
    {
        // Arrange
        var repo = new FakeHolidayManagementRepository();
        await repo.AddAsync(new Holiday { Date = new DateOnly(2026, 1, 1), Name = "Tết Dương lịch", City = null }, default);

        var useCase = new CreateHolidayUseCase(repo);
        var command = new CreateHolidayCommand(new DateOnly(2026, 1, 1), "Tết Tây", null);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(() => useCase.ExecuteAsync(command));
        Assert.Contains("đã tồn tại trong hệ thống", ex.Message);
    }

    [Fact]
    public async Task CreateAsync_ValidCommand_CreatesHolidaySuccessfully()
    {
        // Arrange
        var repo = new FakeHolidayManagementRepository();
        var useCase = new CreateHolidayUseCase(repo);
        var command = new CreateHolidayCommand(new DateOnly(2027, 4, 30), "Ngày Giải phóng Miền Nam", null);

        // Act
        var result = await useCase.ExecuteAsync(command);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Id > 0);
        Assert.Equal(new DateOnly(2027, 4, 30), result.Date);
        Assert.Equal("Ngày Giải phóng Miền Nam", result.Name);
        Assert.Single(repo.Holidays);
    }

    [Fact]
    public async Task GetAllAsync_InvalidYear_ThrowsValidationException()
    {
        // Arrange
        var repo = new FakeHolidayManagementRepository();
        var useCase = new ManageHolidaysUseCase(repo);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(() => useCase.GetAllAsync(1990));
        Assert.Contains("Năm tra cứu không hợp lệ", ex.Message);
    }

    [Fact]
    public async Task GetAllAsync_FilterByYear_ReturnsMatchingHolidays()
    {
        // Arrange
        var repo = new FakeHolidayManagementRepository();
        await repo.AddAsync(new Holiday { Date = new DateOnly(2026, 1, 1), Name = "Năm 2026" }, default);
        await repo.AddAsync(new Holiday { Date = new DateOnly(2027, 1, 1), Name = "Năm 2027" }, default);

        var useCase = new ManageHolidaysUseCase(repo);

        // Act
        var result = await useCase.GetAllAsync(2026);

        // Assert
        Assert.Single(result);
        Assert.Equal("Năm 2026", result[0].Name);
    }

    [Fact]
    public async Task DeleteAsync_NotFound_ThrowsNotFoundException()
    {
        // Arrange
        var repo = new FakeHolidayManagementRepository();
        var useCase = new ManageHolidaysUseCase(repo);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() => useCase.DeleteAsync(999));
    }

    [Fact]
    public async Task DeleteAsync_ExistingId_DeletesHolidaySuccessfully()
    {
        // Arrange
        var repo = new FakeHolidayManagementRepository();
        var holiday = new Holiday { Date = new DateOnly(2026, 1, 1), Name = "Tết" };
        await repo.AddAsync(holiday, default);

        var useCase = new ManageHolidaysUseCase(repo);

        // Act
        await useCase.DeleteAsync(holiday.Id);

        // Assert
        Assert.Empty(repo.Holidays);
    }
}

