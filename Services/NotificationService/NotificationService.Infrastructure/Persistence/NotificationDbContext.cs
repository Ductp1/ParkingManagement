using Microsoft.EntityFrameworkCore;
using NotificationService.Domain.Entities;
using ParkingManagement.ServiceDefaults.Persistence;

namespace NotificationService.Infrastructure.Persistence;

/// <summary>Database riêng của NotificationService: PM_NotificationDb. Thông báo in-app/email/SMS và mẫu nội dung.</summary>
public sealed class NotificationDbContext(DbContextOptions<NotificationDbContext> options) : ServiceDbContext(options)
{
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<NotificationTemplate> NotificationTemplates => Set<NotificationTemplate>();
    public DbSet<NotificationPreference> NotificationPreferences => Set<NotificationPreference>();
    public DbSet<DeviceToken> DeviceTokens => Set<DeviceToken>();
}
