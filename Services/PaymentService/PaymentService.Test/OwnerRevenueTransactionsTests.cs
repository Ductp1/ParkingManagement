using System.Text.Json;
using ParkingManagement.SharedKernel.Enums;
using ParkingManagement.SharedKernel.Exceptions;
using PaymentService.Application.Features.Reports;
using PaymentService.Domain.Entities;
using static PaymentService.Test.OwnerRevenueTestData;

namespace PaymentService.Test;

/// <summary>
/// US-078 AC2–AC3: danh sách giao dịch trong báo cáo doanh thu chủ bãi – chỉ dữ liệu của chính chủ bãi, lọc, phân trang,
/// không lộ dữ liệu tài xế. Port chạy trong bộ nhớ bằng đúng hình dạng truy vấn của Infrastructure.
/// </summary>
public class OwnerRevenueTransactionsTests
{
    private static readonly OwnerRevenueTransactionQuery October = new(new DateOnly(2026, 10, 1), new DateOnly(2026, 11, 1));

    private static ListOwnerRevenueTransactionsUseCase UseCase(IOwnerRevenueQueries queries)
        => new(queries, new FixedTimeProvider(Utc(2026, 10, 15, 3)));

    private static Payment WithProviderId(Payment payment, string? providerTransactionId)
    {
        payment.ProviderTransactionId = providerTransactionId;
        return payment;
    }

    [Fact]
    public async Task OwnerSeesOnlyOwnTransactions_NewestFirst()
    {
        var queries = new InMemoryOwnerRevenueQueries(
        [
            Paid(OwnerA, LotA1, 10_000, Utc(2026, 10, 2, 3)),
            Paid(OwnerB, LotB1, 70_000, Utc(2026, 10, 3, 3)),
            Paid(OwnerA, LotA2, 20_000, Utc(2026, 10, 4, 3)),
        ]);

        var result = await UseCase(queries).ExecuteAsync(OwnerA, October, 1, 20);

        Assert.Equal(2, result.TotalCount);
        Assert.Equal([20_000m, 10_000m], result.Items.Select(t => t.Amount));
        Assert.Equal(OwnerA, queries.LastFilter!.OwnerProfileId);
    }

    [Fact]
    public async Task LotOfAnotherOwner_ReturnsEmptyPage()
    {
        var queries = new InMemoryOwnerRevenueQueries([Paid(OwnerB, LotB1, 70_000, Utc(2026, 10, 3, 3))]);

        var result = await UseCase(queries).ExecuteAsync(OwnerA, October with { ParkingLotId = LotB1 }, 1, 20);

        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalCount);
        Assert.Equal((OwnerA, LotB1), (queries.LastFilter!.OwnerProfileId, queries.LastFilter.ParkingLotId));
    }

    [Fact]
    public async Task OnlyRevenueStatusesAndVietnamDayRange_AreListed()
    {
        var queries = new InMemoryOwnerRevenueQueries(
        [
            Paid(OwnerA, LotA1, 1, Utc(2026, 9, 30, 16, 59, 59)),                          // 30/09 ICT → ngoài
            Paid(OwnerA, LotA1, 2, Utc(2026, 9, 30, 17)),                                  // 01/10 00:00 ICT
            Paid(OwnerA, LotA1, 4, Utc(2026, 10, 5, 3), status: PaymentStatus.Pending),
            Paid(OwnerA, LotA1, 8, Utc(2026, 10, 5, 3), status: PaymentStatus.Failed),
            Paid(OwnerA, LotA1, 16, Utc(2026, 10, 5, 3), status: PaymentStatus.Refunded, refunded: 16),
            Paid(OwnerA, LotA1, 32, Utc(2026, 10, 31, 17)),                                // 01/11 ICT → ngoài
        ]);

        var result = await UseCase(queries).ExecuteAsync(OwnerA, October, 1, 20);

        Assert.Equal([16m, 2m], result.Items.Select(t => t.Amount));
    }

    [Fact]
    public async Task Paging_SkipsAndCountsAll()
    {
        var queries = new InMemoryOwnerRevenueQueries(Enumerable.Range(1, 5)
            .Select(day => Paid(OwnerA, LotA1, day * 1_000, Utc(2026, 10, day, 3))));

        var page2 = await UseCase(queries).ExecuteAsync(OwnerA, October, 2, 2);

        Assert.Equal(5, page2.TotalCount);
        Assert.Equal(3, page2.TotalPages);
        Assert.Equal([3_000m, 2_000m], page2.Items.Select(t => t.Amount));
    }

    [Fact]
    public async Task FiltersByMethodPurposeAndStatus_CaseInsensitive()
    {
        var at = Utc(2026, 10, 5, 3);
        var queries = new InMemoryOwnerRevenueQueries(
        [
            Paid(OwnerA, LotA1, 1_000, at, PaymentMethod.Cash, PaymentPurpose.WalkIn),
            Paid(OwnerA, LotA1, 2_000, at, PaymentMethod.Cash, PaymentPurpose.Overstay),
            Paid(OwnerA, LotA1, 4_000, at, PaymentMethod.Cash, PaymentPurpose.WalkIn, PaymentStatus.PartiallyRefunded, 1_000),
            Paid(OwnerA, LotA1, 8_000, at, PaymentMethod.Vnpay, PaymentPurpose.WalkIn),
        ]);

        var result = await UseCase(queries).ExecuteAsync(OwnerA,
            October with { Method = "cash", Purpose = "WALKIN", Status = "partiallyRefunded" }, 1, 20);

        var only = Assert.Single(result.Items);
        Assert.Equal((4_000m, 1_000m, 3_000m), (only.Amount, only.RefundedAmount, only.NetAmount));
        Assert.Equal(("Cash", "WalkIn", "PartiallyRefunded"), (only.Method, only.Purpose, only.Status));
    }

    [Theory]
    [InlineData("Paypal", null, null, "method phải là Vnpay, Momo, VietQr hoặc Cash.")]
    [InlineData("1", null, null, "method phải là Vnpay, Momo, VietQr hoặc Cash.")]
    [InlineData(null, "Parking", null, "purpose phải là Booking, Extension, Overstay, WalkIn hoặc MonthlyPass.")]
    [InlineData(null, null, "Pending", "status phải là Succeeded, PartiallyRefunded hoặc Refunded.")]
    [InlineData(null, null, "Succeeded,Refunded", "status phải là Succeeded, PartiallyRefunded hoặc Refunded.")]
    public async Task InvalidFilterValue_Throws400(string? method, string? purpose, string? status, string message)
    {
        var ex = await Assert.ThrowsAsync<ValidationException>(() => UseCase(new InMemoryOwnerRevenueQueries())
            .ExecuteAsync(OwnerA, October with { Method = method, Purpose = purpose, Status = status }, 1, 20));
        Assert.Equal(message, ex.Message);
    }

    [Theory]
    [InlineData(0, 20, "page phải ≥ 1.")]
    [InlineData(1, 0, "pageSize phải trong khoảng 1–100.")]
    [InlineData(1, 101, "pageSize phải trong khoảng 1–100.")]
    [InlineData(int.MaxValue, 100, "page quá lớn.")]
    public async Task InvalidPaging_Throws400(int page, int pageSize, string message)
    {
        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            UseCase(new InMemoryOwnerRevenueQueries()).ExecuteAsync(OwnerA, October, page, pageSize));
        Assert.Equal(message, ex.Message);
    }

    [Fact]
    public async Task OwnerWithoutProfile_Throws403()
    {
        await Assert.ThrowsAsync<ForbiddenException>(() => UseCase(new InMemoryOwnerRevenueQueries()).ExecuteAsync(null, October, 1, 20));
    }

    [Fact]
    public async Task InvalidRange_Throws400()
    {
        await Assert.ThrowsAsync<ValidationException>(() => UseCase(new InMemoryOwnerRevenueQueries())
            .ExecuteAsync(OwnerA, new OwnerRevenueTransactionQuery(new DateOnly(2026, 11, 1), new DateOnly(2026, 10, 1)), 1, 20));
    }

    // ----- Che dữ liệu -----

    [Theory]
    [InlineData("VNP14500001", "****0001")]
    [InlineData("12345", "****2345")]
    [InlineData("1234", "****")]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void ProviderTransactionId_KeepsOnlyLast4(string? raw, string? expected)
        => Assert.Equal(expected, OwnerRevenueTransactionRules.MaskProviderTransactionId(raw));

    [Fact]
    public async Task Dto_HasNoDriverOrCallbackData_AndMasksProviderId()
    {
        var payment = WithProviderId(Paid(OwnerA, LotA1, 50_000, Utc(2026, 10, 5, 3)), "VNP14500001");
        payment.UserId = 42;
        payment.TransferContent = "SS-0001 Nguyen Van A";
        payment.RawCallbackJson = "{\"vnp_CardNumber\":\"9704xxxx\"}";
        payment.ConfirmedByStaffId = 9;
        payment.BookingCode = "BK-0001";

        var result = await UseCase(new InMemoryOwnerRevenueQueries([payment])).ExecuteAsync(OwnerA, October, 1, 20);

        var dto = Assert.Single(result.Items);
        Assert.Equal("****0001", dto.ProviderTransactionRef);
        Assert.Equal("BK-0001", dto.BookingCode);
        var json = JsonSerializer.Serialize(dto);
        foreach (var leaked in new[] { "UserId", "TransferContent", "RawCallbackJson", "ConfirmedByStaffId", "VNP14500001", "Nguyen", "9704" })
            Assert.DoesNotContain(leaked, json);
    }
}
