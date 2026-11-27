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
/// Bộ kiểm thử đơn vị cho US-072: Kiểm soát thay đổi bảng giá.
/// Quy tắc kiểm thử: Hiệu lực sau 12h-24h (AC1), cảnh báo tăng > 30% (AC2), ghi Audit Log (AC3).
/// Tuân thủ quy tắc đặt tên phương thức kiểm thử: TênHàm_TrạngThái_KếtQuả.
/// </summary>
public class RateCardPriceControlTests
{
    private sealed class FakeRateCardManagementRepository : IRateCardManagementRepository
    {
        public readonly Dictionary<int, RateCard> Cards = [];
        public RateCard? CurrentActiveCard;

        public Task<RateCard?> GetByIdAsync(int id, CancellationToken cancellationToken)
        {
            Cards.TryGetValue(id, out var card);
            return Task.FromResult(card);
        }

        public Task<IReadOnlyList<RateCard>> GetByParkingLotIdAsync(int parkingLotId, CancellationToken cancellationToken)
        {
            var list = Cards.Values.Where(c => c.ParkingLotId == parkingLotId).ToList();
            return Task.FromResult<IReadOnlyList<RateCard>>(list);
        }

        public Task<RateCard?> GetCurrentActiveAsync(int parkingLotId, DateTime asOfUtc, CancellationToken cancellationToken)
        {
            return Task.FromResult(CurrentActiveCard);
        }

        public Task AddAsync(RateCard rateCard, CancellationToken cancellationToken)
        {
            var nextId = Cards.Count > 0 ? Cards.Keys.Max() + 1 : 1;
            typeof(BaseEntity).GetProperty(nameof(BaseEntity.Id))!.SetValue(rateCard, nextId);
            Cards[nextId] = rateCard;
            return Task.CompletedTask;
        }

        public Task UpdateAsync(RateCard rateCard, CancellationToken cancellationToken)
        {
            Cards[rateCard.Id] = rateCard;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeRateCardAuditLogRepository : IRateCardAuditLogRepository
    {
        public readonly List<RateCardAuditLog> SavedLogs = [];

        public Task AddAsync(RateCardAuditLog auditLog, CancellationToken cancellationToken)
        {
            SavedLogs.Add(auditLog);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<RateCardAuditLog>> GetByParkingLotIdAsync(int parkingLotId, CancellationToken cancellationToken)
        {
            var list = SavedLogs.Where(l => l.ParkingLotId == parkingLotId).ToList();
            return Task.FromResult<IReadOnlyList<RateCardAuditLog>>(list);
        }
    }

    private sealed class FakeTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    private static readonly DateTimeOffset FixedNow = new(2026, 11, 8, 8, 0, 0, TimeSpan.Zero);

    private static RateCard CreateActiveCard(
        int lotId = 1,
        decimal pricePerBlock = 10_000m,
        decimal overnight = 20_000m,
        decimal overstayMultiplier = 1.5m)
    {
        var card = new RateCard
        {
            ParkingLotId = lotId,
            OwnerProfileId = 2,
            Name = "Bảng giá hiện tại",
            EffectiveFromUtc = FixedNow.UtcDateTime.AddDays(-10),
            EffectiveToUtc = null,
            IsActive = true,
            OvernightSurcharge = overnight,
            OverstayMultiplier = overstayMultiplier,
            WeekendMultiplier = 1.0m,
            HolidayMultiplier = 1.0m,
            OversizedMultiplier = 1.5m
        };
        typeof(BaseEntity).GetProperty(nameof(BaseEntity.Id))!.SetValue(card, 10);
        card.Rules.Add(new RateRule
        {
            VehicleType = VehicleType.Sedan,
            FromMinute = 0,
            ToMinute = 60,
            BlockMinutes = 60,
            PricePerBlock = pricePerBlock,
            SortOrder = 1
        });
        return card;
    }

    [Fact]
    public async Task ExecuteAsync_EffectiveFromInThePast_ThrowsValidationException()
    {
        // Sắp xếp (Arrange)
        var repo = new FakeRateCardManagementRepository();
        var auditRepo = new FakeRateCardAuditLogRepository();
        var timeProvider = new FakeTimeProvider(FixedNow);
        var useCase = new CreateRateCardUseCase(repo, auditRepo, timeProvider);

        var pastTime = FixedNow.UtcDateTime.AddHours(-1); // Giờ trong quá khứ
        var command = new CreateRateCardCommand(
            1,
            2,
            "Bảng giá giờ quá khứ",
            pastTime,
            null,
            20_000m,
            1.0m,
            1.0m,
            1.5m,
            1.5m,
            null,
            [new(VehicleType.Sedan, 0, 60, 60, 10_000m, 1)]);

        // Hành động & Kiểm chứng (Act & Assert)
        var ex = await Assert.ThrowsAsync<ValidationException>(() => useCase.ExecuteAsync(command));
        Assert.Contains("không thể ở trong quá khứ", ex.Message);
    }

    [Fact]
    public async Task ExecuteAsync_EffectiveFromLessThan12Hours_ThrowsValidationException()
    {
        // Sắp xếp (Arrange)
        var repo = new FakeRateCardManagementRepository();
        var auditRepo = new FakeRateCardAuditLogRepository();
        var timeProvider = new FakeTimeProvider(FixedNow);
        var useCase = new CreateRateCardUseCase(repo, auditRepo, timeProvider);

        var only5HoursLater = FixedNow.UtcDateTime.AddHours(5); // Chỉ sau 5 tiếng (dưới 12 tiếng)
        var command = new CreateRateCardCommand(
            1,
            2,
            "Bảng giá gấp",
            only5HoursLater,
            null,
            20_000m,
            1.0m,
            1.0m,
            1.5m,
            1.5m,
            null,
            [new(VehicleType.Sedan, 0, 60, 60, 10_000m, 1)]);

        // Hành động & Kiểm chứng (Act & Assert)
        var ex = await Assert.ThrowsAsync<ValidationException>(() => useCase.ExecuteAsync(command));
        Assert.Contains("phải sau thời điểm hiện tại ít nhất 12 giờ", ex.Message);
    }

    [Fact]
    public async Task ExecuteAsync_EffectiveFromExactly12HoursOrMore_Success()
    {
        // Sắp xếp (Arrange)
        var repo = new FakeRateCardManagementRepository();
        var auditRepo = new FakeRateCardAuditLogRepository();
        var timeProvider = new FakeTimeProvider(FixedNow);
        var useCase = new CreateRateCardUseCase(repo, auditRepo, timeProvider);

        var validEffectiveTime = FixedNow.UtcDateTime.AddHours(14); // Sau 14 tiếng (hợp lệ)
        var command = new CreateRateCardCommand(
            1,
            2,
            "Bảng giá hợp lệ sau 14h",
            validEffectiveTime,
            null,
            20_000m,
            1.0m,
            1.0m,
            1.5m,
            1.5m,
            null,
            [new(VehicleType.Sedan, 0, 60, 60, 10_000m, 1)]);

        // Hành động (Act)
        var result = await useCase.ExecuteAsync(command);

        // Kiểm chứng (Assert)
        Assert.NotNull(result);
        Assert.Equal(validEffectiveTime, result.EffectiveFromUtc);
        Assert.False(result.RequiresAdminApproval);
        Assert.True(result.IsActive);
    }

    [Fact]
    public async Task ExecuteAsync_PriceIncreaseLessThanOrEqual30Percent_DoesNotRequireAdminApproval()
    {
        // Sắp xếp (Arrange): Giá cũ 10.000đ, giá mới 12.000đ (tăng 20% <= 30%)
        var repo = new FakeRateCardManagementRepository();
        repo.CurrentActiveCard = CreateActiveCard(lotId: 1, pricePerBlock: 10_000m);

        var auditRepo = new FakeRateCardAuditLogRepository();
        var timeProvider = new FakeTimeProvider(FixedNow);
        var useCase = new CreateRateCardUseCase(repo, auditRepo, timeProvider);

        var command = new CreateRateCardCommand(
            1,
            2,
            "Bảng giá tăng 20%",
            FixedNow.UtcDateTime.AddHours(15),
            null,
            20_000m,
            1.0m,
            1.0m,
            1.5m,
            1.5m,
            null,
            [new(VehicleType.Sedan, 0, 60, 60, 12_000m, 1)]);

        // Hành động (Act)
        var result = await useCase.ExecuteAsync(command);

        // Kiểm chứng (Assert)
        Assert.False(result.RequiresAdminApproval);
        Assert.True(result.IsActive);
        Assert.Equal(20m, result.MaxPriceIncreasePercentage);
        Assert.Null(result.WarningMessage);
    }

    [Fact]
    public async Task ExecuteAsync_PriceIncreaseGreaterThan30Percent_FlagsRequiresAdminApprovalAndInactive()
    {
        // Sắp xếp (Arrange): Giá cũ 10.000đ, giá mới 15.000đ (tăng 50% > 30%)
        var repo = new FakeRateCardManagementRepository();
        repo.CurrentActiveCard = CreateActiveCard(lotId: 1, pricePerBlock: 10_000m);

        var auditRepo = new FakeRateCardAuditLogRepository();
        var timeProvider = new FakeTimeProvider(FixedNow);
        var useCase = new CreateRateCardUseCase(repo, auditRepo, timeProvider);

        var command = new CreateRateCardCommand(
            1,
            2,
            "Bảng giá tăng 50%",
            FixedNow.UtcDateTime.AddHours(20),
            null,
            20_000m,
            1.0m,
            1.0m,
            1.5m,
            1.5m,
            null,
            [new(VehicleType.Sedan, 0, 60, 60, 15_000m, 1)]);

        // Hành động (Act)
        var result = await useCase.ExecuteAsync(command);

        // Kiểm chứng (Assert): Gắn cờ cần Admin duyệt và tạm thời IsActive = false
        Assert.True(result.RequiresAdminApproval);
        Assert.False(result.IsActive);
        Assert.Equal(50m, result.MaxPriceIncreasePercentage);
        Assert.NotNull(result.WarningMessage);
        Assert.Contains("50", result.WarningMessage);
        Assert.Contains("> 30%", result.WarningMessage);
    }

    [Fact]
    public async Task ExecuteAsync_OvernightSurchargeIncreaseGreaterThan30Percent_FlagsRequiresAdminApproval()
    {
        // Sắp xếp (Arrange): Phụ thu qua đêm cũ 20.000đ, mới 30.000đ (tăng 50%)
        var repo = new FakeRateCardManagementRepository();
        repo.CurrentActiveCard = CreateActiveCard(lotId: 1, overnight: 20_000m);

        var auditRepo = new FakeRateCardAuditLogRepository();
        var timeProvider = new FakeTimeProvider(FixedNow);
        var useCase = new CreateRateCardUseCase(repo, auditRepo, timeProvider);

        var command = new CreateRateCardCommand(
            1,
            2,
            "Bảng giá tăng phụ thu đêm",
            FixedNow.UtcDateTime.AddHours(16),
            null,
            30_000m,
            1.0m,
            1.0m,
            1.5m,
            1.5m,
            null,
            [new(VehicleType.Sedan, 0, 60, 60, 10_000m, 1)]);

        // Hành động (Act)
        var result = await useCase.ExecuteAsync(command);

        // Kiểm chứng (Assert)
        Assert.True(result.RequiresAdminApproval);
        Assert.False(result.IsActive);
        Assert.Equal(50m, result.MaxPriceIncreasePercentage);
    }

    [Fact]
    public async Task ExecuteAsync_OverstayMultiplierIncreaseGreaterThan30Percent_FlagsRequiresAdminApproval()
    {
        // Sắp xếp (Arrange): Hệ số phạt cũ 1.5, hệ số phạt mới 2.0 (tăng 33.33% > 30%)
        var repo = new FakeRateCardManagementRepository();
        repo.CurrentActiveCard = CreateActiveCard(lotId: 1, overstayMultiplier: 1.5m);

        var auditRepo = new FakeRateCardAuditLogRepository();
        var timeProvider = new FakeTimeProvider(FixedNow);
        var useCase = new CreateRateCardUseCase(repo, auditRepo, timeProvider);

        var command = new CreateRateCardCommand(
            1,
            2,
            "Bảng giá tăng hệ số phạt quá giờ",
            FixedNow.UtcDateTime.AddHours(18),
            null,
            20_000m,
            1.0m,
            1.0m,
            1.5m,
            2.0m, // 2.0 vs 1.5 -> tăng 33.33%
            null,
            [new(VehicleType.Sedan, 0, 60, 60, 10_000m, 1)]);

        // Hành động (Act)
        var result = await useCase.ExecuteAsync(command);

        // Kiểm chứng (Assert)
        Assert.True(result.RequiresAdminApproval);
        Assert.False(result.IsActive);
        Assert.Equal(33.33m, result.MaxPriceIncreasePercentage);
    }

    [Fact]
    public async Task ExecuteAsync_NoPreviousActiveCard_FirstCardSuccessWithoutWarning()
    {
        // Sắp xếp (Arrange): Lần đầu tạo bảng giá, chưa có giá cũ nào
        var repo = new FakeRateCardManagementRepository();
        repo.CurrentActiveCard = null;

        var auditRepo = new FakeRateCardAuditLogRepository();
        var timeProvider = new FakeTimeProvider(FixedNow);
        var useCase = new CreateRateCardUseCase(repo, auditRepo, timeProvider);

        var command = new CreateRateCardCommand(
            1,
            2,
            "Bảng giá khởi tạo đầu tiên",
            FixedNow.UtcDateTime.AddHours(15),
            null,
            20_000m,
            1.0m,
            1.0m,
            1.5m,
            1.5m,
            null,
            [new(VehicleType.Sedan, 0, 60, 60, 10_000m, 1)]);

        // Hành động (Act)
        var result = await useCase.ExecuteAsync(command);

        // Kiểm chứng (Assert)
        Assert.False(result.RequiresAdminApproval);
        Assert.True(result.IsActive);
        Assert.Equal(0m, result.MaxPriceIncreasePercentage);
    }

    [Fact]
    public async Task ExecuteAsync_CreatesAuditLogWithOldAndNewValuesJson()
    {
        // Sắp xếp (Arrange)
        var repo = new FakeRateCardManagementRepository();
        repo.CurrentActiveCard = CreateActiveCard(lotId: 1, pricePerBlock: 10_000m);

        var auditRepo = new FakeRateCardAuditLogRepository();
        var timeProvider = new FakeTimeProvider(FixedNow);
        var useCase = new CreateRateCardUseCase(repo, auditRepo, timeProvider);

        var command = new CreateRateCardCommand(
            1,
            2,
            "Bảng giá cập nhật",
            FixedNow.UtcDateTime.AddHours(14),
            null,
            20_000m,
            1.0m,
            1.0m,
            1.5m,
            1.5m,
            null,
            [new(VehicleType.Sedan, 0, 60, 60, 12_000m, 1)]);

        // Hành động (Act)
        await useCase.ExecuteAsync(command);

        // Kiểm chứng Audit Log đã lưu (Assert)
        Assert.Single(auditRepo.SavedLogs);
        var log = auditRepo.SavedLogs[0];
        Assert.Equal(1, log.ParkingLotId);
        Assert.Equal(2, log.ChangedByUserId);
        Assert.NotNull(log.OldValueJson);
        Assert.NotNull(log.NewValueJson);
        Assert.Contains("10000", log.OldValueJson);
        Assert.Contains("12000", log.NewValueJson);
    }

    [Fact]
    public async Task ExecuteAsync_ExistingActiveCardGetsEffectiveToUpdatedWhenNewCardActive()
    {
        // Sắp xếp (Arrange): Bảng giá cũ sẽ được cập nhật EffectiveToUtc bằng thời điểm bắt đầu giá mới
        var repo = new FakeRateCardManagementRepository();
        var activeCard = CreateActiveCard(lotId: 1, pricePerBlock: 10_000m);
        repo.CurrentActiveCard = activeCard;

        var auditRepo = new FakeRateCardAuditLogRepository();
        var timeProvider = new FakeTimeProvider(FixedNow);
        var useCase = new CreateRateCardUseCase(repo, auditRepo, timeProvider);

        var newEffectiveTime = FixedNow.UtcDateTime.AddHours(15);
        var command = new CreateRateCardCommand(
            1,
            2,
            "Bảng giá thay thế hợp lệ",
            newEffectiveTime,
            null,
            20_000m,
            1.0m,
            1.0m,
            1.5m,
            1.5m,
            null,
            [new(VehicleType.Sedan, 0, 60, 60, 11_000m, 1)]);

        // Hành động (Act)
        await useCase.ExecuteAsync(command);

        // Kiểm chứng (Assert): Giá cũ được đóng hiệu lực tại đúng mốc bắt đầu của giá mới (AC1)
        Assert.Equal(newEffectiveTime, activeCard.EffectiveToUtc);
    }
}
