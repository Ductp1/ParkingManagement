using AdminService.Domain.Entities;
using AdminService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace AdminService.Test;

// US-098: cấu hình EF của bảng SystemConfigChanges. Chỉ đọc model của AdminDbContext – không mở kết nối database.
public class SystemConfigChangeModelTests
{
    [Fact]
    public void Changes_are_stored_in_their_own_table_with_bounded_text_columns()
    {
        using var db = CreateContext();
        var entity = ChangeEntity(db);

        Assert.Equal("SystemConfigChanges", entity.GetTableName());
        AssertText(entity, nameof(SystemConfigChange.Value), maxLength: 1000, nullable: false);
        AssertText(entity, nameof(SystemConfigChange.Reason), maxLength: 1000, nullable: false);      // khớp AuditLog.Reason
        AssertText(entity, nameof(SystemConfigChange.CancelReason), maxLength: 1000, nullable: true);
        Assert.True(entity.FindProperty(nameof(SystemConfigChange.CancelledAtUtc))!.IsNullable);
        Assert.True(entity.FindProperty(nameof(SystemConfigChange.CancelledByUserId))!.IsNullable);
    }

    [Fact]
    public void One_config_cannot_have_two_live_changes_on_the_same_effective_date()
    {
        using var db = CreateContext();

        var index = Assert.Single(ChangeEntity(db).GetIndexes(), i => i.IsUnique);

        Assert.Equal([nameof(SystemConfigChange.SystemConfigId), nameof(SystemConfigChange.EffectiveFromUtc)], index.Properties.Select(p => p.Name));
        Assert.Equal("IX_SystemConfigChanges_SystemConfigId_EffectiveFromUtc_Active", index.GetDatabaseName());
        // Thay đổi đã hủy rời khỏi index: mốc hiệu lực đó được đặt lại.
        Assert.Equal("\"CancelledAtUtc\" IS NULL", index.GetFilter());
    }

    [Fact]
    public void Change_points_to_its_config_and_the_config_cannot_be_deleted_while_it_has_history()
    {
        using var db = CreateContext();

        var foreignKey = Assert.Single(ChangeEntity(db).GetForeignKeys());

        Assert.Equal(typeof(SystemConfig), foreignKey.PrincipalEntityType.ClrType);
        Assert.Equal(nameof(SystemConfigChange.SystemConfigId), Assert.Single(foreignKey.Properties).Name);
        Assert.Equal(DeleteBehavior.Restrict, foreignKey.DeleteBehavior);
    }

    [Fact]
    public void Latest_migration_matches_the_model()
    {
        // Sửa entity / cấu hình / dữ liệu seed mà quên tạo migration thì test này báo.
        using var db = CreateContext();

        Assert.False(db.Database.HasPendingModelChanges());
    }

    private static AdminDbContext CreateContext()
        => new(new DbContextOptionsBuilder<AdminDbContext>().UseNpgsql("Host=unused").Options);

    private static IEntityType ChangeEntity(AdminDbContext db)
        => db.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(SystemConfigChange))!;

    private static void AssertText(IEntityType entity, string propertyName, int maxLength, bool nullable)
    {
        var property = entity.FindProperty(propertyName)!;
        Assert.Equal(maxLength, property.GetMaxLength());
        Assert.Equal(nullable, property.IsNullable);
    }
}
