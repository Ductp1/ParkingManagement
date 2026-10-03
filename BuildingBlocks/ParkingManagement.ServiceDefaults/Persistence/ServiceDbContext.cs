using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using ParkingManagement.SharedKernel.Domain;

namespace ParkingManagement.ServiceDefaults.Persistence;

/// <summary>
/// DbContext gốc cho mọi service. Mỗi service kế thừa lớp này và có DATABASE RIÊNG (Database-per-Service).
/// Gom các quy ước dùng chung để database của 9 service có cùng "luật chơi":
///  - Enum lưu dạng chuỗi (dễ đọc khi query bằng psql), tiền decimal(18,2), chuỗi mặc định varchar(500).
///  - Khóa ngoại BÊN TRONG service: RESTRICT (không cascade). Xóa nghiệp vụ dùng soft delete (ISoftDelete).
///  - Tham chiếu SANG SERVICE KHÁC chỉ lưu ID (không có khóa ngoại) – xem database/README.md.
///  - Bảng OutboxMessages để phát sự kiện tích hợp cho service khác.
///  - Tự đóng dấu UpdatedAtUtc khi SaveChanges.
/// </summary>
public abstract class ServiceDbContext(DbContextOptions options) : DbContext(options)
{
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<decimal>().HavePrecision(18, 2);
        configurationBuilder.Properties<string>().HaveMaxLength(500);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Áp dụng mọi IEntityTypeConfiguration nằm trong project Infrastructure của service con.
        modelBuilder.ApplyConfigurationsFromAssembly(GetType().Assembly);

        modelBuilder.Entity<OutboxMessage>(e =>
        {
            e.ToTable("OutboxMessages");
            e.HasKey(x => x.Id);
            e.Property(x => x.EventType).HasMaxLength(200);
            e.Property(x => x.PayloadJson).HasColumnType("text");
            e.Property(x => x.LastError).HasMaxLength(2000);
            e.HasIndex(x => new { x.ProcessedAtUtc, x.OccurredAtUtc });
        });

        ApplySharedConventions(modelBuilder);
    }

    private static void ApplySharedConventions(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes().ToList())
        {
            var clrType = entityType.ClrType;
            var entity = modelBuilder.Entity(clrType);

            // Enum → varchar(40)
            foreach (var property in entityType.GetProperties())
            {
                var type = Nullable.GetUnderlyingType(property.ClrType) ?? property.ClrType;
                if (type.IsEnum)
                    entity.Property(property.Name).HasConversion<string>().HasMaxLength(40);
            }

            foreach (var fk in entityType.GetForeignKeys())
                fk.DeleteBehavior = DeleteBehavior.Restrict;

            // Soft delete: WHERE IsDeleted = 0 cho mọi truy vấn LINQ.
            if (typeof(ISoftDelete).IsAssignableFrom(clrType) && entityType.BaseType is null)
            {
                var parameter = Expression.Parameter(clrType, "e");
                var body = Expression.Not(Expression.Property(parameter, nameof(ISoftDelete.IsDeleted)));
                entity.HasQueryFilter(Expression.Lambda(body, parameter));
            }
        }
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        ApplyAuditStamps();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        ApplyAuditStamps();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void ApplyAuditStamps()
    {
        var now = DateTime.UtcNow;
        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.State == EntityState.Deleted && entry.Entity is ISoftDelete softDelete)
            {
                // Chặn xóa cứng: chuyển thành cập nhật IsDeleted.
                entry.State = EntityState.Modified;
                softDelete.IsDeleted = true;
                softDelete.DeletedAtUtc = now;
            }

            if (entry.State == EntityState.Modified && entry.Entity is BaseEntity)
                entry.Property(nameof(BaseEntity.UpdatedAtUtc)).CurrentValue = now;
        }
    }
}
