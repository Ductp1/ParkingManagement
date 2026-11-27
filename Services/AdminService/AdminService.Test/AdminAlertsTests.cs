using AdminService.Application.Features.Notifications;
using AdminService.Application.Features.RiskFlags;
using AdminService.Domain.Entities;
using AdminService.Domain.Enums;
using AdminService.Infrastructure.Integrations;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using ParkingManagement.SharedKernel.Enums;
using ParkingManagement.SharedKernel.Exceptions;

namespace AdminService.Test;

internal static class TestAdminAlerts
{
    public static AdminAlerts Create(FakeNotificationClient? notifications = null, params int[] adminIds)
        => new(notifications ?? new FakeNotificationClient(),
            adminIds.Length > 0 ? new AdminAlertSettings(adminIds) : AdminAlertSettings.Default,
            NullLogger<AdminAlerts>.Instance);
}

public class AdminAlertsTests
{
    private sealed class FlagStore : IRiskFlagStore
    {
        private RiskFlag? flag;
        public Task<RiskFlag?> FindPendingAsync(RiskSubjectType subjectType, int subjectId, string ruleCode,
            CancellationToken cancellationToken = default) => Task.FromResult(flag);
        public Task AddAsync(RiskFlag added, CancellationToken cancellationToken = default)
        {
            flag = added;
            return Task.CompletedTask;
        }
        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private static StaffCancellationAlertRequest Cancels(int count)
        => new(7, 1, count, new DateTime(2026, 11, 27, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 11, 27, 1, 0, 0, DateTimeKind.Utc), WarningThreshold: 3, CriticalThreshold: 5);

    private sealed class CaptureHandler : HttpMessageHandler
    {
        public string? Authorization { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Authorization = request.Headers.Authorization?.ToString();
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK));
        }
    }

    [Theory] // Khóa nội bộ của GateService không được đi theo thông báo sang service khác; JWT người dùng vẫn chuyển tiếp.
    [InlineData("/internal/v1/risk-flags/staff-abnormal-cancel", null)]
    [InlineData("/api/v1/partner-applications", "Bearer caller")]
    public async Task Caller_authorization_is_not_forwarded_from_internal_requests(string path, string? forwarded)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        context.Request.Headers.Authorization = "Bearer caller";
        var sink = new CaptureHandler();
        using var client = new HttpClient(new BearerTokenForwardingHandler(new HttpContextAccessor { HttpContext = context })
        {
            InnerHandler = sink,
        });

        await client.GetAsync("http://gateway/api/v1/notifications");

        Assert.Equal(forwarded, sink.Authorization);
    }

    [Fact]
    public async Task Alert_goes_to_every_admin_with_the_screen_to_open()
    {
        var notifications = new FakeNotificationClient();

        await TestAdminAlerts.Create(notifications, 1, 9).RaiseAsync("KEY", "Tiêu đề", "Nội dung", "/admin/overview/dashboard");

        Assert.Equal([1, 9], notifications.Sent.Select(n => n.UserId));
        Assert.All(notifications.Sent, n => Assert.Equal("""{"adminPath":"/admin/overview/dashboard"}""", n.DataJson));
    }

    [Fact]
    public async Task Notification_failure_never_reaches_the_caller()
    {
        var notifications = new FakeNotificationClient { Failure = new DependencyUnavailableException("down") };

        var error = await Record.ExceptionAsync(
            () => TestAdminAlerts.Create(notifications).RaiseAsync("KEY", "T", "B", "/admin/overview/dashboard"));

        Assert.Null(error);
    }

    [Fact]
    public async Task Risk_flag_notifies_when_created_and_when_escalated_only()
    {
        var notifications = new FakeNotificationClient();
        var useCase = new RaiseStaffCancellationAlertUseCase(new FlagStore(), TimeProvider.System,
            TestAdminAlerts.Create(notifications));

        await useCase.ExecuteAsync("GateService", Cancels(3));   // tạo mới (Warning)
        await useCase.ExecuteAsync("GateService", Cancels(4));   // cập nhật bằng chứng
        await useCase.ExecuteAsync("GateService", Cancels(5));   // nâng lên Critical

        Assert.Equal(2, notifications.Sent.Count);
        Assert.All(notifications.Sent, n => Assert.Equal("RISK_FLAG_RAISED", n.TemplateKey));
        Assert.Contains("Critical", notifications.Sent[1].Title);
    }
}
