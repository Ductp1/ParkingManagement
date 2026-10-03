using System.Text.Json;
using BookingService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ParkingManagement.ServiceDefaults.Persistence;
using ParkingManagement.SharedKernel.Contracts;
using ParkingManagement.SharedKernel.Enums;

namespace BookingService.Infrastructure.Persistence.Seeding;

/// <summary>
/// 3 booking demo: BK-0001 Completed (hôm qua), BK-0002 Confirmed (ngày mai, dùng mã WELCOME10), BK-0003 PendingPayment (đang giữ chỗ).
/// Các ID User/Vehicle/Lot/Slot lấy từ DemoIds để khớp với database của service khác.
/// </summary>
public sealed class BookingDataSeeder(ILogger<BookingDataSeeder> logger) : IDataSeeder<BookingDbContext>
{
    private static readonly string VincomRules = JsonSerializer.Serialize(new[]
    {
        new { FromMinute = 0, ToMinute = (int?)60, BlockMinutes = 60, PricePerBlock = 30000 },
        new { FromMinute = 60, ToMinute = (int?)240, BlockMinutes = 60, PricePerBlock = 20000 },
        new { FromMinute = 240, ToMinute = (int?)null, BlockMinutes = 60, PricePerBlock = 15000 },
    });

    public async Task SeedAsync(BookingDbContext db, CancellationToken cancellationToken)
    {
        await SeedCoreAsync(db, cancellationToken);
        await SeedExtrasAsync(db, cancellationToken);
    }

    /// <summary>Dữ liệu demo ban đầu (migration InitialCreate).</summary>
    private async Task SeedCoreAsync(BookingDbContext db, CancellationToken cancellationToken)
    {
        if (await db.Bookings.AnyAsync(cancellationToken)) return;
        var now = DateTime.UtcNow;
        var day = now.Date;

        var completed = New("BK-0001", DemoIds.Driver1User, DemoIds.Driver1Car, "51F12345", VehicleType.Sedan,
            DemoIds.LotVincom, DemoIds.OwnerVincom, "Bãi xe Vincom Đồng Khởi", DemoIds.ZoneVincomB, DemoIds.SlotVincomB1A01, "B1-A01",
            day.AddDays(-1).AddHours(1), 120, BookingStatus.Completed, 50000, DemoIds.RateCardVincom, VincomRules);
        completed.CheckedInAtUtc = completed.StartAtUtc.AddMinutes(5);
        completed.CheckedOutAtUtc = completed.EndAtUtc.AddMinutes(-10);
        completed.CompletedAtUtc = completed.CheckedOutAtUtc;
        completed.StatusLogs.Add(new BookingStatusLog { FromStatus = BookingStatus.CheckedOut, ToStatus = BookingStatus.Completed });

        var upcoming = New("BK-0002", DemoIds.Driver1User, DemoIds.Driver1Car, "51F12345", VehicleType.Sedan,
            DemoIds.LotVincom, DemoIds.OwnerVincom, "Bãi xe Vincom Đồng Khởi", DemoIds.ZoneVincomB, DemoIds.SlotVincomB1A02, "B1-A02",
            day.AddDays(1).AddHours(2), 180, BookingStatus.Confirmed, 70000, DemoIds.RateCardVincom, VincomRules);
        upcoming.PromotionId = DemoIds.PromotionWelcome10;
        upcoming.PromotionCode = "WELCOME10";
        upcoming.DiscountAmount = 7000;
        upcoming.PaidAmount = 63000;
        upcoming.PriceSnapshot!.DiscountAmount = 7000;
        upcoming.PriceSnapshot.FinalAmount = 63000;

        var pending = New("BK-0003", DemoIds.Driver2User, DemoIds.Driver2Ev, "51H91991", VehicleType.Sedan,
            DemoIds.LotTsn, DemoIds.OwnerTsn, "Bãi xe Sân bay Tân Sơn Nhất (P2)", DemoIds.ZoneTsnP2, DemoIds.SlotTsnMdA03, "MD-A03",
            now.AddHours(2), 120, BookingStatus.PendingPayment, 40000, DemoIds.RateCardTsn, "[]");
        pending.HoldExpiresAtUtc = now.AddMinutes(15);
        pending.ConfirmedAtUtc = null;
        pending.PaidAmount = 0;
        pending.PriceSnapshot = null;
        pending.QrToken = null;

        foreach (var b in new[] { completed, upcoming, pending })
        {
            db.Bookings.Add(b);
            await db.SaveChangesAsync(cancellationToken);
        }
        logger.LogInformation("Seed BookingService xong: 3 booking.");
    }

    private static Booking New(string code, int userId, int vehicleId, string plate, VehicleType vehicleType,
        int lotId, int ownerId, string lotName, int zoneId, int slotId, string slotCode,
        DateTime start, int minutes, BookingStatus status, decimal amount, int rateCardId, string rulesJson)
    {
        var b = new Booking
        {
            Code = code, UserId = userId, VehicleId = vehicleId, PlateNumber = plate, VehicleType = vehicleType,
            ParkingLotId = lotId, OwnerProfileId = ownerId, ParkingLotName = lotName, ZoneId = zoneId, SlotId = slotId, SlotCode = slotCode,
            StartAtUtc = start, EndAtUtc = start.AddMinutes(minutes), Status = status, ConfirmedAtUtc = start.AddDays(-1),
            TotalAmount = amount, PaidAmount = amount, QrToken = $"demo-qr-{code}",
            PriceSnapshot = new PriceSnapshot
            {
                RateCardId = rateCardId, RateCardJson = rulesJson, BaseAmount = amount, FinalAmount = amount, OverstayMultiplier = 1.5m
            }
        };
        b.StatusLogs.Add(new BookingStatusLog { ToStatus = BookingStatus.PendingPayment, ChangedByUserId = userId });
        if (status != BookingStatus.PendingPayment)
            b.StatusLogs.Add(new BookingStatusLog { FromStatus = BookingStatus.PendingPayment, ToStatus = BookingStatus.Confirmed, Reason = "VNPAY callback thành công" });
        return b;
    }

    /// <summary>Dữ liệu demo cho các bảng thêm ở migration AddDocumentCoverage – mỗi bảng kiểm tra riêng nên chạy được trên DB cũ.</summary>
    private static async Task SeedExtrasAsync(BookingDbContext db, CancellationToken cancellationToken)
    {
        if (!await db.BookingModifications.AnyAsync(cancellationToken))
        {
            var bk2 = await db.Bookings.FirstOrDefaultAsync(b => b.Id == DemoIds.BookingConfirmed, cancellationToken);
            if (bk2 is not null)
            {
                // BK-0002 ban đầu đặt 2 giờ, tài xế đổi thành 3 giờ trước khi check-in → trả thêm 20.000đ.
                db.BookingModifications.Add(new BookingModification
                {
                    BookingId = bk2.Id, ModificationType = BookingModificationType.ChangeTime,
                    OldStartAtUtc = bk2.StartAtUtc, OldEndAtUtc = bk2.StartAtUtc.AddHours(2),
                    NewStartAtUtc = bk2.StartAtUtc, NewEndAtUtc = bk2.EndAtUtc,
                    PriceDifference = 20000, RequestedByUserId = DemoIds.Driver1User
                });
            }
        }

        if (!await db.MonthlyPasses.AnyAsync(cancellationToken))
        {
            // Phase 2 – vé tháng cho chiếc SUV của driver1 tại Vincom (chưa gán Dedicated Slot).
            var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
            db.MonthlyPasses.Add(new MonthlyPass
            {
                Code = "MP-0001", UserId = DemoIds.Driver1User, VehicleId = DemoIds.Driver1Suv, PlateNumber = "30A67890",
                ParkingLotId = DemoIds.LotVincom, OwnerProfileId = DemoIds.OwnerVincom,
                ValidFrom = new DateOnly(today.Year, today.Month, 1), ValidTo = new DateOnly(today.Year, today.Month, 1).AddMonths(1).AddDays(-1),
                Price = 2500000, AutoRenew = true, Status = MonthlyPassStatus.Active
            });
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
