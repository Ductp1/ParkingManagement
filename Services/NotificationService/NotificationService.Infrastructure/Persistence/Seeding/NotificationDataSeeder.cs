using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NotificationService.Domain.Entities;
using ParkingManagement.ServiceDefaults.Persistence;
using ParkingManagement.SharedKernel.Contracts;
using ParkingManagement.SharedKernel.Enums;

namespace NotificationService.Infrastructure.Persistence.Seeding;

/// <summary>Mẫu thông báo đã có sẵn qua migration (HasData). Seeder chỉ thêm 2 thông báo demo.</summary>
public sealed class NotificationDataSeeder(ILogger<NotificationDataSeeder> logger) : IDataSeeder<NotificationDbContext>
{
    public async Task SeedAsync(NotificationDbContext db, CancellationToken cancellationToken)
    {
        await SeedCoreAsync(db, cancellationToken);
        await SeedExtrasAsync(db, cancellationToken);
    }

    /// <summary>Dữ liệu demo ban đầu (migration InitialCreate).</summary>
    private async Task SeedCoreAsync(NotificationDbContext db, CancellationToken cancellationToken)
    {
        if (await db.Notifications.AnyAsync(cancellationToken)) return;
        var templates = await db.NotificationTemplates.ToDictionaryAsync(t => (t.Key, t.Channel), cancellationToken);

        db.Notifications.AddRange(
            new Notification
            {
                UserId = DemoIds.Driver1User, TemplateId = templates[("BOOKING_CONFIRMED", NotificationChannel.InApp)].Id,
                TemplateKey = "BOOKING_CONFIRMED", Title = "Đặt chỗ thành công",
                Body = "Booking BK-0002 tại Bãi xe Vincom Đồng Khởi đã được xác nhận.",
                DataJson = JsonSerializer.Serialize(new { bookingId = DemoIds.BookingConfirmed }),
                Status = NotificationStatus.Sent, SentAtUtc = DateTime.UtcNow
            },
            new Notification
            {
                UserId = DemoIds.OwnerVincomUser, TemplateId = templates[("COMPLAINT_CREATED", NotificationChannel.InApp)].Id,
                TemplateKey = "COMPLAINT_CREATED", Title = "Có khiếu nại mới",
                Body = "Khiếu nại CP-0001 cho bãi Bãi xe Vincom Đồng Khởi. Vui lòng phản hồi trong 48 giờ.",
                DataJson = JsonSerializer.Serialize(new { complaintId = DemoIds.ComplaintOvercharge }),
                Status = NotificationStatus.Sent, SentAtUtc = DateTime.UtcNow
            });
        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Seed NotificationService xong: 2 thông báo.");
    }

    /// <summary>Dữ liệu demo cho các bảng thêm ở migration AddDocumentCoverage – mỗi bảng kiểm tra riêng nên chạy được trên DB cũ.</summary>
    private static async Task SeedExtrasAsync(NotificationDbContext db, CancellationToken cancellationToken)
    {
        if (!await db.NotificationPreferences.AnyAsync(cancellationToken))
        {
            db.NotificationPreferences.AddRange(
                // driver1 tắt email nhắc lịch, chỉ nhận in-app; không nhận push 23:00–06:00.
                new NotificationPreference { UserId = DemoIds.Driver1User, TemplateKey = "BOOKING_REMINDER", Channel = NotificationChannel.Email, IsEnabled = false },
                new NotificationPreference { UserId = DemoIds.Driver1User, TemplateKey = "BOOKING_REMINDER", Channel = NotificationChannel.Push, IsEnabled = true,
                                             QuietFrom = new TimeSpan(23, 0, 0), QuietTo = new TimeSpan(6, 0, 0) });
        }

        if (!await db.DeviceTokens.AnyAsync(cancellationToken))
        {
            db.DeviceTokens.AddRange(
                new DeviceToken { UserId = DemoIds.Driver1User, Token = "demo-fcm-token-driver1-android", Platform = "Android", DeviceName = "Samsung Galaxy A55", LastUsedAtUtc = DateTime.UtcNow },
                new DeviceToken { UserId = DemoIds.OwnerVincomUser, Token = "demo-fcm-token-owner-vincom-web", Platform = "Web", DeviceName = "Chrome – Owner portal", LastUsedAtUtc = DateTime.UtcNow });
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
