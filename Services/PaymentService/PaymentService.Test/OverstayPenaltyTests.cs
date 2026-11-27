using ParkingManagement.SharedKernel.Enums;
using ParkingManagement.SharedKernel.Exceptions;
using PaymentService.Application.Features;
using PaymentService.Application.Features.Pricing;
using PaymentService.Domain.Entities;
using PaymentService.Domain.Services;
using Xunit;

namespace PaymentService.Test;

/// <summary>
/// Bộ kiểm thử đơn vị toàn diện cho tính năng tính phí phạt quá giờ (US-047, UC-22).
/// Tuân thủ quy chuẩn đặt tên test: TênHàm_TrạngThái_KếtQuả.
/// </summary>
public class OverstayPenaltyTests
{
    private static readonly DateTime BookedEnd = new(2026, 10, 2, 10, 0, 0, DateTimeKind.Utc);

    /// <summary>
    /// Biểu giá mẫu cho kiểm thử: Giờ đầu 10.000đ, từ giờ thứ hai 20.000đ/giờ cho cả Sedan và SUV.
    /// Hệ số quá giờ mặc định là 1.5x, hệ số xe quá khổ là 1.5x.
    /// </summary>
    private static RateCard CreateSampleRateCard(decimal defaultOverstayMultiplier = 1.5m)
    {
        var card = new RateCard
        {
            ParkingLotId = 10,
            OverstayMultiplier = defaultOverstayMultiplier,
            OversizedMultiplier = 1.5m
        };

        foreach (var t in new[] { VehicleType.Sedan, VehicleType.Suv })
        {
            card.Rules.Add(new RateRule
            {
                VehicleType = t,
                FromMinute = 0,
                ToMinute = 60,
                BlockMinutes = 60,
                PricePerBlock = 10000m
            });

            card.Rules.Add(new RateRule
            {
                VehicleType = t,
                FromMinute = 60,
                ToMinute = null,
                BlockMinutes = 60,
                PricePerBlock = 20000m
            });
        }

        return card;
    }

    // =========================================================================
    // PHẦN 1: KIỂM THỬ TẦNG DOMAIN (PriceCalculator.CalculateOverstay)
    // =========================================================================

    [Fact]
    public void CalculateOverstay_ExitBeforeBookedEnd_ReturnsZeroPenaltyAndGracePeriodTrue()
    {
        // Giờ ra thực tế lúc 09:30 (sớm 30 phút so với giờ hẹn 10:00)
        var exitUtc = BookedEnd.AddMinutes(-30);
        var card = CreateSampleRateCard();

        var result = PriceCalculator.CalculateOverstay(card, VehicleType.Sedan, BookedEnd, exitUtc);

        Assert.Equal(0, result.OverstayMinutes);
        Assert.True(result.IsWithinGracePeriod);
        Assert.Equal(0m, result.BaseRateAmount);
        Assert.Equal(0m, result.PenaltyAmount);
    }

    [Fact]
    public void CalculateOverstay_ExitExactlyAtBookedEnd_ReturnsZeroPenaltyAndGracePeriodTrue()
    {
        // Giờ ra thực tế đúng 10:00:00 (không trễ giây nào)
        var exitUtc = BookedEnd;
        var card = CreateSampleRateCard();

        var result = PriceCalculator.CalculateOverstay(card, VehicleType.Sedan, BookedEnd, exitUtc);

        Assert.Equal(0, result.OverstayMinutes);
        Assert.True(result.IsWithinGracePeriod);
        Assert.Equal(0m, result.PenaltyAmount);
    }

    [Fact]
    public void CalculateOverstay_ExitWithin15MinutesGracePeriod_ReturnsZeroOverstayMinutesAndGracePeriodTrue()
    {
        // Ra trễ 10 phút (nằm trong khoảng 15 phút ân hạn) -> miễn phí phạt, số phút tính phạt = 0
        var exitUtc = BookedEnd.AddMinutes(10);
        var card = CreateSampleRateCard();

        var result = PriceCalculator.CalculateOverstay(card, VehicleType.Sedan, BookedEnd, exitUtc);

        Assert.Equal(0, result.OverstayMinutes);
        Assert.True(result.IsWithinGracePeriod);
        Assert.Equal(0m, result.BaseRateAmount);
        Assert.Equal(0m, result.PenaltyAmount);
    }

    [Fact]
    public void CalculateOverstay_ExitExactlyAt15MinutesGracePeriodBoundary_ReturnsGracePeriodTrueAndZeroPenalty()
    {
        // Ra trễ đúng mốc biên 15 phút tròn
        var exitUtc = BookedEnd.AddMinutes(15);
        var card = CreateSampleRateCard();

        var result = PriceCalculator.CalculateOverstay(card, VehicleType.Sedan, BookedEnd, exitUtc);

        Assert.Equal(0, result.OverstayMinutes);
        Assert.True(result.IsWithinGracePeriod);
        Assert.Equal(0m, result.PenaltyAmount);
    }

    [Fact]
    public void CalculateOverstay_ExitBeyond15MinutesGracePeriod_BilledFromBookedEndTimeWithMultiplier()
    {
        // Ra trễ 35 phút (vượt quá 15 phút ân hạn) -> Tính từ 10:00, 35 phút làm tròn thành 1 block 60' (10.000đ) x 1.5 = 15.000đ
        var exitUtc = BookedEnd.AddMinutes(35);
        var card = CreateSampleRateCard(defaultOverstayMultiplier: 1.5m);

        var result = PriceCalculator.CalculateOverstay(card, VehicleType.Sedan, BookedEnd, exitUtc);

        Assert.Equal(35, result.OverstayMinutes);
        Assert.False(result.IsWithinGracePeriod);
        Assert.Equal(10000m, result.BaseRateAmount);
        Assert.Equal(15000m, result.PenaltyAmount);
        Assert.Single(result.Lines);
    }

    [Fact]
    public void CalculateOverstay_ExitFractionalBlock_CeilsToNextBlock()
    {
        // Ra trễ 65 phút: 60 phút đầu (10.000đ) + 5 phút lẻ của giờ thứ hai làm tròn thành 1 block thứ hai (20.000đ) = 30.000đ cước gốc
        // Tiền phạt = 30.000đ x 1.5 = 45.000đ
        var exitUtc = BookedEnd.AddMinutes(65);
        var card = CreateSampleRateCard(defaultOverstayMultiplier: 1.5m);

        var result = PriceCalculator.CalculateOverstay(card, VehicleType.Sedan, BookedEnd, exitUtc);

        Assert.Equal(65, result.OverstayMinutes);
        Assert.False(result.IsWithinGracePeriod);
        Assert.Equal(30000m, result.BaseRateAmount);
        Assert.Equal(45000m, result.PenaltyAmount);
        Assert.Equal(2, result.Lines.Count);
    }

    [Fact]
    public void CalculateOverstay_CustomMultiplierProvided_OverridesRateCardDefaultMultiplier()
    {
        // Hệ số từ hợp đồng đặt chỗ ban đầu là 2.0x, ghi đè hệ số 1.5x của bãi xe
        var exitUtc = BookedEnd.AddMinutes(35);
        var card = CreateSampleRateCard(defaultOverstayMultiplier: 1.5m);

        var result = PriceCalculator.CalculateOverstay(
            card, VehicleType.Sedan, BookedEnd, exitUtc, customOverstayMultiplier: 2.0m);

        Assert.Equal(35, result.OverstayMinutes);
        Assert.Equal(2.0m, result.OverstayMultiplier);
        // 1 block 10.000đ x 2.0 = 20.000đ
        Assert.Equal(10000m, result.BaseRateAmount);
        Assert.Equal(20000m, result.PenaltyAmount);
    }

    [Fact]
    public void CalculateOverstay_OversizedVehicleWithoutSpecificRules_AppliesOversizedMultiplier()
    {
        // Xe quá khổ (Oversized) mượn bảng giá SUV và nhân thêm hệ số OversizedMultiplier 1.5x
        var exitUtc = BookedEnd.AddMinutes(35);
        var card = CreateSampleRateCard(defaultOverstayMultiplier: 1.5m);

        var result = PriceCalculator.CalculateOverstay(card, VehicleType.Oversized, BookedEnd, exitUtc);

        Assert.Equal(35, result.OverstayMinutes);
        // Cước gốc 10.000đ x 1.5 (quá khổ) = 15.000đ. Tiền phạt = 15.000đ x 1.5 (hệ số quá giờ) = 22.500đ
        Assert.Equal(15000m, result.BaseRateAmount);
        Assert.Equal(22500m, result.PenaltyAmount);
    }

    [Fact]
    public void CalculateOverstay_LongOverstayOver24Hours_CalculatesAllBlocksAccurately()
    {
        // Đỗ quá giờ 25 tiếng = 1500 phút
        var exitUtc = BookedEnd.AddMinutes(1500);
        var card = CreateSampleRateCard(defaultOverstayMultiplier: 1.5m);

        var result = PriceCalculator.CalculateOverstay(card, VehicleType.Sedan, BookedEnd, exitUtc);

        Assert.Equal(1500, result.OverstayMinutes);
        Assert.False(result.IsWithinGracePeriod);
        // 60' đầu = 10.000đ. 24 tiếng còn lại (1440 phút) = 24 block x 20.000đ = 480.000đ. Tổng cước gốc = 490.000đ
        Assert.Equal(490000m, result.BaseRateAmount);
        // Tiền phạt = 490.000đ x 1.5 = 735.000đ
        Assert.Equal(735000m, result.PenaltyAmount);
    }

    // =========================================================================
    // PHẦN 2: KIỂM THỬ TẦNG APPLICATION (CalculateOverstayPenaltyUseCase)
    // =========================================================================

    private sealed class FakeRateCardRepository(RateCard? cardToReturn) : IRateCardRepository
    {
        public Task<RateCard?> GetActiveAsync(int parkingLotId, DateTime atUtc, CancellationToken cancellationToken)
            => Task.FromResult(cardToReturn);
    }

    [Fact]
    public async Task ExecuteAsync_ValidOverstayQuery_ReturnsAccurateOverstayPenaltyDto()
    {
        var card = CreateSampleRateCard(defaultOverstayMultiplier: 1.5m);
        var repo = new FakeRateCardRepository(card);
        var useCase = new CalculateOverstayPenaltyUseCase(repo);

        var query = new CalculateOverstayPenaltyQuery(
            ParkingLotId: 10,
            VehicleType: "Sedan",
            BookedEndAtUtc: BookedEnd,
            ActualExitAtUtc: BookedEnd.AddMinutes(35));

        var dto = await useCase.ExecuteAsync(query);

        Assert.Equal(10, dto.ParkingLotId);
        Assert.Equal(card.Id, dto.RateCardId);
        Assert.Equal("Sedan", dto.VehicleType);
        Assert.Equal(35, dto.OverstayMinutes);
        Assert.False(dto.IsWithinGracePeriod);
        Assert.Equal(1.5m, dto.OverstayMultiplier);
        Assert.Equal(10000m, dto.BaseRateAmount);
        Assert.Equal(15000m, dto.PenaltyAmount);
        Assert.Contains("Quá giờ 35 phút", dto.Description);
    }

    [Fact]
    public async Task ExecuteAsync_InvalidParkingLotId_ThrowsValidationException()
    {
        var repo = new FakeRateCardRepository(CreateSampleRateCard());
        var useCase = new CalculateOverstayPenaltyUseCase(repo);

        var query = new CalculateOverstayPenaltyQuery(
            ParkingLotId: 0, // Không hợp lệ
            VehicleType: "Sedan",
            BookedEndAtUtc: BookedEnd,
            ActualExitAtUtc: BookedEnd.AddMinutes(35));

        await Assert.ThrowsAsync<ValidationException>(() => useCase.ExecuteAsync(query));
    }

    [Fact]
    public async Task ExecuteAsync_InvalidVehicleType_ThrowsValidationException()
    {
        var repo = new FakeRateCardRepository(CreateSampleRateCard());
        var useCase = new CalculateOverstayPenaltyUseCase(repo);

        var query = new CalculateOverstayPenaltyQuery(
            ParkingLotId: 10,
            VehicleType: "TenXeKhongTonTai", // Không hợp lệ
            BookedEndAtUtc: BookedEnd,
            ActualExitAtUtc: BookedEnd.AddMinutes(35));

        await Assert.ThrowsAsync<ValidationException>(() => useCase.ExecuteAsync(query));
    }

    [Fact]
    public async Task ExecuteAsync_NonExistentRateCard_ThrowsNotFoundException()
    {
        var repo = new FakeRateCardRepository(null); // Không tìm thấy biểu giá
        var useCase = new CalculateOverstayPenaltyUseCase(repo);

        var query = new CalculateOverstayPenaltyQuery(
            ParkingLotId: 999,
            VehicleType: "Sedan",
            BookedEndAtUtc: BookedEnd,
            ActualExitAtUtc: BookedEnd.AddMinutes(35));

        await Assert.ThrowsAsync<NotFoundException>(() => useCase.ExecuteAsync(query));
    }

    [Fact]
    public async Task ExecuteAsync_ExitWithinGracePeriod_ReturnsDtoWithZeroPenaltyAndGracePeriodTrue()
    {
        var card = CreateSampleRateCard();
        var repo = new FakeRateCardRepository(card);
        var useCase = new CalculateOverstayPenaltyUseCase(repo);

        var query = new CalculateOverstayPenaltyQuery(
            ParkingLotId: 10,
            VehicleType: "Sedan",
            BookedEndAtUtc: BookedEnd,
            ActualExitAtUtc: BookedEnd.AddMinutes(12)); // Ra trễ 12 phút trong ân hạn

        var dto = await useCase.ExecuteAsync(query);

        Assert.Equal(0, dto.OverstayMinutes);
        Assert.True(dto.IsWithinGracePeriod);
        Assert.Equal(0m, dto.PenaltyAmount);
    }

    [Fact]
    public async Task ExecuteAsync_ExitBeyondGracePeriodWithCustomMultiplier_AppliesCustomMultiplierInDto()
    {
        var card = CreateSampleRateCard(defaultOverstayMultiplier: 1.5m);
        var repo = new FakeRateCardRepository(card);
        var useCase = new CalculateOverstayPenaltyUseCase(repo);

        var query = new CalculateOverstayPenaltyQuery(
            ParkingLotId: 10,
            VehicleType: "Sedan",
            BookedEndAtUtc: BookedEnd,
            ActualExitAtUtc: BookedEnd.AddMinutes(35),
            CustomOverstayMultiplier: 2.0m); // Ghi đè thành 2.0x

        var dto = await useCase.ExecuteAsync(query);

        Assert.Equal(2.0m, dto.OverstayMultiplier);
        Assert.Equal(20000m, dto.PenaltyAmount); // 10.000 x 2.0 = 20.000
    }
}
