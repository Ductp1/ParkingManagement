using AdminService.Application;
using AdminService.Application.Features.AuditLogs;
using AdminService.Domain.Entities;
using AdminService.Infrastructure;
using AdminService.Infrastructure.Persistence;
using AdminService.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using ParkingManagement.ServiceDefaults.Messaging;

namespace AdminService.Test;

// T-802: bảng InboxReceipts + repository nhận AuditRecorded. AdminDbContext dùng Npgsql nhưng không cần PostgreSQL:
// chỉ đọc model, hoặc kiểm tra lỗi kết nối không bị nuốt thành "Duplicate".
public class AuditEventInboxPersistenceTests
{
    private static readonly DateTime Now = new(2026, 10, 27, 8, 0, 0, DateTimeKind.Utc);

    private static AdminDbContext CreateContext()
        => new(new DbContextOptionsBuilder<AdminDbContext>()
            .UseNpgsql("Host=audit-inbox.invalid;Database=pm_admin_test;Timeout=1")
            .Options);

    private static AuditLog Log() => AuditLog.FromIntegrationEvent("ParkingService", 1, null, "KybApplication.Approved",
        "KybApplication", "7", null, null, null, null, Now);

    // ==================== Model ====================

    [Fact]
    public void Inbox_receipts_table_has_event_id_as_primary_key()
    {
        using var db = CreateContext();
        var entity = db.Model.FindEntityType(typeof(InboxReceipt))!;

        Assert.Equal("InboxReceipts", entity.GetTableName());
        var key = entity.FindPrimaryKey()!;
        Assert.Equal(nameof(InboxReceipt.EventId), Assert.Single(key.Properties).Name);
        Assert.Equal(AuditEventInboxRepository.InboxPrimaryKey, key.GetName());       // repository nhận diện trùng theo tên này
        Assert.Equal("uuid", entity.FindProperty(nameof(InboxReceipt.EventId))!.GetColumnType());
        Assert.Equal("timestamp with time zone", entity.FindProperty(nameof(InboxReceipt.ProcessedAtUtc))!.GetColumnType());
        Assert.Equal(2, entity.GetProperties().Count());
    }

    [Fact]
    public void Audit_logs_table_is_unchanged_by_the_inbox()
    {
        // Chống trùng nằm ở bảng riêng: AuditLogs của US-096 / US-098 / US-100 không thêm cột hay unique index nào.
        using var db = CreateContext();
        var auditLog = db.Model.FindEntityType(typeof(AuditLog))!;

        Assert.DoesNotContain(auditLog.GetProperties(), p => p.Name.Contains("Event", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(auditLog.GetIndexes(), i => i.IsUnique);
        Assert.Empty(auditLog.GetForeignKeys());
    }

    [Fact]
    public void Migration_creates_inbox_receipts_table()
    {
        using var db = CreateContext();

        Assert.Contains(db.Database.GetMigrations(), m => m.EndsWith("_AddInboxReceiptsForAuditEvents", StringComparison.Ordinal));
    }

    // ==================== Nhận diện trùng ====================

    private static PostgresException Postgres(string sqlState, string? constraintName)
        => new("duplicate key value violates unique constraint", "ERROR", "ERROR", sqlState, constraintName: constraintName);

    [Fact]
    public void Unique_violation_on_inbox_primary_key_is_a_duplicate_event()
        => Assert.True(AuditEventInboxRepository.IsDuplicateReceipt(
            new DbUpdateException("trùng", Postgres(PostgresErrorCodes.UniqueViolation, "PK_InboxReceipts"))));

    [Theory]
    [InlineData(PostgresErrorCodes.UniqueViolation, "PK_AuditLogs")]           // ràng buộc khác: không phải sự kiện trùng
    [InlineData(PostgresErrorCodes.UniqueViolation, null)]
    [InlineData(PostgresErrorCodes.ForeignKeyViolation, "PK_InboxReceipts")]
    [InlineData(PostgresErrorCodes.SerializationFailure, null)]
    public void Other_database_errors_are_not_treated_as_duplicates(string sqlState, string? constraintName)
        => Assert.False(AuditEventInboxRepository.IsDuplicateReceipt(new DbUpdateException("lỗi", Postgres(sqlState, constraintName))));

    [Fact]
    public void Update_error_without_postgres_cause_is_not_a_duplicate()
    {
        Assert.False(AuditEventInboxRepository.IsDuplicateReceipt(new DbUpdateException("lỗi")));
        Assert.False(AuditEventInboxRepository.IsDuplicateReceipt(new DbUpdateException("lỗi", new TimeoutException())));
    }

    [Fact]
    public async Task Database_unavailable_propagates_instead_of_reporting_duplicate()
    {
        // Mất kết nối phải nổi lên (5xx) để dispatcher retry; trả "Duplicate" sẽ làm mất sự kiện.
        await using var db = CreateContext();
        var repository = new AuditEventInboxRepository(db);

        await Assert.ThrowsAnyAsync<Exception>(() => repository.TryRecordAsync(Guid.NewGuid(), Log(), Now, CancellationToken.None));
    }

    [Fact]
    public async Task Receipt_and_audit_log_are_staged_in_the_same_save()
    {
        await using var db = CreateContext();
        var eventId = Guid.NewGuid();
        var log = Log();

        await Assert.ThrowsAnyAsync<Exception>(() => new AuditEventInboxRepository(db).TryRecordAsync(eventId, log, Now, CancellationToken.None));

        // Lần SaveChanges duy nhất mang cả 2 entity; thất bại thì không có entity nào được ghi riêng lẻ.
        var receipt = Assert.Single(db.ChangeTracker.Entries<InboxReceipt>());
        Assert.Equal(EntityState.Added, receipt.State);
        Assert.Equal(eventId, receipt.Entity.EventId);
        Assert.Equal(Now, receipt.Entity.ProcessedAtUtc);
        Assert.Same(log, Assert.Single(db.ChangeTracker.Entries<AuditLog>()).Entity);
    }

    // ==================== Đăng ký DI ====================

    [Fact]
    public void Use_case_and_repository_are_registered()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:ServiceDb"] = "Host=audit-inbox.invalid;Database=pm_admin_test",
            ["Services:GatewayBaseUrl"] = "http://localhost:5000",
        }).Build();
        var services = new ServiceCollection().AddLogging().AddSingleton(TimeProvider.System);
        services.AddAdminApplication();
        services.AddAdminInfrastructure(configuration);

        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        using var scope = provider.CreateScope();

        Assert.IsType<RecordAuditEventUseCase>(scope.ServiceProvider.GetRequiredService<IRecordAuditEventUseCase>());
        Assert.IsType<AuditEventInboxRepository>(scope.ServiceProvider.GetRequiredService<IAuditEventInboxRepository>());
    }
}
