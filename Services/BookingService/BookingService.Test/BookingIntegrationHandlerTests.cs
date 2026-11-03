using BookingService.Domain.Entities;
using BookingService.Infrastructure.Integrations;
using BookingService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using ParkingManagement.ServiceDefaults.Messaging;
using ParkingManagement.SharedKernel.Contracts;
using ParkingManagement.SharedKernel.Enums;

namespace BookingService.Test;

/// <summary>
/// US-083: BookingIntegrationEventHandler — cầu nối còn thiếu giữa PaymentService và BookingService.
/// PaymentSucceeded → ConfirmPayment (PendingPayment/PendingOwnerApproval → Confirmed) + phát BookingConfirmed
/// ra outbox (NotificationService dùng để gửi xác nhận + đặt lịch nhắc US-084). Chạy trên PostgreSQL thật
/// (scratch DB pm_us083_*) vì cần transaction thật cho outbox + booking.
/// </summary>
public class BookingIntegrationHandlerTests
{
    private const string AdminCs = "Host=localhost;Port=5432;Username=postgres;Password=postgres;Database=postgres";

    private static async Task<string> CreateScratchDbAsync()
    {
        var name = "pm_us083_" + Guid.NewGuid().ToString("N")[..12];
        await using var conn = new Npgsql.NpgsqlConnection(AdminCs);
        await conn.OpenAsync();
        await using (var drop = new Npgsql.NpgsqlCommand($"DROP DATABASE IF EXISTS \"{name}\" WITH (FORCE)", conn))
            await drop.ExecuteNonQueryAsync();
        await using (var create = new Npgsql.NpgsqlCommand($"CREATE DATABASE \"{name}\"", conn))
            await create.ExecuteNonQueryAsync();
        return $"Host=localhost;Port=5432;Username=postgres;Password=postgres;Database={name}";
    }

    private static async Task DropDbAsync(string cs)
    {
        var name = new Npgsql.NpgsqlConnectionStringBuilder(cs).Database!;
        await using var conn = new Npgsql.NpgsqlConnection(AdminCs);
        await conn.OpenAsync();
        await using var drop = new Npgsql.NpgsqlCommand($"DROP DATABASE IF EXISTS \"{name}\" WITH (FORCE)", conn);
        await drop.ExecuteNonQueryAsync();
    }

    private static BookingDbContext NewDb(string cs)
        => new(new DbContextOptionsBuilder<BookingDbContext>().UseNpgsql(cs).Options);

    private static Booking NewPendingBooking() => new()
    {
        Code = "BK-US083-0001",
        UserId = 5,
        VehicleId = 1,
        PlateNumber = "51F-123.45",
        ParkingLotId = 2,
        OwnerProfileId = 7,
        ParkingLotName = "Bãi Test US-083",
        StartAtUtc = new DateTime(2026, 11, 5, 2, 0, 0, DateTimeKind.Utc),
        EndAtUtc = new DateTime(2026, 11, 5, 5, 0, 0, DateTimeKind.Utc),
        Status = BookingStatus.PendingPayment,
        TotalAmount = 120000m,
        HoldExpiresAtUtc = new DateTime(2026, 11, 3, 1, 30, 0, DateTimeKind.Utc),
    };

    private static BookingIntegrationEventHandler NewHandler(BookingDbContext db, DateTime nowUtc)
        => new(
            new BookingService.Infrastructure.Persistence.Repositories.BookingRepository(db),
            new BookingEventOutbox(db),
            new FixedTestClock(nowUtc),
            NullLogger<BookingIntegrationEventHandler>.Instance);

    private static IntegrationEventEnvelope PaymentEnvelope(decimal amount)
        => new(Guid.NewGuid(), nameof(PaymentSucceeded), DateTime.UtcNow,
            System.Text.Json.JsonSerializer.SerializeToElement(new PaymentSucceeded(
                PaymentId: 99, BookingId: 1, ParkingSessionId: null, Amount: amount, Method: "VNPAY")));

    [Fact]
    public async Task PaymentSucceeded_confirms_pending_booking_and_publishes_confirmed_event()
    {
        var cs = await CreateScratchDbAsync();
        try
        {
            var now = new DateTime(2026, 11, 3, 1, 0, 0, DateTimeKind.Utc);
            await using (var db = NewDb(cs))
            {
                await db.Database.EnsureCreatedAsync();
                db.Bookings.Add(NewPendingBooking());
                await db.SaveChangesAsync();
            }

            await using var db2 = NewDb(cs);
            await NewHandler(db2, now)
                .HandleAsync(PaymentEnvelope(120000m), CancellationToken.None);

            // Handler phát 2 event: BookingConfirmed (xác nhận đặt chỗ US-083) + BookingPaymentCompleted (thông báo thanh toán).
            Assert.Equal(2, db2.OutboxMessages.Count());
            var confirmedRow = db2.OutboxMessages.Single(m => m.EventType == nameof(BookingConfirmed));
            var confirmed = System.Text.Json.JsonSerializer.Deserialize<BookingConfirmed>(confirmedRow.PayloadJson);
            Assert.NotNull(confirmed);
            Assert.Equal(1, confirmed!.BookingId);
            Assert.Equal("BK-US083-0001", confirmed.Code);
            Assert.Equal(7, confirmed.OwnerProfileId);
            Assert.Equal(BookingStatus.Confirmed, (await db2.Bookings.SingleAsync()).Status);
        }
        finally
        {
            await DropDbAsync(cs);
        }
    }

    [Fact]
    public async Task PaymentSucceeded_is_idempotent_when_booking_already_confirmed()
    {
        var cs = await CreateScratchDbAsync();
        try
        {
            var now = new DateTime(2026, 11, 3, 1, 0, 0, DateTimeKind.Utc);
            await using (var db = NewDb(cs))
            {
                await db.Database.EnsureCreatedAsync();
                var booking = NewPendingBooking();
                booking.ConfirmPayment(120000m, now); // đã confirm trước đó (webhook replay)
                db.Bookings.Add(booking);
                await db.SaveChangesAsync();
            }

            await using var db2 = NewDb(cs);
            await NewHandler(db2, now)
                .HandleAsync(PaymentEnvelope(120000m), CancellationToken.None); // KHÔNG được nổ ConflictException

            // Đã confirm trước đó → KHÔNG phát BookingConfirmed; chỉ có BookingPaymentCompleted (US-083 thông báo nhận tiền trễ).
            Assert.Single(db2.OutboxMessages);
            Assert.Equal(nameof(BookingPaymentCompleted), db2.OutboxMessages.Single().EventType);
        }
        finally
        {
            await DropDbAsync(cs);
        }
    }
}

/// <summary>Đồng hồ cố định cho test.</summary>
internal sealed class FixedTestClock(DateTime utc) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => new(utc, TimeSpan.Zero);
}

