using GateService.Application.Features.LotCapacity;
using GateService.Domain.Entities;
using GateService.Domain.Enums;
using GateService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace GateService.Test;

/// <summary>
/// US-066 – CHỐNG RACE CONDITION với PostgreSQL thật: N request vào cùng lúc khi còn 1 chỗ
/// thì đúng 1 thành công; vãng lai chặn khi hết buffer; xe ra trừ đúng bộ đếm theo loại.
/// Mỗi test tự tạo/drop database pm_us066_* riêng.
/// </summary>
public class LotAdmissionGateDbTests
{
    private const string AdminCs = "Host=localhost;Port=5432;Username=postgres;Password=postgres;Database=postgres";

    private static async Task<string> CreateScratchDbAsync()
    {
        var name = "pm_us066_" + Guid.NewGuid().ToString("N")[..12];
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

    private static async Task SeedAsync(string cs, int capacity, int buffer, int total, int transient)
    {
        await using var db = new GateDbContext(Options(cs));
        await db.Database.EnsureCreatedAsync();
        db.LotCapacityConfigs.Add(new LotCapacityConfig
        {
            ParkingLotId = 3, Capacity = capacity, TransientBuffer = buffer, SubscriberQuota = 100, Version = 1,
        });
        db.LotAdmissionCounters.Add(new LotAdmissionCounter
        {
            ParkingLotId = 3, Total = total, Transient = transient, Subscriber = total - transient,
        });
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Concurrent_checkins_when_one_slot_left_exactly_one_succeeds()
    {
        var cs = await CreateScratchDbAsync();
        try
        {
            // C = 5, B = 5: T = 4 → còn đúng 1 chỗ tổng; V = 4 < B = 5 vẫn đủ điều kiện.
            await SeedAsync(cs, capacity: 5, buffer: 5, total: 4, transient: 4);

            var options = Options(cs);
            var results = await Task.WhenAll(Enumerable.Range(0, 8).Select(async _ =>
            {
                await using var db = new GateDbContext(options);
                var gate = new LotAdmissionGate(db);
                return await gate.TryAdmitAsync(3, AdmissionType.Transient);
            }));

            Assert.Equal(1, results.Count(r => r.Admitted));

            await using var verify = new GateDbContext(options);
            var counter = await verify.LotAdmissionCounters.SingleAsync(c => c.ParkingLotId == 3);
            Assert.Equal(5, counter.Total);
            Assert.Equal(5, counter.Transient);
        }
        finally
        {
            await DropDbAsync(cs);
        }
    }

    [Fact]
    public async Task Transient_blocked_at_buffer_while_subscriber_still_enters()
    {
        var cs = await CreateScratchDbAsync();
        try
        {
            // C = 5, B = 1: T = 1, V = 1 → vãng lai hết buffer; thuê bao vẫn vào được (T < C).
            await SeedAsync(cs, capacity: 5, buffer: 1, total: 1, transient: 1);

            await using (var db = new GateDbContext(Options(cs)))
            {
                var gate = new LotAdmissionGate(db);
                var (transientAdmitted, transientReason) = await gate.TryAdmitAsync(3, AdmissionType.Transient);
                Assert.False(transientAdmitted);
                Assert.Equal(AdmissionRejectReasons.TransientBufferFull, transientReason);

                var (subscriberAdmitted, _) = await gate.TryAdmitAsync(3, AdmissionType.Subscriber);
                Assert.True(subscriberAdmitted);
            }

            await using var verify = new GateDbContext(Options(cs));
            var counter = await verify.LotAdmissionCounters.SingleAsync(c => c.ParkingLotId == 3);
            Assert.Equal(2, counter.Total);
            Assert.Equal(1, counter.Transient);
            Assert.Equal(1, counter.Subscriber);
        }
        finally
        {
            await DropDbAsync(cs);
        }
    }

    [Fact]
    public async Task Release_decrements_by_recorded_type_and_never_goes_negative()
    {
        var cs = await CreateScratchDbAsync();
        try
        {
            await SeedAsync(cs, capacity: 5, buffer: 2, total: 3, transient: 2);

            await using var db = new GateDbContext(Options(cs));
            var gate = new LotAdmissionGate(db);

            await gate.ReleaseAsync(3, AdmissionType.Transient);
            await gate.ReleaseAsync(3, AdmissionType.Subscriber);
            await gate.ReleaseAsync(3, AdmissionType.Transient);
            await gate.ReleaseAsync(3, AdmissionType.Transient); // dư → guard chặn, không âm

            var counter = await db.LotAdmissionCounters.AsNoTracking().SingleAsync(c => c.ParkingLotId == 3);
            Assert.Equal(0, counter.Total);
            Assert.Equal(0, counter.Transient);
            Assert.Equal(0, counter.Subscriber);
        }
        finally
        {
            await DropDbAsync(cs);
        }
    }
}