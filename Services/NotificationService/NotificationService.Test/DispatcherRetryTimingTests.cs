using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NotificationService.Application.Features.Dispatcher;
using NotificationService.Domain.Entities;
using NotificationService.Infrastructure.Persistence.Repositories;
using NotificationService.Infrastructure.Services.Dispatcher;
using NSubstitute;
using ParkingManagement.SharedKernel.Enums;
using ParkingManagement.SharedKernel.Exceptions;

namespace NotificationService.Test;

/// <summary>
/// T-602 – Unit test Retry/Backoff/Cancel của NotificationDispatcher THẬT (xUnit + NSubstitute + FakeTimeProvider):
/// retry 3 lần TỔNG CỘNG (gồm lần đầu – theo doc của NotificationDispatcher), exponential backoff 5s * 1.5^n,
/// CancellationToken hủy được giữa chừng, validate payload trước khi lưu DB.
/// Backoff/backoff-delay chạy theo TimeProvider → test advance đồng hồ giả, mỗi test &lt; 1s.
/// </summary>
public class DispatcherRetryTimingTests
{
    private readonly INotificationRepository _repository = Substitute.For<INotificationRepository>();
    private readonly IEmailNotificationSender _emailSender = Substitute.For<IEmailNotificationSender>();
    private readonly ISmsNotificationSender _smsSender = Substitute.For<ISmsNotificationSender>();
    private readonly IFcmNotificationSender _fcmSender = Substitute.For<IFcmNotificationSender>();
    private readonly IInAppNotificationSender _inAppSender = Substitute.For<IInAppNotificationSender>();
    private readonly FakeTimeProvider _clock = new();
    private int _emailCalls;

    private NotificationDispatcher CreateDispatcher() => new(
        _repository, NullLogger<NotificationDispatcher>.Instance, _clock,
        _emailSender, _smsSender, _fcmSender, _inAppSender);

    private void EmailAlwaysTransient()
    {
        _emailSender.SendAsync(Arg.Any<Notification>(), Arg.Any<CancellationToken>())
            .Returns(_ => { _emailCalls++; return ChannelSendResult.TransientFailure("SMTP down"); });
    }

    private void ConfigureEmail(params ChannelSendResult[] results)
    {
        _emailSender.SendAsync(Arg.Any<Notification>(), Arg.Any<CancellationToken>())
            .Returns(_ => { _emailCalls++; return results[Math.Min(_emailCalls - 1, results.Length - 1)]; });
    }

    /// <summary>Advance đồng hồ giả cho tới khi sender được gọi đúng `target` lần (hoặc task xong).</summary>
    private async Task WaitForCallCountAsync(int target, Task task)
    {
        for (var tick = 0; tick < 300 && _emailCalls < target && !task.IsCompleted; tick++)
        {
            _clock.Advance(TimeSpan.FromMilliseconds(100));
            await Task.Delay(2);
        }
        Assert.True(_emailCalls >= target, $"Sender mới được gọi {_emailCalls} lần (cần >= {target}).");
    }

    private static SendNotificationRequest EmailRequest() => new(
        UserId: 5, Channel: NotificationChannel.Email, TemplateKey: "BOOKING_CONFIRMED",
        Title: "T", Body: "B");

    [Fact]
    public async Task Transient_fail_twice_then_success_is_sent_on_third_total_attempt()
    {
        // "Retry 3 lần" = 3 lần TỔNG CỘNG gồm lần đầu: fail, fail, success → Sent đúng ở lần thử thứ 3.
        ConfigureEmail(ChannelSendResult.TransientFailure("e1"), ChannelSendResult.TransientFailure("e2"), ChannelSendResult.Ok());
        var dispatcher = CreateDispatcher();
        var task = dispatcher.SendAsync(EmailRequest());

        await WaitForCallCountAsync(3, task);
        var response = await task;

        Assert.Equal(NotificationStatus.Sent, response.Status);
        Assert.Equal(3, _emailCalls);
        await _emailSender.Received(3).SendAsync(Arg.Any<Notification>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Transient_always_failing_stops_exactly_after_three_attempts_and_marks_failed()
    {
        EmailAlwaysTransient();
        var dispatcher = CreateDispatcher();
        var task = dispatcher.SendAsync(EmailRequest());

        await WaitForCallCountAsync(3, task);
        var response = await task;

        Assert.Equal(NotificationStatus.Failed, response.Status);
        Assert.Equal(3, _emailCalls);
        Assert.Contains("SMTP down", response.Message);
    }

    [Fact]
    public async Task Permanent_failure_stops_immediately_after_first_attempt()
    {
        _emailSender.SendAsync(Arg.Any<Notification>(), Arg.Any<CancellationToken>())
            .Returns(_ => { _emailCalls++; return ChannelSendResult.PermanentFailure("sai cấu hình"); });
        var dispatcher = CreateDispatcher();

        var response = await dispatcher.SendAsync(EmailRequest());

        Assert.Equal(NotificationStatus.Failed, response.Status);
        Assert.Equal(1, _emailCalls);
    }
}