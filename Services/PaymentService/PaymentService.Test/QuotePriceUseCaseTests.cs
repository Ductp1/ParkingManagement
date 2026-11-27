using ParkingManagement.SharedKernel.Enums;
using ParkingManagement.SharedKernel.Exceptions;
using PaymentService.Application.Features;
using PaymentService.Domain.Entities;
using Xunit;

namespace PaymentService.Test;

/// <summary>
/// Bộ kiểm thử đơn vị cho ca sử dụng báo giá QuotePriceUseCase (Tầng Application - US-046, US-070, US-071).
/// Sử dụng lớp giả lập (Fake Repository) chạy trực tiếp trong bộ nhớ RAM, không phụ thuộc cơ sở dữ liệu.
/// </summary>
public class QuotePriceUseCaseTests
{
    private sealed class FakeRateCardRepository(RateCard? card) : IRateCardRepository
    {
        public Task<RateCard?> GetActiveAsync(int parkingLotId, DateTime atUtc, CancellationToken cancellationToken)
            => Task.FromResult(card);
    }

    private sealed class FakeHolidayRepository(IReadOnlyCollection<DateOnly>? holidays = null) : IHolidayRepository
    {
        public Task<IReadOnlyCollection<DateOnly>> GetHolidaysInRangeAsync(DateOnly fromDate, DateOnly toDate, CancellationToken cancellationToken)
            => Task.FromResult(holidays ?? (IReadOnlyCollection<DateOnly>)[]);
    }

    private static RateCard CreateSampleCard()
    {
        var card = new RateCard { ParkingLotId = 1, WeekendMultiplier = 1.2m, OvernightSurcharge = 50000m };
        card.Rules.Add(new RateRule { VehicleType = VehicleType.Sedan, FromMinute = 0, ToMinute = 60, BlockMinutes = 60, PricePerBlock = 30000m });
        return card;
    }

    [Fact]
    public async Task ExecuteAsync_NegativeParkingLotId_ThrowsValidationException()
    {
        var useCase = new QuotePriceUseCase(new FakeRateCardRepository(null), new FakeHolidayRepository());
        var query = new QuotePriceQuery(-1, VehicleType.Sedan, DateTime.UtcNow, DateTime.UtcNow.AddHours(2));

        var ex = await Assert.ThrowsAsync<ValidationException>(() => useCase.ExecuteAsync(query));
        Assert.Contains("parkingLotId phải là số nguyên dương", ex.Message);
    }

    [Fact]
    public async Task ExecuteAsync_EndBeforeStart_ThrowsValidationException()
    {
        var useCase = new QuotePriceUseCase(new FakeRateCardRepository(null), new FakeHolidayRepository());
        var now = DateTime.UtcNow;
        var query = new QuotePriceQuery(1, VehicleType.Sedan, now, now.AddHours(-1));

        var ex = await Assert.ThrowsAsync<ValidationException>(() => useCase.ExecuteAsync(query));
        Assert.Contains("Giờ kết thúc phải sau giờ bắt đầu", ex.Message);
    }

    [Fact]
    public async Task ExecuteAsync_DurationExceeds30Days_ThrowsValidationException()
    {
        var useCase = new QuotePriceUseCase(new FakeRateCardRepository(null), new FakeHolidayRepository());
        var now = DateTime.UtcNow;
        var query = new QuotePriceQuery(1, VehicleType.Sedan, now, now.AddDays(31));

        var ex = await Assert.ThrowsAsync<ValidationException>(() => useCase.ExecuteAsync(query));
        Assert.Contains("Thời gian đỗ tối đa 30 ngày", ex.Message);
    }

    [Fact]
    public async Task ExecuteAsync_NotFoundRateCard_ThrowsNotFoundException()
    {
        var useCase = new QuotePriceUseCase(new FakeRateCardRepository(null), new FakeHolidayRepository());
        var now = DateTime.UtcNow;
        var query = new QuotePriceQuery(99, VehicleType.Sedan, now, now.AddHours(2));

        await Assert.ThrowsAsync<NotFoundException>(() => useCase.ExecuteAsync(query));
    }

    [Fact]
    public async Task ExecuteAsync_ValidQuery_CalculatesAndReturnsPriceQuoteDto()
    {
        var card = CreateSampleCard();
        var useCase = new QuotePriceUseCase(new FakeRateCardRepository(card), new FakeHolidayRepository());
        var now = new DateTime(2026, 10, 2, 2, 0, 0, DateTimeKind.Utc); // Thứ Sáu 09:00 sáng VN
        var query = new QuotePriceQuery(1, VehicleType.Sedan, now, now.AddMinutes(45));

        var result = await useCase.ExecuteAsync(query);

        Assert.Equal(1, result.ParkingLotId);
        Assert.Equal(card.Id, result.RateCardId);
        Assert.Equal(30000m, result.FinalAmount);
        Assert.False(result.IsWithinGracePeriod);
    }
}
