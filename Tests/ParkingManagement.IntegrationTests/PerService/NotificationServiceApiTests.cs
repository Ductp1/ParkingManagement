using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NotificationService.API.Controllers;
using NotificationService.Infrastructure.Persistence;
using ParkingManagement.SharedKernel.Enums;

namespace ParkingManagement.IntegrationTests.PerService;

/// <summary>Collection: NotificationService chạy host thật trên database pm_it_notification_* riêng của từng lần chạy.</summary>
[CollectionDefinition("Notification API")]
public sealed class NotificationApiCollection : ICollectionFixture<ServiceHostFixture<NotificationsController, NotificationDbContext>>;

/// <summary>
/// Integration test NotificationService (TV6): hộp thư (UC-21), gửi thông báo qua dispatcher (S1-T602),
/// đánh dấu đã đọc, cài đặt người dùng (S4) và đăng ký/thu hồi device token FCM.
/// </summary>
[Collection("Notification API")]
public class NotificationServiceApiTests(ServiceHostFixture<NotificationsController, NotificationDbContext> fixture)
{
    [Fact] // Seed: driver 5 có thông báo BOOKING_CONFIRMED chưa đọc; mark-read làm UnreadCount giảm đúng 1.
    public async Task Seed_inbox_lists_and_marks_read()
    {
        var inbox = await fixture.Client.GetFromJsonAsync<InboxJson>("/api/v1/notifications?userId=5");
        var unreadBefore = inbox!.UnreadCount;

        // Thông báo seed của driver 5 đang ở trạng thái chưa đọc
        var seeded = inbox.Items.Items.Single(n => n.TemplateKey == "BOOKING_CONFIRMED" && n.Title == "Đặt chỗ thành công");
        Assert.False(seeded.IsRead);

        // Người dùng khác không đánh dấu được thông báo không thuộc về mình → 404
        await Asserts.NotFoundAsync(await fixture.Client.PutAsync($"/api/v1/notifications/{seeded.Id}/read?userId=6", null));

        var read = await fixture.Client.PutAsync($"/api/v1/notifications/{seeded.Id}/read?userId=5", null);
        Assert.Equal(HttpStatusCode.NoContent, read.StatusCode);

        var after = await fixture.Client.GetFromJsonAsync<InboxJson>("/api/v1/notifications?userId=5");
        Assert.Equal(unreadBefore - 1, after!.UnreadCount);
        Assert.True(after.Items.Items.Single(n => n.Id == seeded.Id).IsRead);
    }

    [Fact] // Dispatcher InApp: gửi → Sent (đã lưu DB + broadcast), xuất hiện ngay trong hộp thư chưa đọc.
    public async Task Send_in_app_notification_lands_in_the_inbox()
    {
        var send = await fixture.Client.PostAsJsonAsync("/api/v1/notifications/send", new
        {
            userId = 5,
            channel = "InApp",
            templateKey = "BOOKING_CONFIRMED",
            title = "IT – đặt chỗ giữ chỗ thành công",
            body = "Integration test tạo thông báo in-app.",
            dataJson = "{\"bookingId\":999}",
        });
        Assert.Equal(HttpStatusCode.OK, send.StatusCode);
        var sent = await send.Content.ReadFromJsonAsync<SendJson>();
        Assert.Equal("Sent", sent!.Status);
        Assert.True(sent.NotificationId > 0);

        var inbox = await fixture.Client.GetFromJsonAsync<InboxJson>("/api/v1/notifications?userId=5&unreadOnly=true");
        Assert.Contains(inbox!.Items.Items, n => n.Id == sent.NotificationId && n.Title.StartsWith("IT –"));

        // Kiểm chứng trực tiếp trong pm_it_notification: row đã lưu với trạng thái Sent.
        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
        var entity = await db.Notifications.FindAsync([sent.NotificationId]);
        Assert.NotNull(entity);
        Assert.Equal(NotificationStatus.Sent, entity!.Status);
    }

    [Fact] // S4: cài đặt mặc định (marketing TẮT) và cập nhật một phần – chỉ flag gửi rõ ràng mới đổi.
    public async Task Preferences_support_partial_update()
    {
        var before = await fixture.Client.GetFromJsonAsync<PreferencesJson>("/api/v1/notifications/preferences?userId=5");
        Assert.True(before!.PushEnabled);
        Assert.False(before.MarketingNotifications); // mặc định nền tảng: tắt marketing

        var update = await fixture.Client.PutAsJsonAsync("/api/v1/notifications/preferences?userId=5",
            new { emailEnabled = false });
        Assert.Equal(HttpStatusCode.NoContent, update.StatusCode);

        var after = await fixture.Client.GetFromJsonAsync<PreferencesJson>("/api/v1/notifications/preferences?userId=5");
        Assert.False(after!.EmailEnabled);
        Assert.True(after.PushEnabled);           // không gửi flag → giữ nguyên
        Assert.False(after.MarketingNotifications);

        // Bật marketing rồi kiểm tra persist
        await fixture.Client.PutAsJsonAsync("/api/v1/notifications/preferences?userId=5", new { marketingNotifications = true });
        var enabled = await fixture.Client.GetFromJsonAsync<PreferencesJson>("/api/v1/notifications/preferences?userId=5");
        Assert.True(enabled!.MarketingNotifications);
    }

    [Fact] // Device token: đăng ký idempotent (re-register không nhân đôi), thu hồi xong gọi lại → 404.
    public async Task Device_tokens_register_idempotently_and_revoke()
    {
        var register = await fixture.Client.PostAsJsonAsync("/api/v1/devices", new
        {
            userId = 9,
            token = "it-fcm-token-001",
            deviceType = "Android",
            deviceId = "Pixel-IT",
        });
        Assert.Equal(HttpStatusCode.Created, register.StatusCode);

        // Re-register (re-install app) → vẫn 201, KHÔNG tạo row trùng
        await fixture.Client.PostAsJsonAsync("/api/v1/devices", new
        {
            userId = 9,
            token = "it-fcm-token-001",
            deviceType = "Android",
            deviceId = "Pixel-IT-2",
        });

        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
            var rows = await db.DeviceTokens.Where(t => t.UserId == 9 && t.Token == "it-fcm-token-001").ToListAsync();
            var row = Assert.Single(rows);
            Assert.Equal("Pixel-IT-2", row.DeviceName); // được cập nhật bởi lần re-register
            Assert.False(row.IsRevoked);
        }

        Assert.Equal(HttpStatusCode.NoContent,
            (await fixture.Client.DeleteAsync("/api/v1/devices/it-fcm-token-001?userId=9")).StatusCode);

        // Thu hồi lần 2: idempotent (row đã revoked vẫn khớp điều kiện revoke) → vẫn 204, không lỗi
        Assert.Equal(HttpStatusCode.NoContent,
            (await fixture.Client.DeleteAsync("/api/v1/devices/it-fcm-token-001?userId=9")).StatusCode);

        await using var verifyScope = fixture.Services.CreateAsyncScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<NotificationDbContext>();
        var revoked = await verifyDb.DeviceTokens.SingleAsync(t => t.UserId == 9 && t.Token == "it-fcm-token-001");
        Assert.True(revoked.IsRevoked);
    }

    [Fact] // Validate phân trang hộp thư → 400 ProblemDetails.
    public async Task Inbox_validates_paging_input()
    {
        await Asserts.BadRequestAsync(await fixture.Client.GetAsync("/api/v1/notifications?userId=5&pageSize=200"),
            "page ≥ 1 và pageSize trong khoảng 1–100");
        await Asserts.BadRequestAsync(await fixture.Client.GetAsync("/api/v1/notifications?userId=5&page=0"),
            "page ≥ 1 và pageSize trong khoảng 1–100");
        await Asserts.BadRequestAsync(await fixture.Client.GetAsync("/api/v1/notifications?userId=0"),
            "userId phải là số nguyên dương");
    }

    public sealed record InboxJson(int UnreadCount, PageJson Items);

    public sealed record PageJson(IReadOnlyList<NotificationJson> Items, int Page, int PageSize, int TotalCount);

    public sealed record NotificationJson(int Id, string Channel, string TemplateKey, string Title, string Body,
        string? DataJson, string Status, bool IsRead, DateTime CreatedAtUtc);

    public sealed record SendJson(int NotificationId, string Status, string Message);

    public sealed record PreferencesJson(int UserId, bool EmailEnabled, bool SmsEnabled, bool PushEnabled,
        bool TransactionalNotifications, bool MarketingNotifications, bool UrgentNotifications);
}
