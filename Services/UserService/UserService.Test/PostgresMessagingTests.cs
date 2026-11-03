using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using ParkingManagement.ServiceDefaults.Messaging;
using ParkingManagement.SharedKernel.Domain;
using UserService.Infrastructure.Persistence;

namespace UserService.Test;

public sealed class MessagingPostgresFactAttribute : FactAttribute
{
    public MessagingPostgresFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("TV1_TEST_DB")))
            Skip = "Cần TV1_TEST_DB; mỗi test tạo và dọn database pm_tv1_s2_<guid> riêng.";
    }
}

public sealed class PostgresMessagingTests
{
    [MessagingPostgresFact]
    public async Task Dispatcher_retries_with_backoff_then_marks_processed_with_same_event_id()
    {
        await using var fixture = await Database.CreateAsync();
        var transport = new Transport { Fail = true };
        using var services = new ServiceCollection().AddDbContext<UserDbContext>(o => o.UseNpgsql(fixture.Connection))
            .AddSingleton<IOutboxTransport>(transport).BuildServiceProvider();
        var clock = new Clock();
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["EventBus:MaxAttempts"] = "2" }).Build();
        var dispatcher = new OutboxDispatcher<UserDbContext>(services.GetRequiredService<IServiceScopeFactory>(), config,
            NullLogger<OutboxDispatcher<UserDbContext>>.Instance, clock);
        var message = new OutboxMessage { EventType = "Test", PayloadJson = "{}" };
        await using (var db = fixture.Context()) { db.Add(message); await db.SaveChangesAsync(); }
        await dispatcher.DispatchOneAsync(default);
        await using (var db = fixture.Context())
        {
            var saved = await db.OutboxMessages.SingleAsync();
            Assert.Equal(1, saved.AttemptCount);
            Assert.Null(saved.ProcessedAtUtc);
            Assert.Equal(clock.Now.AddSeconds(10), saved.NextAttemptAtUtc);
            Assert.Equal(nameof(HttpRequestException), saved.LastError);
        }
        await dispatcher.DispatchOneAsync(default);
        Assert.Equal(1, transport.Deliveries);
        clock.Now = clock.Now.AddSeconds(10); transport.Fail = false;
        await Task.WhenAll(dispatcher.DispatchOneAsync(default), dispatcher.DispatchOneAsync(default));
        Assert.Equal(2, transport.Deliveries);
        Assert.All(transport.Ids, id => Assert.Equal(message.Id, id));
        await using var verify = fixture.Context();
        var final = await verify.OutboxMessages.SingleAsync();
        Assert.NotNull(final.ProcessedAtUtc);
        Assert.Null(final.LastError);
        Assert.Null(final.NextAttemptAtUtc);
    }

    [MessagingPostgresFact]
    public async Task Dispatcher_stops_retrying_at_max_attempts()
    {
        await using var fixture = await Database.CreateAsync();
        var transport = new Transport { Fail = true };
        using var services = new ServiceCollection().AddDbContext<UserDbContext>(o => o.UseNpgsql(fixture.Connection))
            .AddSingleton<IOutboxTransport>(transport).BuildServiceProvider();
        var clock = new Clock();
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["EventBus:MaxAttempts"] = "1" }).Build();
        var dispatcher = new OutboxDispatcher<UserDbContext>(services.GetRequiredService<IServiceScopeFactory>(), config,
            NullLogger<OutboxDispatcher<UserDbContext>>.Instance, clock);
        await using (var db = fixture.Context()) { db.Add(new OutboxMessage { EventType = "Test", PayloadJson = "{}" }); await db.SaveChangesAsync(); }
        await dispatcher.DispatchOneAsync(default);
        clock.Now = clock.Now.AddDays(1);
        await dispatcher.DispatchOneAsync(default);
        Assert.Equal(1, transport.Deliveries);
    }

    [MessagingPostgresFact]
    public async Task Inbox_rolls_back_failed_effects_and_applies_concurrent_duplicate_only_once()
    {
        await using var fixture = await Database.CreateAsync();
        var id = Guid.NewGuid();
        await using (var db = fixture.Context())
            await Assert.ThrowsAsync<InvalidOperationException>(() => db.ApplyOnceAsync(id, async ct =>
            {
                db.Add(new OutboxMessage { EventType = "Effect", PayloadJson = "{}" });
                await db.SaveChangesAsync(ct);
                throw new InvalidOperationException();
            }, default));
        await using (var db = fixture.Context())
        {
            Assert.Empty(await db.OutboxMessages.ToListAsync());
            Assert.Empty(await db.Set<InboxReceipt>().ToListAsync());
        }
        async Task<bool> Apply()
        {
            await using var db = fixture.Context();
            return await db.ApplyOnceAsync(id, ct =>
            {
                db.Add(new OutboxMessage { EventType = "Effect", PayloadJson = "{}" });
                return Task.CompletedTask;
            }, default);
        }
        var results = await Task.WhenAll(Apply(), Apply());
        Assert.Single(results, applied => applied);
        await using var verify = fixture.Context();
        Assert.Single(await verify.OutboxMessages.ToListAsync());
        Assert.Single(await verify.Set<InboxReceipt>().ToListAsync());
    }

    private sealed class Clock : TimeProvider
    {
        public DateTime Now = new(DateTime.UtcNow.AddMinutes(1).Ticks / 10 * 10, DateTimeKind.Utc);
        public override DateTimeOffset GetUtcNow() => new(Now);
    }
    private sealed class Transport : IOutboxTransport
    {
        public bool Fail;
        public int Deliveries;
        public List<Guid> Ids = [];
        public Task DeliverAsync(OutboxMessage message, CancellationToken ct)
        {
            Deliveries++; Ids.Add(message.Id);
            if (Fail) throw new HttpRequestException("private failure body");
            return Task.CompletedTask;
        }
    }
    private sealed class Database(string connection) : IAsyncDisposable
    {
        public string Connection => connection;
        public UserDbContext Context() => new(new DbContextOptionsBuilder<UserDbContext>().UseNpgsql(connection).Options);
        public static async Task<Database> CreateAsync()
        {
            var builder = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("TV1_TEST_DB"))
                { Database = "pm_tv1_s2_" + Guid.NewGuid().ToString("N") };
            var fixture = new Database(builder.ConnectionString);
            await using var db = fixture.Context();
            await db.Database.EnsureCreatedAsync();
            return fixture;
        }
        public async ValueTask DisposeAsync()
        {
            await using var db = Context();
            await db.Database.EnsureDeletedAsync();
        }
    }
}
