using AdminService.Domain.Entities;
using AdminService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AdminService.Test;

// US-100 AC2: audit log không sửa / xóa được qua EF. AdminDbContext dùng Npgsql nhưng không mở kết nối:
// guard chạy trước khi EF chạm database, nên các ca bị chặn không cần PostgreSQL.
public class AuditLogAppendOnlyTests
{
    private static readonly DateTime Now = new(2026, 10, 23, 8, 0, 0, DateTimeKind.Utc);

    private static AdminDbContext CreateContext()
        => new(new DbContextOptionsBuilder<AdminDbContext>()
            .UseNpgsql("Host=audit-guard.invalid;Database=pm_admin_test;Timeout=1")
            .Options);

    private static AuditLog ExistingLog() => AuditLogs.Create(18, Now, "AdminService", 1, 2, "Owner.Locked", "OwnerProfile", "12",
        "Gian lận doanh thu", "{\"isLocked\":false}", "{\"isLocked\":true}");

    [Fact]
    public async Task Saving_a_modified_audit_log_is_rejected()
    {
        await using var db = CreateContext();
        var log = ExistingLog();
        db.AuditLogs.Attach(log);
        log.Reason = "Sửa lý do";

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        Assert.Contains("#18", error.Message);
        Assert.Contains("không được sửa hoặc xóa", error.Message);
    }

    [Fact]
    public async Task Saving_a_deleted_audit_log_is_rejected()
    {
        await using var db = CreateContext();
        db.AuditLogs.Attach(ExistingLog());
        db.AuditLogs.Remove(db.AuditLogs.Local.Single());

        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public void Synchronous_save_is_guarded_too()
    {
        using var db = CreateContext();
        var log = ExistingLog();
        db.AuditLogs.Attach(log);
        db.Entry(log).State = EntityState.Modified;

        Assert.Throws<InvalidOperationException>(() => db.SaveChanges());
    }

    [Fact]
    public void Adding_new_audit_logs_and_unchanged_ones_pass_the_guard()
    {
        using var db = CreateContext();
        db.AuditLogs.Attach(ExistingLog());                                                         // Unchanged
        db.AuditLogs.Add(new AuditLog { SourceService = "AdminService", Action = "Owner.Unlocked", EntityName = "OwnerProfile" });

        AuditLogAppendOnlyGuard.Check(db.ChangeTracker);
    }

    [Fact]
    public void Changes_to_other_entities_are_not_affected()
    {
        using var db = CreateContext();
        var flag = new FeatureFlag { Key = "FEATURE_3D_MAP" };
        typeof(ParkingManagement.SharedKernel.Domain.BaseEntity).GetProperty("Id")!.SetValue(flag, 3);   // dòng đã có trong database
        db.FeatureFlags.Attach(flag);
        flag.IsEnabled = true;
        db.AuditLogs.Add(new AuditLog { SourceService = "AdminService", Action = "FeatureFlag.Updated", EntityName = "FeatureFlag" });

        AuditLogAppendOnlyGuard.Check(db.ChangeTracker);
    }
}
