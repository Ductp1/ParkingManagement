using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ParkingManagement.ServiceDefaults.Persistence;
using ParkingManagement.SharedKernel.Contracts;
using ParkingManagement.SharedKernel.Enums;
using PaymentService.Domain.Entities;

namespace PaymentService.Infrastructure.Persistence.Seeding;

/// <summary>3 bảng giá (Vincom, TSN, Landmark), mã WELCOME10, 2 thanh toán VNPAY + 1 hóa đơn cho BK-0001 / BK-0002.</summary>
public sealed class PaymentDataSeeder(ILogger<PaymentDataSeeder> logger) : IDataSeeder<PaymentDbContext>
{
    public async Task SeedAsync(PaymentDbContext db, CancellationToken cancellationToken)
    {
        await SeedCoreAsync(db, cancellationToken);
        await SeedExtrasAsync(db, cancellationToken);
    }

    /// <summary>Dữ liệu demo ban đầu (migration InitialCreate).</summary>
    private async Task SeedCoreAsync(PaymentDbContext db, CancellationToken cancellationToken)
    {
        if (await db.RateCards.AnyAsync(cancellationToken)) return;
        var now = DateTime.UtcNow;

        RateCard Card(int lotId, int ownerId, decimal first, decimal next, decimal after, bool oversized)
        {
            var card = new RateCard
            {
                ParkingLotId = lotId, OwnerProfileId = ownerId, Name = "Bảng giá tiêu chuẩn 2026",
                EffectiveFromUtc = now.AddDays(-30), OvernightSurcharge = 30000, WeekendMultiplier = 1.2m,
                HolidayMultiplier = 1.5m, OversizedMultiplier = 1.5m, OverstayMultiplier = 1.5m, MaxDailyAmount = 300000
            };
            var types = oversized ? new[] { VehicleType.Sedan, VehicleType.Suv, VehicleType.Pickup, VehicleType.Oversized }
                                  : new[] { VehicleType.Sedan, VehicleType.Suv };
            var order = 0;
            foreach (var t in types)
            {
                card.Rules.Add(new RateRule { VehicleType = t, FromMinute = 0, ToMinute = 60, BlockMinutes = 60, PricePerBlock = first, SortOrder = ++order });
                card.Rules.Add(new RateRule { VehicleType = t, FromMinute = 60, ToMinute = 240, BlockMinutes = 60, PricePerBlock = next, SortOrder = ++order });
                card.Rules.Add(new RateRule { VehicleType = t, FromMinute = 240, ToMinute = null, BlockMinutes = 60, PricePerBlock = after, SortOrder = ++order });
            }
            return card;
        }

        foreach (var card in new[]
                 {
                     Card(DemoIds.LotVincom, DemoIds.OwnerVincom, 30000, 20000, 15000, oversized: false),
                     Card(DemoIds.LotTsn, DemoIds.OwnerTsn, 25000, 15000, 10000, oversized: true),
                     Card(DemoIds.LotLandmark, DemoIds.OwnerVincom, 40000, 25000, 20000, oversized: false),
                 })
        {
            db.RateCards.Add(card);
            await db.SaveChangesAsync(cancellationToken);
        }

        db.Promotions.Add(new Promotion
        {
            Code = "WELCOME10", Name = "Giảm 10% cho lần đặt đầu tiên", Sponsor = PromotionSponsor.Platform,
            DiscountType = DiscountType.Percentage, DiscountValue = 10, MaxDiscountAmount = 20000,
            StartsAtUtc = now.AddDays(-7), EndsAtUtc = now.AddMonths(3), UsageLimit = 1000, UsedCount = 1
        });

        var paidCompleted = now.Date.AddDays(-2).AddHours(1);
        var completed = new Payment
        {
            Code = "PM-0001", BookingId = DemoIds.BookingCompleted, BookingCode = "BK-0001", UserId = DemoIds.Driver1User,
            ParkingLotId = DemoIds.LotVincom, OwnerProfileId = DemoIds.OwnerVincom, Method = PaymentMethod.Vnpay,
            Status = PaymentStatus.Succeeded, Amount = 50000, IdempotencyKey = "BK-0001-Vnpay",
            ProviderTransactionId = "VNP14500001", ProviderResponseCode = "00", PaidAtUtc = paidCompleted,
            Invoice = new Invoice
            {
                InvoiceNumber = "HD-2026-000001", BuyerName = "Nguyễn Văn An", BuyerEmail = "driver1@smartparking.vn",
                SubTotal = 45455, VatAmount = 4545, Total = 50000, IssuedAtUtc = paidCompleted
            }
        };
        var confirmed = new Payment
        {
            Code = "PM-0002", BookingId = DemoIds.BookingConfirmed, BookingCode = "BK-0002", UserId = DemoIds.Driver1User,
            ParkingLotId = DemoIds.LotVincom, OwnerProfileId = DemoIds.OwnerVincom, Method = PaymentMethod.Vnpay,
            Status = PaymentStatus.Succeeded, Amount = 63000, IdempotencyKey = "BK-0002-Vnpay",
            ProviderTransactionId = "VNP14500002", ProviderResponseCode = "00", PaidAtUtc = now
        };
        db.Payments.Add(completed);
        await db.SaveChangesAsync(cancellationToken);
        db.Payments.Add(confirmed);
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Seed PaymentService xong: 3 bảng giá, 1 khuyến mãi, 2 thanh toán.");
    }

    /// <summary>Dữ liệu demo cho các bảng thêm ở migration AddDocumentCoverage – mỗi bảng kiểm tra riêng nên chạy được trên DB cũ.</summary>
    private static async Task SeedExtrasAsync(PaymentDbContext db, CancellationToken cancellationToken)
    {
        if (!await db.PromotionRedemptions.AnyAsync(cancellationToken) && await db.Payments.AnyAsync(p => p.Id == DemoIds.PaymentConfirmed, cancellationToken))
        {
            db.PromotionRedemptions.Add(new PromotionRedemption
            {
                PromotionId = DemoIds.PromotionWelcome10, UserId = DemoIds.Driver1User, BookingId = DemoIds.BookingConfirmed,
                PaymentId = DemoIds.PaymentConfirmed, DiscountAmount = 7000
            });
        }

        if (!await db.PaymentCallbackLogs.AnyAsync(cancellationToken) && await db.Payments.AnyAsync(cancellationToken))
        {
            static string Ipn(string txn, string bk, long amount) =>
                $"{{\"vnp_TxnRef\":\"{bk}\",\"vnp_TransactionNo\":\"{txn}\",\"vnp_Amount\":\"{amount * 100}\",\"vnp_ResponseCode\":\"00\",\"vnp_SecureHash\":\"demo\"}}";

            db.PaymentCallbackLogs.AddRange(
                new PaymentCallbackLog { PaymentId = DemoIds.PaymentCompleted, Provider = PaymentMethod.Vnpay, ProviderTransactionId = "VNP14500001",
                                         RawPayload = Ipn("VNP14500001", "BK-0001", 50000), SignatureValid = true, ResultCode = "00", SourceIp = "113.160.92.202" },
                new PaymentCallbackLog { PaymentId = DemoIds.PaymentConfirmed, Provider = PaymentMethod.Vnpay, ProviderTransactionId = "VNP14500002",
                                         RawPayload = Ipn("VNP14500002", "BK-0002", 63000), SignatureValid = true, ResultCode = "00", SourceIp = "113.160.92.202" },
                // VNPAY gửi lại IPN lần 2 → nhận diện trùng, không cộng tiền lần nữa.
                new PaymentCallbackLog { PaymentId = DemoIds.PaymentConfirmed, Provider = PaymentMethod.Vnpay, ProviderTransactionId = "VNP14500002",
                                         RawPayload = Ipn("VNP14500002", "BK-0002", 63000), SignatureValid = true, IsDuplicate = true, ResultCode = "02", SourceIp = "113.160.92.202" });
        }

        if (!await db.CompensationVouchers.AnyAsync(cancellationToken))
        {
            db.CompensationVouchers.Add(new CompensationVoucher
            {
                Code = "COMP-0001", UserId = DemoIds.Driver2User, Amount = 50000,
                Reason = "Bãi TSN hết chỗ dù booking đã xác nhận – đền bù bất tiện (US-091)",
                ChargedToOwnerProfileId = DemoIds.OwnerTsn, ExpiresAtUtc = DateTime.UtcNow.AddDays(30),
                Status = VoucherStatus.Issued, IssuedByUserId = DemoIds.AdminUser
            });
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
