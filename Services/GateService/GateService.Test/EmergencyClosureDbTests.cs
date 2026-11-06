using System.Text.Json;
using GateService.Application.Features.LotCapacity;
using GateService.Domain.Entities;
using GateService.Domain.Enums;
using GateService.Infrastructure.Integrations;
using GateService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using ParkingManagement.ServiceDefaults.Messaging;
using ParkingManagement.SharedKernel.Contracts;

namespace GateService.Test;

/// <summary>
/// US-069 – Emergency Stop phía GateService (PostgreSQL thật): consumer LotEmergencyStopped/Lifted
/// cập nhật cache LotOperationalStatus, LotAdmissionGate CHẶN mọi check-in mới khi bãi đóng
/// (vãng lai lẫn booking QR) và mở lại khi lift. Check-out không qua gate admit nên xe trong bãi
/// vẫn thoát an toàn. Mỗi test tự tạo/drop database pm_us069_* riêng (pattern US-066).
/// </summary>
public class EmergencyClosureDbTests
{
    private const string AdminCs = "Host=localhost;Port=5432;Username=postgres;Password=postgres;Database=postgres";

    private static async Task<string> CreateScratchDbAsync()
    {
        var name = "pm_us069_" + Guid.NewGuid().ToString("N")[..12];
        await using var conn = new NpgsqlConnection(AdminCs);
        await conn.OpenAsync();
        await using (var drop = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{name}\" WITH (FORCE)", conn))
            await drop.ExecuteNonQueryAsync();
        await using (var create = new NpgsqlCommand($"CREATE DATABASE \"{name}\"", conn))
            await create.ExecuteNonQueryAsync();
        return $"Host=localhost;Port=5432;Username=postgres;Password=postgres;Database={name}";
    }

    private static async Task DropDbAsync(string connectionString)
    {
        var name = new NpgsqlConnectionStringBuilder(connectionString).Database!;
        await using var conn = new NpgsqlConnection(AdminCs);
        await conn.OpenAsync();
        await using var drop = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{name}\" WITH (FORCE)", conn);
        await drop.ExecuteNonQueryAsync();
    }

    private static DbContextOptions<GateDbContext> Options(string connectionString)
        => new DbContextOptionsBuilder<GateDbContext>().UseNpgsql(connectionString).Options;

    /// <summary>Bãi C=5, B=3 (vãng lai), bộ đếm 2/1 — còn room để admit 2 lần trong 1 test.</summary>
    private static async Task SeedOpenLotAsync(string cs)
    {
        await using var db = new GateDbContext(Options(cs));
        await db.Database.EnsureCreatedAsync();
        db.LotCapacityConfigs.Add(new LotCapacityConfig
        {
            ParkingLotId = 3, Capacity = 5, TransientBuffer = 3, SubscriberQuota = 2, Version = 1,
        });
        db.LotAdmissionCounters.Add(new LotAdmissionCounter
        {
            ParkingLotId = 3, Total = 2, Transient = 1, Subscriber = 1,
        });
        await db.SaveChangesAsync();
    }

    private static IntegrationEventEnvelope Envelope<T>(T evt) where T : IntegrationEvent
        => new(evt.EventId, typeof(T).Name, evt.OccurredAtUtc,
            JsonSerializer.SerializeToElement(evt, typeof(T)));

    private static async Task<GateOperationalStatusHandler> HandlerAsync(string cs)
    {
        var db = new GateDbContext(Options(cs));
        await db.Database.EnsureCreatedAsync();
        return new GateOperationalStatusHandler(db, NullLogger<GateOperationalStatusHandler>.Instance);
    }

    [Fact]
    public async Task Emergency_stop_blocks_all_checkins_then_lift_reopens()
    {
        var cs = await CreateScratchDbAsync();
        try
        {
            await SeedOpenLotAsync(cs);
            var handler = await HandlerAsync(cs);

            // Trước khi stop: vãng lai vào bình thường.
            await using (var db = new GateDbContext(Options(cs)))
            {
                var (admitted, _) = await new LotAdmissionGate(db).TryAdmitAsync(3, AdmissionType.Transient);
                Assert.True(admitted);
            }

            // Chủ bãi đóng bãi khẩn cấp → consumer cache trạng thái.
            var stopped = new LotEmergencyStopped(3, "Bãi Vincom", OwnerProfileId: 7, StoppedByUserId: 9,
                Reason: "Cháy hệ thống điện tầng B1", StoppedAtUtc: DateTime.UtcNow, AffectedBookings: []);
            await handler.HandleAsync(Envelope(stopped), CancellationToken.None);

            await using (var db = new GateDbContext(Options(cs)))
            {
                var gate = new LotAdmissionGate(db);
                var (transientAdmitted, transientReason) = await gate.TryAdmitAsync(3, AdmissionType.Transient);
                Assert.False(transientAdmitted);                       // chặn vãng lai
                Assert.Contains("LOT_CLOSED", transientReason);

                var (bookingAdmitted, bookingReason) = await gate.TryAdmitAsync(3, AdmissionType.Subscriber);
                Assert.False(bookingAdmitted);                          // chặn cả thuê bao/booking QR
                Assert.Contains("LOT_CLOSED", bookingReason);

                var status = await db.LotOperationalStatuses.SingleAsync(s => s.ParkingLotId == 3);
                Assert.True(status.IsClosed);
                Assert.Equal("Cháy hệ thống điện tầng B1", status.ClosedReason);
            }

            // Gỡ đóng bãi → mở lại, check-in hoạt động trở lại (consumer replay cùng EventId bỏ qua — idempotent).
            var lifted = new LotEmergencyLifted(3, "Bãi Vincom", OwnerProfileId: 7, LiftedByUserId: 9,
                Reason: "Đã xử lý xong sự cố", LiftedAtUtc: DateTime.UtcNow);
            await handler.HandleAsync(Envelope(lifted), CancellationToken.None);
            await handler.HandleAsync(Envelope(lifted), CancellationToken.None);

            await using (var db = new GateDbContext(Options(cs)))
            {
                var status = await db.LotOperationalStatuses.SingleAsync(s => s.ParkingLotId == 3);
                Assert.False(status.IsClosed);

                var (admitted, _) = await new LotAdmissionGate(db).TryAdmitAsync(3, AdmissionType.Transient);
                Assert.True(admitted);
            }
        }
        finally
        {
            await DropDbAsync(cs);
        }
    }
}

