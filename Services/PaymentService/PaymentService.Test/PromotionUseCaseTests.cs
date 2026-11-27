using System.Reflection;
using ParkingManagement.SharedKernel.Domain;
using ParkingManagement.SharedKernel.Enums;
using ParkingManagement.SharedKernel.Exceptions;
using PaymentService.Application.Features.Promotions;
using PaymentService.Application.Interfaces;
using PaymentService.Domain.Entities;
using Xunit;

namespace PaymentService.Test;

/// <summary>
/// Bộ kiểm thử đơn vị cho chức năng Quản lý & Áp dụng Mã khuyến mãi (US-073).
/// Tuân thủ quy chuẩn: TênHàm_TrạngThái_KếtQuả và cô lập hoàn toàn trên bộ nhớ RAM.
/// </summary>
public class PromotionUseCaseTests
{
    private sealed class FakePromotionRepository : IPromotionRepository
    {
        public readonly Dictionary<int, Promotion> Promotions = [];
        public readonly List<PromotionRedemption> Redemptions = [];
        private int _idSeq = 1;

        public Task<Promotion?> GetByCodeAsync(string code, CancellationToken cancellationToken = default)
        {
            var normalized = code.Trim().ToUpperInvariant();
            var found = Promotions.Values.FirstOrDefault(p => p.Code == normalized);
            return Task.FromResult(found);
        }

        public Task<Promotion?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        {
            Promotions.TryGetValue(id, out var found);
            return Task.FromResult(found);
        }

        public Task<IReadOnlyList<Promotion>> ListAsync(
            int? parkingLotId = null, 
            bool? isActiveOnly = null, 
            CancellationToken cancellationToken = default)
        {
            var query = Promotions.Values.AsEnumerable();

            if (parkingLotId.HasValue)
            {
                query = query.Where(p => p.ParkingLotId == parkingLotId.Value || p.ParkingLotId == null);
            }

            if (isActiveOnly.HasValue && isActiveOnly.Value)
            {
                query = query.Where(p => p.IsActive);
            }

            var result = (IReadOnlyList<Promotion>)query.OrderByDescending(p => p.Id).ToList();
            return Task.FromResult(result);
        }

        public Task AddAsync(Promotion promotion, CancellationToken cancellationToken = default)
        {
            if (promotion.Id == 0)
            {
                typeof(BaseEntity).GetProperty(nameof(BaseEntity.Id))!.SetValue(promotion, _idSeq++);
            }
            Promotions[promotion.Id] = promotion;
            return Task.CompletedTask;
        }

        public Task UpdateAsync(Promotion promotion, CancellationToken cancellationToken = default)
        {
            Promotions[promotion.Id] = promotion;
            return Task.CompletedTask;
        }

        public Task<bool> ExistsByCodeAsync(string code, int? excludeId = null, CancellationToken cancellationToken = default)
        {
            var normalized = code.Trim().ToUpperInvariant();
            var exists = Promotions.Values.Any(p => p.Code == normalized && (!excludeId.HasValue || p.Id != excludeId.Value));
            return Task.FromResult(exists);
        }

        public Task AddRedemptionAsync(PromotionRedemption redemption, CancellationToken cancellationToken = default)
        {
            Redemptions.Add(redemption);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    private static readonly DateTimeOffset FixedNow = new(2026, 11, 8, 12, 0, 0, TimeSpan.Zero);

    private static CreatePromotionCommand CreateValidCommand(
        string code = "CHAOBANMOI",
        string name = "Giảm giá 20% cho tài xế mới",
        int? lotId = null,
        int? ownerId = null,
        PromotionSponsor sponsor = PromotionSponsor.Platform,
        DiscountType discountType = DiscountType.Percentage,
        decimal discountValue = 20m,
        decimal? maxDiscount = 50_000m,
        decimal? minOrder = 30_000m,
        DateTime? startsAt = null,
        DateTime? endsAt = null,
        int? usageLimit = 100)
    {
        return new CreatePromotionCommand(
            code,
            name,
            lotId,
            ownerId,
            sponsor,
            discountType,
            discountValue,
            maxDiscount,
            minOrder,
            startsAt ?? FixedNow.UtcDateTime.AddDays(-1),
            endsAt ?? FixedNow.UtcDateTime.AddDays(30),
            usageLimit);
    }

    // ==========================================
    // NHÓM 1: KIỂM THỬ TẠO MỚI (CREATE PROMOTION)
    // ==========================================

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task CreateAsync_EmptyCode_ThrowsValidationException(string? invalidCode)
    {
        var repo = new FakePromotionRepository();
        var useCase = new CreatePromotionUseCase(repo);
        var command = CreateValidCommand(code: invalidCode!);

        var ex = await Assert.ThrowsAsync<ValidationException>(() => useCase.ExecuteAsync(command));
        Assert.Contains("Mã khuyến mãi (Code) không được để trống", ex.Message);
    }

    [Fact]
    public async Task CreateAsync_CodeExceeds30Chars_ThrowsValidationException()
    {
        var repo = new FakePromotionRepository();
        var useCase = new CreatePromotionUseCase(repo);
        var command = CreateValidCommand(code: new string('A', 31));

        var ex = await Assert.ThrowsAsync<ValidationException>(() => useCase.ExecuteAsync(command));
        Assert.Contains("không được vượt quá 30 ký tự", ex.Message);
    }

    [Fact]
    public async Task CreateAsync_EmptyName_ThrowsValidationException()
    {
        var repo = new FakePromotionRepository();
        var useCase = new CreatePromotionUseCase(repo);
        var command = CreateValidCommand(name: "   ");

        var ex = await Assert.ThrowsAsync<ValidationException>(() => useCase.ExecuteAsync(command));
        Assert.Contains("Tên chương trình khuyến mãi không được để trống", ex.Message);
    }

    [Fact]
    public async Task CreateAsync_EndsAtBeforeStartsAt_ThrowsValidationException()
    {
        var repo = new FakePromotionRepository();
        var useCase = new CreatePromotionUseCase(repo);
        var command = CreateValidCommand(
            startsAt: FixedNow.UtcDateTime.AddDays(5),
            endsAt: FixedNow.UtcDateTime.AddDays(2));

        var ex = await Assert.ThrowsAsync<ValidationException>(() => useCase.ExecuteAsync(command));
        Assert.Contains("Thời điểm kết thúc (EndsAtUtc) phải lớn hơn thời điểm bắt đầu", ex.Message);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public async Task CreateAsync_ZeroOrNegativeDiscountValue_ThrowsValidationException(decimal invalidValue)
    {
        var repo = new FakePromotionRepository();
        var useCase = new CreatePromotionUseCase(repo);
        var command = CreateValidCommand(discountValue: invalidValue);

        var ex = await Assert.ThrowsAsync<ValidationException>(() => useCase.ExecuteAsync(command));
        Assert.Contains("Giá trị giảm giá phải là số dương lớn hơn 0", ex.Message);
    }

    [Fact]
    public async Task CreateAsync_PercentageDiscountExceeds100_ThrowsValidationException()
    {
        var repo = new FakePromotionRepository();
        var useCase = new CreatePromotionUseCase(repo);
        var command = CreateValidCommand(discountType: DiscountType.Percentage, discountValue: 120m);

        var ex = await Assert.ThrowsAsync<ValidationException>(() => useCase.ExecuteAsync(command));
        Assert.Contains("Giảm giá theo phần trăm không được vượt quá 100%", ex.Message);
    }

    [Fact]
    public async Task CreateAsync_NegativeMaxDiscountAmount_ThrowsValidationException()
    {
        var repo = new FakePromotionRepository();
        var useCase = new CreatePromotionUseCase(repo);
        var command = CreateValidCommand(maxDiscount: -5000m);

        var ex = await Assert.ThrowsAsync<ValidationException>(() => useCase.ExecuteAsync(command));
        Assert.Contains("Mức giảm tối đa không được là số âm", ex.Message);
    }

    [Fact]
    public async Task CreateAsync_NegativeMinOrderAmount_ThrowsValidationException()
    {
        var repo = new FakePromotionRepository();
        var useCase = new CreatePromotionUseCase(repo);
        var command = CreateValidCommand(minOrder: -1000m);

        var ex = await Assert.ThrowsAsync<ValidationException>(() => useCase.ExecuteAsync(command));
        Assert.Contains("Giá trị đơn tối thiểu không được là số âm", ex.Message);
    }

    [Fact]
    public async Task CreateAsync_ZeroOrNegativeUsageLimit_ThrowsValidationException()
    {
        var repo = new FakePromotionRepository();
        var useCase = new CreatePromotionUseCase(repo);
        var command = CreateValidCommand(usageLimit: 0);

        var ex = await Assert.ThrowsAsync<ValidationException>(() => useCase.ExecuteAsync(command));
        Assert.Contains("Số lượt sử dụng tối đa phải lớn hơn 0", ex.Message);
    }

    [Fact]
    public async Task CreateAsync_LotOwnerSponsorWithoutLotId_ThrowsValidationException()
    {
        var repo = new FakePromotionRepository();
        var useCase = new CreatePromotionUseCase(repo);
        var command = CreateValidCommand(
            sponsor: PromotionSponsor.LotOwner,
            lotId: null,
            ownerId: 2);

        var ex = await Assert.ThrowsAsync<ValidationException>(() => useCase.ExecuteAsync(command));
        Assert.Contains("Khuyến mãi do Chủ bãi tài trợ bắt buộc phải gắn với một bãi xe cụ thể", ex.Message);
    }

    [Fact]
    public async Task CreateAsync_LotOwnerSponsorWithoutOwnerId_ThrowsValidationException()
    {
        var repo = new FakePromotionRepository();
        var useCase = new CreatePromotionUseCase(repo);
        var command = CreateValidCommand(
            sponsor: PromotionSponsor.LotOwner,
            lotId: 1,
            ownerId: null);

        var ex = await Assert.ThrowsAsync<ValidationException>(() => useCase.ExecuteAsync(command));
        Assert.Contains("Khuyến mãi do Chủ bãi tài trợ bắt buộc phải có mã chủ bãi", ex.Message);
    }

    [Fact]
    public async Task CreateAsync_DuplicateCode_ThrowsValidationException()
    {
        var repo = new FakePromotionRepository();
        var useCase = new CreatePromotionUseCase(repo);
        var command = CreateValidCommand(code: "GIAM20K");

        // Tạo lần đầu thành công
        await useCase.ExecuteAsync(command);

        // Tạo lần hai cùng mã -> ném lỗi trùng mã
        var ex = await Assert.ThrowsAsync<ValidationException>(() => useCase.ExecuteAsync(command));
        Assert.Contains("đã tồn tại trong hệ thống", ex.Message);
    }

    [Fact]
    public async Task CreateAsync_ValidCommand_CreatesPromotionSuccessfully()
    {
        var repo = new FakePromotionRepository();
        var useCase = new CreatePromotionUseCase(repo);
        var command = CreateValidCommand(code: "vincom30", name: "Vincom giảm 30k");

        var result = await useCase.ExecuteAsync(command);

        Assert.NotNull(result);
        Assert.True(result.Id > 0);
        Assert.Equal("VINCOM30", result.Code);
        Assert.Equal("Vincom giảm 30k", result.Name);
        Assert.True(result.IsActive);
        Assert.Equal(0, result.UsedCount);
        Assert.Single(repo.Promotions);
    }

    // ==========================================
    // NHÓM 2: KIỂM THỬ ÁP DỤNG MÃ (APPLY AT CHECKOUT)
    // ==========================================

    [Theory]
    [InlineData(0)]
    [InlineData(-1000)]
    public async Task ApplyAsync_ZeroOrNegativeOriginalAmount_ThrowsValidationException(decimal invalidAmount)
    {
        var repo = new FakePromotionRepository();
        var useCase = new ApplyPromotionUseCase(repo, new FakeTimeProvider(FixedNow));
        var query = new ApplyPromotionQuery("TEST", 1, invalidAmount);

        var ex = await Assert.ThrowsAsync<ValidationException>(() => useCase.ExecuteAsync(query));
        Assert.Contains("Giá trị đơn hàng ban đầu phải là số dương lớn hơn 0", ex.Message);
    }

    [Fact]
    public async Task ApplyAsync_EmptyCode_ThrowsValidationException()
    {
        var repo = new FakePromotionRepository();
        var useCase = new ApplyPromotionUseCase(repo, new FakeTimeProvider(FixedNow));
        var query = new ApplyPromotionQuery("   ", 1, 100_000m);

        var ex = await Assert.ThrowsAsync<ValidationException>(() => useCase.ExecuteAsync(query));
        Assert.Contains("Mã khuyến mãi không được để trống", ex.Message);
    }

    [Fact]
    public async Task ApplyAsync_NonExistentCode_ThrowsNotFoundException()
    {
        var repo = new FakePromotionRepository();
        var useCase = new ApplyPromotionUseCase(repo, new FakeTimeProvider(FixedNow));
        var query = new ApplyPromotionQuery("KHONGTONTAI", 1, 100_000m);

        await Assert.ThrowsAsync<NotFoundException>(() => useCase.ExecuteAsync(query));
    }

    [Fact]
    public async Task ApplyAsync_InactivePromotion_ThrowsValidationException()
    {
        var repo = new FakePromotionRepository();
        var promo = new Promotion
        {
            Code = "TAMKHOA",
            Name = "Mã tạm khóa",
            IsActive = false,
            DiscountType = DiscountType.FixedAmount,
            DiscountValue = 10_000m,
            StartsAtUtc = FixedNow.UtcDateTime.AddDays(-1),
            EndsAtUtc = FixedNow.UtcDateTime.AddDays(1)
        };
        await repo.AddAsync(promo);

        var useCase = new ApplyPromotionUseCase(repo, new FakeTimeProvider(FixedNow));
        var query = new ApplyPromotionQuery("TAMKHOA", 1, 100_000m);

        var ex = await Assert.ThrowsAsync<ValidationException>(() => useCase.ExecuteAsync(query));
        Assert.Contains("hiện đang tạm ngưng hoạt động", ex.Message);
    }

    [Fact]
    public async Task ApplyAsync_BeforeStartsAtUtc_ThrowsValidationException()
    {
        var repo = new FakePromotionRepository();
        var promo = new Promotion
        {
            Code = "TUONGLAI",
            Name = "Mã chưa tới ngày",
            IsActive = true,
            DiscountType = DiscountType.FixedAmount,
            DiscountValue = 10_000m,
            StartsAtUtc = FixedNow.UtcDateTime.AddDays(2),
            EndsAtUtc = FixedNow.UtcDateTime.AddDays(10)
        };
        await repo.AddAsync(promo);

        var useCase = new ApplyPromotionUseCase(repo, new FakeTimeProvider(FixedNow));
        var query = new ApplyPromotionQuery("TUONGLAI", 1, 100_000m);

        var ex = await Assert.ThrowsAsync<ValidationException>(() => useCase.ExecuteAsync(query));
        Assert.Contains("chưa có hiệu lực", ex.Message);
    }

    [Fact]
    public async Task ApplyAsync_AfterEndsAtUtc_ThrowsValidationException()
    {
        var repo = new FakePromotionRepository();
        var promo = new Promotion
        {
            Code = "QUAHAN",
            Name = "Mã hết hạn",
            IsActive = true,
            DiscountType = DiscountType.FixedAmount,
            DiscountValue = 10_000m,
            StartsAtUtc = FixedNow.UtcDateTime.AddDays(-10),
            EndsAtUtc = FixedNow.UtcDateTime.AddDays(-1)
        };
        await repo.AddAsync(promo);

        var useCase = new ApplyPromotionUseCase(repo, new FakeTimeProvider(FixedNow));
        var query = new ApplyPromotionQuery("QUAHAN", 1, 100_000m);

        var ex = await Assert.ThrowsAsync<ValidationException>(() => useCase.ExecuteAsync(query));
        Assert.Contains("đã hết hạn sử dụng", ex.Message);
    }

    [Fact]
    public async Task ApplyAsync_UsageLimitReached_ThrowsValidationException()
    {
        var repo = new FakePromotionRepository();
        var promo = new Promotion
        {
            Code = "HETLUOT",
            Name = "Mã hết lượt",
            IsActive = true,
            DiscountType = DiscountType.FixedAmount,
            DiscountValue = 10_000m,
            StartsAtUtc = FixedNow.UtcDateTime.AddDays(-1),
            EndsAtUtc = FixedNow.UtcDateTime.AddDays(1),
            UsageLimit = 5,
            UsedCount = 5
        };
        await repo.AddAsync(promo);

        var useCase = new ApplyPromotionUseCase(repo, new FakeTimeProvider(FixedNow));
        var query = new ApplyPromotionQuery("HETLUOT", 1, 100_000m);

        var ex = await Assert.ThrowsAsync<ValidationException>(() => useCase.ExecuteAsync(query));
        Assert.Contains("đã hết lượt sử dụng", ex.Message);
    }

    [Fact]
    public async Task ApplyAsync_MismatchParkingLotId_ThrowsValidationException()
    {
        var repo = new FakePromotionRepository();
        var promo = new Promotion
        {
            Code = "VINCOMONLY",
            Name = "Chỉ áp dụng bãi Vincom số 1",
            ParkingLotId = 1,
            IsActive = true,
            DiscountType = DiscountType.FixedAmount,
            DiscountValue = 10_000m,
            StartsAtUtc = FixedNow.UtcDateTime.AddDays(-1),
            EndsAtUtc = FixedNow.UtcDateTime.AddDays(1)
        };
        await repo.AddAsync(promo);

        var useCase = new ApplyPromotionUseCase(repo, new FakeTimeProvider(FixedNow));
        var query = new ApplyPromotionQuery("VINCOMONLY", 2, 100_000m); // Bãi số 2

        var ex = await Assert.ThrowsAsync<ValidationException>(() => useCase.ExecuteAsync(query));
        Assert.Contains("không áp dụng cho bãi đỗ xe được chọn", ex.Message);
    }

    [Fact]
    public async Task ApplyAsync_OrderAmountBelowMinimum_ThrowsValidationException()
    {
        var repo = new FakePromotionRepository();
        var promo = new Promotion
        {
            Code = "MIN50K",
            Name = "Đơn từ 50k",
            IsActive = true,
            MinOrderAmount = 50_000m,
            DiscountType = DiscountType.FixedAmount,
            DiscountValue = 10_000m,
            StartsAtUtc = FixedNow.UtcDateTime.AddDays(-1),
            EndsAtUtc = FixedNow.UtcDateTime.AddDays(1)
        };
        await repo.AddAsync(promo);

        var useCase = new ApplyPromotionUseCase(repo, new FakeTimeProvider(FixedNow));
        var query = new ApplyPromotionQuery("MIN50K", 1, 40_000m); // Đơn chỉ 40k < 50k

        var ex = await Assert.ThrowsAsync<ValidationException>(() => useCase.ExecuteAsync(query));
        Assert.Contains("phải đạt giá trị tối thiểu", ex.Message);
    }

    [Fact]
    public async Task ApplyAsync_PercentageDiscountWithoutCap_CalculatesCorrectDiscount()
    {
        var repo = new FakePromotionRepository();
        var promo = new Promotion
        {
            Code = "GIAM20PT",
            Name = "Giảm 20% không trần",
            IsActive = true,
            DiscountType = DiscountType.Percentage,
            DiscountValue = 20m,
            MaxDiscountAmount = null,
            StartsAtUtc = FixedNow.UtcDateTime.AddDays(-1),
            EndsAtUtc = FixedNow.UtcDateTime.AddDays(1),
            Sponsor = PromotionSponsor.Platform
        };
        await repo.AddAsync(promo);

        var useCase = new ApplyPromotionUseCase(repo, new FakeTimeProvider(FixedNow));
        var query = new ApplyPromotionQuery("GIAM20PT", 1, 100_000m);

        var result = await useCase.ExecuteAsync(query);

        Assert.Equal(100_000m, result.OriginalAmount);
        Assert.Equal(20_000m, result.DiscountAmount); // 20% của 100k = 20k
        Assert.Equal(80_000m, result.FinalAmount);
        Assert.Equal(PromotionSponsor.Platform, result.Sponsor);
    }

    [Fact]
    public async Task ApplyAsync_PercentageDiscountExceedsCap_CapsAtMaxDiscountAmount()
    {
        var repo = new FakePromotionRepository();
        var promo = new Promotion
        {
            Code = "GIAM50PT_TRAN30K",
            Name = "Giảm 50% trần 30k",
            IsActive = true,
            DiscountType = DiscountType.Percentage,
            DiscountValue = 50m,
            MaxDiscountAmount = 30_000m, // Trần 30k
            StartsAtUtc = FixedNow.UtcDateTime.AddDays(-1),
            EndsAtUtc = FixedNow.UtcDateTime.AddDays(1),
            Sponsor = PromotionSponsor.LotOwner
        };
        await repo.AddAsync(promo);

        var useCase = new ApplyPromotionUseCase(repo, new FakeTimeProvider(FixedNow));
        var query = new ApplyPromotionQuery("GIAM50PT_TRAN30K", 1, 100_000m); // 50% của 100k = 50k > trần 30k

        var result = await useCase.ExecuteAsync(query);

        Assert.Equal(100_000m, result.OriginalAmount);
        Assert.Equal(30_000m, result.DiscountAmount); // Bị ép về trần 30k
        Assert.Equal(70_000m, result.FinalAmount);
        Assert.Equal(PromotionSponsor.LotOwner, result.Sponsor);
    }

    [Fact]
    public async Task ApplyAsync_FixedAmountDiscount_CalculatesCorrectDiscount()
    {
        var repo = new FakePromotionRepository();
        var promo = new Promotion
        {
            Code = "GIAM25K",
            Name = "Giảm 25.000 VNĐ",
            IsActive = true,
            DiscountType = DiscountType.FixedAmount,
            DiscountValue = 25_000m,
            StartsAtUtc = FixedNow.UtcDateTime.AddDays(-1),
            EndsAtUtc = FixedNow.UtcDateTime.AddDays(1)
        };
        await repo.AddAsync(promo);

        var useCase = new ApplyPromotionUseCase(repo, new FakeTimeProvider(FixedNow));
        var query = new ApplyPromotionQuery("GIAM25K", 1, 80_000m);

        var result = await useCase.ExecuteAsync(query);

        Assert.Equal(80_000m, result.OriginalAmount);
        Assert.Equal(25_000m, result.DiscountAmount);
        Assert.Equal(55_000m, result.FinalAmount);
    }

    [Fact]
    public async Task ApplyAsync_FixedAmountExceedsOrderAmount_CapsAtOrderAmount()
    {
        var repo = new FakePromotionRepository();
        var promo = new Promotion
        {
            Code = "GIAM50K",
            Name = "Giảm 50k",
            IsActive = true,
            DiscountType = DiscountType.FixedAmount,
            DiscountValue = 50_000m,
            StartsAtUtc = FixedNow.UtcDateTime.AddDays(-1),
            EndsAtUtc = FixedNow.UtcDateTime.AddDays(1)
        };
        await repo.AddAsync(promo);

        var useCase = new ApplyPromotionUseCase(repo, new FakeTimeProvider(FixedNow));
        var query = new ApplyPromotionQuery("GIAM50K", 1, 30_000m); // Đơn chỉ 30k < 50k

        var result = await useCase.ExecuteAsync(query);

        Assert.Equal(30_000m, result.OriginalAmount);
        Assert.Equal(30_000m, result.DiscountAmount); // Giảm tối đa bằng giá trị đơn
        Assert.Equal(0m, result.FinalAmount); // Số tiền còn lại = 0 đồng
    }

    // ==========================================
    // NHÓM 3: KIỂM THỬ XEM & CẬP NHẬT (GET & UPDATE)
    // ==========================================

    [Fact]
    public async Task GetByIdAsync_NonExistentId_ThrowsNotFoundException()
    {
        var repo = new FakePromotionRepository();
        var useCase = new GetPromotionsUseCase(repo);

        await Assert.ThrowsAsync<NotFoundException>(() => useCase.GetByIdAsync(999));
    }

    [Fact]
    public async Task ListAsync_FilterByLot_ReturnsLotAndPlatformPromotions()
    {
        var repo = new FakePromotionRepository();
        await repo.AddAsync(new Promotion { Code = "TOANSAN", Name = "Toàn sàn", ParkingLotId = null, IsActive = true });
        await repo.AddAsync(new Promotion { Code = "BAI1", Name = "Bãi 1", ParkingLotId = 1, IsActive = true });
        await repo.AddAsync(new Promotion { Code = "BAI2", Name = "Bãi 2", ParkingLotId = 2, IsActive = true });

        var useCase = new GetPromotionsUseCase(repo);
        var result = await useCase.ListAsync(parkingLotId: 1);

        Assert.Equal(2, result.Count);
        Assert.Contains(result, p => p.Code == "TOANSAN");
        Assert.Contains(result, p => p.Code == "BAI1");
        Assert.DoesNotContain(result, p => p.Code == "BAI2");
    }

    [Fact]
    public async Task UpdateAsync_ValidCommand_UpdatesPromotionSuccessfully()
    {
        var repo = new FakePromotionRepository();
        var promo = new Promotion
        {
            Code = "UPDATE_TEST",
            Name = "Tên cũ",
            IsActive = true,
            StartsAtUtc = FixedNow.UtcDateTime.AddDays(-1),
            EndsAtUtc = FixedNow.UtcDateTime.AddDays(5)
        };
        await repo.AddAsync(promo);

        var useCase = new UpdatePromotionUseCase(repo);
        var command = new UpdatePromotionCommand(
            promo.Id,
            "Tên mới cập nhật",
            40_000m,
            60_000m,
            FixedNow.UtcDateTime.AddDays(-1),
            FixedNow.UtcDateTime.AddDays(15),
            200,
            false); // Tắt hoạt động

        var result = await useCase.ExecuteAsync(command);

        Assert.Equal("Tên mới cập nhật", result.Name);
        Assert.False(result.IsActive);
        Assert.Equal(200, result.UsageLimit);
        Assert.Equal(60_000m, result.MinOrderAmount);
    }
}

