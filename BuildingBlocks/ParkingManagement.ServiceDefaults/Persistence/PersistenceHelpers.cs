using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ParkingManagement.ServiceDefaults.Persistence;

public static class ModelBuilderExtensions
{
    /// <summary>Cột text (không giới hạn) cho JSON / văn bản dài (ghi đè quy ước mặc định 500 ký tự).</summary>
    public static PropertyBuilder<T> IsMaxText<T>(this PropertyBuilder<T> builder)
        => builder.HasColumnType("text");
}

/// <summary>Mốc thời gian cố định cho dữ liệu HasData – phải là hằng số để migration không đổi mỗi lần build.</summary>
public static class SeedClock
{
    public static readonly DateTime SeededAtUtc = new(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);
}

/// <summary>Mỗi service cài đặt interface này để nạp dữ liệu demo vào database của mình.</summary>
public interface IDataSeeder<in TContext> where TContext : DbContext
{
    Task SeedAsync(TContext db, CancellationToken cancellationToken);
}
