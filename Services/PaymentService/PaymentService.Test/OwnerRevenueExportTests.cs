using System.Text;
using ParkingManagement.SharedKernel.Enums;
using ParkingManagement.SharedKernel.Exceptions;
using PaymentService.Application.Features.Reports;
using PaymentService.Domain.Entities;
using static PaymentService.Test.OwnerRevenueTestData;

namespace PaymentService.Test;

/// <summary>
/// US-078 AC3: xuất CSV giao dịch của chủ bãi – UTF-8 BOM, RFC 4180 (CRLF, ô có dấu phẩy/ngoặc kép được bao), chống CSV injection,
/// tối đa 10.000 dòng (vượt → 400, không cắt ngầm), chỉ dữ liệu của chính chủ bãi, không có cột dữ liệu tài xế.
/// </summary>
public class OwnerRevenueExportTests
{
    private static readonly OwnerRevenueTransactionQuery October = new(new DateOnly(2026, 10, 1), new DateOnly(2026, 11, 1));

    private static ExportOwnerRevenueTransactionsUseCase UseCase(IOwnerRevenueQueries queries)
        => new(queries, new FixedTimeProvider(Utc(2026, 10, 15, 3)));

    private static string[] Lines(OwnerRevenueCsvFile file)
    {
        var text = Encoding.UTF8.GetString(file.Content, 3, file.Content.Length - 3);
        return text.Split("\r\n");
    }

    [Fact]
    public async Task Csv_HasBomHeaderAndOwnRowsNewestFirst()
    {
        var older = Paid(OwnerA, LotA1, 50_000, Utc(2026, 10, 5, 1, 30), status: PaymentStatus.PartiallyRefunded, refunded: 12_000);
        older.Code = "PM-0001";
        older.BookingCode = "BK-0001";
        older.ProviderTransactionId = "VNP14500001";
        older.UserId = 42;
        var newer = Paid(OwnerA, LotA2, 20_000, Utc(2026, 10, 6, 3), PaymentMethod.Cash, PaymentPurpose.WalkIn);
        newer.Code = "PM-0002";
        var other = Paid(OwnerB, LotB1, 99_000, Utc(2026, 10, 6, 3));

        var file = await UseCase(new InMemoryOwnerRevenueQueries([older, newer, other])).ExecuteAsync(OwnerA, October);

        Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, file.Content[..3]);
        Assert.Equal("owner-revenue-20261001-20261101.csv", file.FileName);
        Assert.Equal("text/csv; charset=utf-8", OwnerRevenueCsvFile.ContentType);
        Assert.Equal(
        [
            "Code,PaidAtUtc,PaidAtIct,ParkingLotId,BookingCode,Method,Purpose,Status,Amount,RefundedAmount,NetAmount,ProviderTransactionRef",
            "PM-0002,2026-10-06T03:00:00Z,06/10/2026 10:00:00,12,,Cash,WalkIn,Succeeded,20000,0,20000,",
            "PM-0001,2026-10-05T01:30:00Z,05/10/2026 08:30:00,11,BK-0001,Vnpay,Booking,PartiallyRefunded,50000,12000,38000,****0001",
            "",
        ], Lines(file));
        Assert.DoesNotContain("99000", Encoding.UTF8.GetString(file.Content));
    }

    [Fact]
    public async Task Csv_NeutralizesFormulas_AndQuotesSpecialCharacters()
    {
        var formula = Paid(OwnerA, LotA1, 1_000, Utc(2026, 10, 6, 3));
        formula.Code = "PM-0003";
        formula.BookingCode = "=HYPERLINK(\"http://x\",\"a,b\")";
        var dash = Paid(OwnerA, LotA1, 1_000, Utc(2026, 10, 5, 3));
        dash.Code = "PM-0004";
        dash.BookingCode = "-2+3";

        var lines = Lines(await UseCase(new InMemoryOwnerRevenueQueries([formula, dash])).ExecuteAsync(OwnerA, October));

        Assert.Contains(",\"'=HYPERLINK(\"\"http://x\"\",\"\"a,b\"\")\",", lines[1]);
        Assert.Contains(",'-2+3,", lines[2]);
    }

    [Fact]
    public async Task Csv_FiltersLikeTheList_AndLotOfAnotherOwnerGivesHeaderOnly()
    {
        var queries = new InMemoryOwnerRevenueQueries([Paid(OwnerB, LotB1, 70_000, Utc(2026, 10, 3, 3))]);

        var file = await UseCase(queries).ExecuteAsync(OwnerA, October with { ParkingLotId = LotB1, Method = "Vnpay" });

        Assert.Equal(2, Lines(file).Length);   // header + dòng trống sau CRLF cuối
        Assert.Equal((OwnerA, LotB1, PaymentMethod.Vnpay),
            (queries.LastTransactionFilter!.Range.OwnerProfileId, queries.LastTransactionFilter.Range.ParkingLotId, queries.LastTransactionFilter.Method));
    }

    [Fact]
    public async Task ExactlyMaxRows_IsExported()
    {
        var payments = Enumerable.Range(0, ExportOwnerRevenueTransactionsUseCase.MaxRows)
            .Select(i => Paid(OwnerA, LotA1, 1_000, Utc(2026, 10, 5, 3).AddSeconds(i)));

        var file = await UseCase(new InMemoryOwnerRevenueQueries(payments)).ExecuteAsync(OwnerA, October);

        Assert.Equal(ExportOwnerRevenueTransactionsUseCase.MaxRows + 2, Lines(file).Length);
    }

    [Fact]
    public async Task MoreThanMaxRows_Throws400_InsteadOfTruncating()
    {
        var payments = Enumerable.Range(0, ExportOwnerRevenueTransactionsUseCase.MaxRows + 1)
            .Select(i => Paid(OwnerA, LotA1, 1_000, Utc(2026, 10, 5, 3).AddSeconds(i)));

        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            UseCase(new InMemoryOwnerRevenueQueries(payments)).ExecuteAsync(OwnerA, October));
        Assert.Equal("Kết quả vượt quá 10.000 dòng. Vui lòng thu hẹp bộ lọc (thời gian, bãi, phương thức…) rồi xuất lại.", ex.Message);
    }

    [Fact]
    public async Task OwnerWithoutProfile_Throws403()
    {
        await Assert.ThrowsAsync<ForbiddenException>(() => UseCase(new InMemoryOwnerRevenueQueries()).ExecuteAsync(null, October));
    }

    [Fact]
    public async Task InvalidStatus_Throws400()
    {
        await Assert.ThrowsAsync<ValidationException>(() =>
            UseCase(new InMemoryOwnerRevenueQueries()).ExecuteAsync(OwnerA, October with { Status = "Failed" }));
    }
}
