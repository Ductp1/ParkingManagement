using System.Reflection;
using ParkingManagement.SharedKernel.Domain;
using ParkingManagement.SharedKernel.Enums;
using ParkingManagement.SharedKernel.Exceptions;
using PaymentService.Application.Features.RateCards;
using PaymentService.Application.Interfaces;
using PaymentService.Domain.Entities;
using Xunit;

namespace PaymentService.Test;

/// <summary>
/// Bộ kiểm thử đơn vị cho các ca sử dụng quản lý cấu hình biểu giá đỗ xe và các mức phụ thu (US-070, US-071).
/// Quy tắc đặt tên phương thức kiểm thử: TênHàm_TrạngThái_KếtQuả.
/// </summary>
public class RateCardUseCaseTests
{
    /// <summary>
    /// Giả lập kho dữ liệu RateCard trong bộ nhớ RAM để cô lập tầng Application.
    /// </summary>
    private sealed class FakeRateCardManagementRepository : IRateCardManagementRepository
    {
        public readonly List<RateCard> RateCards = [];
        private int _idSequence = 1;

        public Task<IReadOnlyList<RateCard>> GetByParkingLotIdAsync(int parkingLotId, CancellationToken cancellationToken)
        {
            var list = RateCards.Where(c => c.ParkingLotId == parkingLotId).ToList();
            return Task.FromResult<IReadOnlyList<RateCard>>(list);
        }

        public Task<RateCard?> GetByIdAsync(int id, CancellationToken cancellationToken)
        {
            var found = RateCards.FirstOrDefault(c => c.Id == id);
            return Task.FromResult(found);
        }

        public Task<RateCard?> GetCurrentActiveAsync(int parkingLotId, DateTime atUtc, CancellationToken cancellationToken)
        {
            var found = RateCards.FirstOrDefault(c => c.ParkingLotId == parkingLotId && c.IsActive
                && c.EffectiveFromUtc <= atUtc && (c.EffectiveToUtc == null || c.EffectiveToUtc > atUtc));
            return Task.FromResult(found);
        }

        public Task AddAsync(RateCard rateCard, CancellationToken cancellationToken)
        {
            if (rateCard.Id == 0)
            {
                typeof(BaseEntity).GetProperty(nameof(BaseEntity.Id))!.SetValue(rateCard, _idSequence++);
            }

            var ruleIdSeq = 1;
            foreach (var r in rateCard.Rules)
            {
                if (r.Id == 0)
                {
                    typeof(BaseEntity).GetProperty(nameof(BaseEntity.Id))!.SetValue(r, ruleIdSeq++);
                }
            }

            RateCards.Add(rateCard);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(RateCard rateCard, CancellationToken cancellationToken)
        {
            var idx = RateCards.FindIndex(c => c.Id == rateCard.Id);
            if (idx >= 0)
            {
                RateCards[idx] = rateCard;
            }
            return Task.CompletedTask;
        }
    }

    private static CreateRateCardCommand CreateSampleCommand(
        int lotId = 1,
        int ownerId = 2,
        string name = "Bảng giá ngày thường",
        decimal overnightSurcharge = 30_000m,
        decimal weekendMultiplier = 1.2m,
        decimal holidayMultiplier = 1.5m,
        decimal overstayMultiplier = 1.5m,
        decimal oversizedMultiplier = 1.5m,
        List<CreateRateRuleRequest>? rules = null)
    {
        rules ??=
        [
            new(VehicleType.Sedan, 0, 60, 60, 30_000m, 1),
            new(VehicleType.Sedan, 60, 240, 60, 20_000m, 2),
            new(VehicleType.Sedan, 240, null, 60, 15_000m, 3)
        ];

        return new CreateRateCardCommand(
            lotId,
            ownerId,
            name,
            DateTime.UtcNow.AddHours(24),
            null,
            overnightSurcharge,
            weekendMultiplier,
            holidayMultiplier,
            oversizedMultiplier,
            overstayMultiplier,
            300_000m,
            rules);
    }

    [Fact]
    public async Task ExecuteAsync_ZeroParkingLotId_ThrowsValidationException()
    {
        // Arrange
        var repo = new FakeRateCardManagementRepository();
        var useCase = new CreateRateCardUseCase(repo);
        var command = CreateSampleCommand(lotId: 0);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(() => useCase.ExecuteAsync(command));
        Assert.Contains("ParkingLotId phải là số nguyên dương", ex.Message);
    }

    [Fact]
    public async Task ExecuteAsync_ZeroOwnerProfileId_ThrowsValidationException()
    {
        // Arrange
        var repo = new FakeRateCardManagementRepository();
        var useCase = new CreateRateCardUseCase(repo);
        var command = CreateSampleCommand(ownerId: 0);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(() => useCase.ExecuteAsync(command));
        Assert.Contains("OwnerProfileId phải là số nguyên dương", ex.Message);
    }

    [Fact]
    public async Task ExecuteAsync_EmptyName_ThrowsValidationException()
    {
        // Arrange
        var repo = new FakeRateCardManagementRepository();
        var useCase = new CreateRateCardUseCase(repo);
        var command = CreateSampleCommand(name: "   ");

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(() => useCase.ExecuteAsync(command));
        Assert.Contains("Tên bảng giá không được để trống", ex.Message);
    }

    [Fact]
    public async Task ExecuteAsync_NegativeOvernightSurcharge_ThrowsValidationException()
    {
        // Arrange
        var repo = new FakeRateCardManagementRepository();
        var useCase = new CreateRateCardUseCase(repo);
        var command = CreateSampleCommand(overnightSurcharge: -10_000m);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(() => useCase.ExecuteAsync(command));
        Assert.Contains("Phụ thu qua đêm không được là số âm", ex.Message);
    }

    [Theory]
    [InlineData(0.9)]
    [InlineData(3.5)]
    public async Task ExecuteAsync_InvalidWeekendMultiplier_ThrowsValidationException(decimal invalidMultiplier)
    {
        // Arrange
        var repo = new FakeRateCardManagementRepository();
        var useCase = new CreateRateCardUseCase(repo);
        var command = CreateSampleCommand(weekendMultiplier: invalidMultiplier);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(() => useCase.ExecuteAsync(command));
        Assert.Contains("Hệ số cuối tuần phải nằm trong khoảng từ 1.0 đến 3.0", ex.Message);
    }

    [Fact]
    public async Task ExecuteAsync_EmptyRules_ThrowsValidationException()
    {
        // Arrange
        var repo = new FakeRateCardManagementRepository();
        var useCase = new CreateRateCardUseCase(repo);
        var command = CreateSampleCommand(rules: []);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(() => useCase.ExecuteAsync(command));
        Assert.Contains("Bảng giá phải có ít nhất 1 quy tắc giá", ex.Message);
    }

    [Fact]
    public async Task ExecuteAsync_InvalidBlockMinutes_ThrowsValidationException()
    {
        // Arrange
        var repo = new FakeRateCardManagementRepository();
        var useCase = new CreateRateCardUseCase(repo);
        var rules = new List<CreateRateRuleRequest>
        {
            new(VehicleType.Sedan, 0, 60, 0, 30_000m, 1) // BlockMinutes = 0
        };
        var command = CreateSampleCommand(rules: rules);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(() => useCase.ExecuteAsync(command));
        Assert.Contains("Số phút của một block", ex.Message);
    }

    [Fact]
    public async Task ExecuteAsync_ToMinuteLessThanOrEqualToFromMinute_ThrowsValidationException()
    {
        // Arrange
        var repo = new FakeRateCardManagementRepository();
        var useCase = new CreateRateCardUseCase(repo);
        var rules = new List<CreateRateRuleRequest>
        {
            new(VehicleType.Sedan, 60, 60, 60, 30_000m, 1) // ToMinute = FromMinute
        };
        var command = CreateSampleCommand(rules: rules);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(() => useCase.ExecuteAsync(command));
        Assert.Contains("ToMinute (60) phải lớn hơn FromMinute (60)", ex.Message);
    }

    [Fact]
    public async Task ExecuteAsync_ValidCommand_CreatesRateCardWithRulesSuccessfully()
    {
        // Arrange
        var repo = new FakeRateCardManagementRepository();
        var useCase = new CreateRateCardUseCase(repo);
        var command = CreateSampleCommand();

        // Act
        var result = await useCase.ExecuteAsync(command);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Id > 0);
        Assert.Equal("Bảng giá ngày thường", result.Name);
        Assert.Equal(1.5m, result.OverstayMultiplier);
        Assert.Equal(1.2m, result.WeekendMultiplier);
        Assert.Equal(1.5m, result.HolidayMultiplier);
        Assert.Equal(30_000m, result.OvernightSurcharge);
        Assert.Equal(3, result.Rules.Count);
        Assert.Single(repo.RateCards);
    }

    [Fact]
    public async Task ExecuteAsync_ExistingActiveCard_ClosesEffectiveToUtcOfOldCard()
    {
        // Arrange: Bãi xe 1 đã có 1 bảng giá đang hiệu lực từ tháng trước
        var repo = new FakeRateCardManagementRepository();
        var oldCard = new RateCard
        {
            ParkingLotId = 1,
            OwnerProfileId = 2,
            Name = "Bảng giá cũ",
            EffectiveFromUtc = DateTime.UtcNow.AddDays(-10),
            EffectiveToUtc = null,
            IsActive = true
        };
        await repo.AddAsync(oldCard, default);

        var useCase = new CreateRateCardUseCase(repo);
        var command = CreateSampleCommand(lotId: 1); // Hiệu lực từ 2026-11-01

        // Act
        await useCase.ExecuteAsync(command);

        // Assert
        Assert.Equal(command.EffectiveFromUtc, oldCard.EffectiveToUtc);
        Assert.Equal(2, repo.RateCards.Count);
    }

    [Fact]
    public async Task GetByParkingLot_ZeroParkingLotId_ThrowsValidationException()
    {
        // Arrange
        var repo = new FakeRateCardManagementRepository();
        var useCase = new GetRateCardsByLotUseCase(repo);

        // Act & Assert
        await Assert.ThrowsAsync<ValidationException>(() => useCase.ExecuteAsync(0));
    }

    [Fact]
    public async Task GetByParkingLot_ValidLotId_ReturnsListOfRateCards()
    {
        // Arrange
        var repo = new FakeRateCardManagementRepository();
        var card = new RateCard
        {
            ParkingLotId = 1,
            OwnerProfileId = 2,
            Name = "Bảng giá mẫu",
            EffectiveFromUtc = DateTime.UtcNow,
            IsActive = true
        };
        await repo.AddAsync(card, default);

        var useCase = new GetRateCardsByLotUseCase(repo);

        // Act
        var result = await useCase.ExecuteAsync(1);

        // Assert
        Assert.Single(result);
        Assert.Equal("Bảng giá mẫu", result[0].Name);
    }

    [Fact]
    public async Task GetById_NotFound_ThrowsNotFoundException()
    {
        // Arrange
        var repo = new FakeRateCardManagementRepository();
        var useCase = new GetRateCardsByLotUseCase(repo);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() => useCase.GetByIdAsync(999));
    }

    [Fact]
    public async Task UpdateAsync_ValidCommand_UpdatesConfigurationSuccessfully()
    {
        // Arrange
        var repo = new FakeRateCardManagementRepository();
        var card = new RateCard
        {
            ParkingLotId = 1,
            OwnerProfileId = 2,
            Name = "Bảng giá gốc",
            OverstayMultiplier = 1.5m,
            WeekendMultiplier = 1.2m,
            HolidayMultiplier = 1.5m,
            EffectiveFromUtc = DateTime.UtcNow,
            IsActive = true
        };
        await repo.AddAsync(card, default);

        var useCase = new UpdateRateCardUseCase(repo);
        var command = new UpdateRateCardCommand(
            card.Id,
            "Bảng giá cập nhật mới",
            40_000m,
            1.3m,
            1.6m,
            1.5m,
            2.0m, // Tăng mức phạt quá giờ lên 2.0x
            400_000m,
            true);

        // Act
        var result = await useCase.ExecuteAsync(command);

        // Assert
        Assert.Equal("Bảng giá cập nhật mới", result.Name);
        Assert.Equal(2.0m, result.OverstayMultiplier);
        Assert.Equal(40_000m, result.OvernightSurcharge);
        Assert.Equal(1.3m, result.WeekendMultiplier);
        Assert.Equal(1.6m, result.HolidayMultiplier);
    }

    [Fact]
    public async Task UpdateAsync_InvalidMultiplier_ThrowsValidationException()
    {
        // Arrange
        var repo = new FakeRateCardManagementRepository();
        var card = new RateCard
        {
            ParkingLotId = 1,
            OwnerProfileId = 2,
            Name = "Bảng giá",
            EffectiveFromUtc = DateTime.UtcNow
        };
        await repo.AddAsync(card, default);

        var useCase = new UpdateRateCardUseCase(repo);
        var command = new UpdateRateCardCommand(
            card.Id,
            "Bảng giá",
            30_000m,
            1.2m,
            1.5m,
            1.5m,
            0.5m, // Nhỏ hơn 1.0 -> không hợp lệ
            null,
            true);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(() => useCase.ExecuteAsync(command));
        Assert.Contains("Hệ số phạt quá giờ phải nằm trong khoảng", ex.Message);
    }
}

