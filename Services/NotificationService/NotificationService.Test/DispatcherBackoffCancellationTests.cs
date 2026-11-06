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
/// T-602 – Backoff + CancellationToken + validate của NotificationDispatcher THẬT:
/// exponential backoff 5s * 1.5^n giữa các lần retry (đo bằng FakeTimeProvider),
/// CancellationToken hủy được giữa chừng (ném OperationCanceledException, không nuốt),
/// payload thiếu người nhận / Title-Body rỗng bị chặn TRƯỚC KHI gọi kênh.
/// </summary>
public class DispatcherBackoffCancellationTests
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

    private static SendNotificationRequest EmailRequest() => new(
        UserId: 5, Channel: NotificationChannel.Email, TemplateKey: "K", Title: "T", Body: "B");

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

    [Fact]
    public async Task Backoff_waits_5s_then_7_5s_between_attempts()
    {
        // Lần 1 lỗi ngay (không backoff trước lần đầu). Khoảng nghỉ sau lần 1 = 5 * 1.5^0 = 5s,
        // sau lần 2 = 5 * 1.5^1 = 7.5s → tổng 12.5s mới xong 3 lần thử.
        _emailSender.SendAsync(Arg.Any<Notification>(), Arg.Any<CancellationToken>())
            .Returns(_ => { _emailCalls++; return ChannelSendResult.TransientFailure("down"); });
        var dispatcher = CreateDispatcher();
        var task = dispatcher.SendAsync(EmailRequest());

        await WaitForCallCountAsync(1, task);

        // Chưa đủ 5s → KHÔNG có lần thử thứ 2 (backoff thật sự tồn tại, không retry ngay).
        _clock.Advance(TimeSpan.FromSeconds(4));
        await Task.Delay(5);
        Assert.Equal(1, _emailCalls);

        // Đủ mốc 5s → lần thử thứ 2 chạy.
        _clock.Advance(TimeSpan.FromSeconds(1));
        await WaitForCallCountAsync(2, task);

        // Khoảng nghỉ thứ 2 = 7.5s: advance 7s vẫn chưa đủ.
        _clock.Advance(TimeSpan.FromSeconds(7));
        await Task.Delay(5);
        Assert.Equal(2, _emailCalls);

        // Đủ 7.5s → lần thử thứ 3 (cuối cùng).
        _clock.Advance(TimeSpan.FromSeconds(1));
        await WaitForCallCountAsync(3, task);

        var response = await task;
        Assert.Equal(NotificationStatus.Failed, response.Status);
        Assert.Equal(3, _emailCalls);
    }

    [Fact]
    public async Task Cancellation_during_backoff_throws_operation_canceled_and_stops_retrying()
    {
        _emailSender.SendAsync(Arg.Any<Notification>(), Arg.Any<CancellationToken>())
            .Returns(_ => { _emailCalls++; return ChannelSendResult.TransientFailure("down"); });
        var dispatcher = CreateDispatcher();
        using var cts = new CancellationTokenSource();
        var task = dispatcher.SendAsync(EmailRequest(), cts.Token);

        await WaitForCallCountAsync(1, task);
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);
        Assert.Equal(1, _emailCalls);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public async Task Invalid_user_id_is_rejected_before_persisting_or_sending(int userId)
    {
        var dispatcher = CreateDispatcher();
        var request = new SendNotificationRequest(userId, NotificationChannel.Email, "K", "T", "B");

        await Assert.ThrowsAsync<ValidationException>(() => dispatcher.SendAsync(request));

        await _repository.DidNotReceiveWithAnyArgs().AddAsync(Arg.Any<Notification>(), Arg.Any<CancellationToken>());
        await _emailSender.DidNotReceiveWithAnyArgs().SendAsync(Arg.Any<Notification>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Empty_title_and_body_are_rejected_before_sending()
    {
        var dispatcher = CreateDispatcher();

        await Assert.ThrowsAsync<ValidationException>(() =>
            dispatcher.SendAsync(new SendNotificationRequest(5, NotificationChannel.Email, "K", " ", "")));

        await _repository.DidNotReceiveWithAnyArgs().AddAsync(Arg.Any<Notification>(), Arg.Any<CancellationToken>());
        await _emailSender.DidNotReceiveWithAnyArgs().SendAsync(Arg.Any<Notification>(), Arg.Any<CancellationToken>());
    }
}