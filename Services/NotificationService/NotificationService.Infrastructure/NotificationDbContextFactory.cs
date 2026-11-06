using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using NotificationService.Infrastructure.Persistence;

namespace NotificationService.Infrastructure;

/// <summary>
/// Design-time factory cho `dotnet ef migrations` (TV6).
/// Nhờ có lớp này, `dotnet ef` dựng NotificationDbContext TRỰC TIẾP mà không phải
/// chạy Program.cs – tránh tác phụ InitializeDatabaseAsync/seed khi model đang có
/// thay đổi chờ migration. Connection string khớp appsettings.json của API.
/// </summary>
public sealed class NotificationDbContextFactory : IDesignTimeDbContextFactory<NotificationDbContext>
{
    public NotificationDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<NotificationDbContext>()
            .UseNpgsql("Host=localhost;Port=5432;Database=pm_notification;Username=postgres;Password=postgres")
            .Options;

        return new NotificationDbContext(options);
    }
}