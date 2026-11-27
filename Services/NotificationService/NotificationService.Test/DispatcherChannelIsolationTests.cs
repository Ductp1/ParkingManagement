using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NotificationService.Application.Features.Dispatcher;
using NotificationService.Domain.Entities;
using NotificationService.Infrastructure.Persistence.Repositories;
using NotificationService.Infrastructure.Services.Dispatcher;
using NSubstitute;
using ParkingManagement.SharedKernel.Enums;

namespace NotificationService.Test;

/// <summary>
/// T-602 – cô lập lỗi giữa các kênh + validate payload của NotificationDispatcher THẬT:
/// một kênh lỗi không làm mất kết quả của kênh khác, payload rỗng bị chặn trước khi gọi kênh.
/// </summary>
public class DispatcherChannelIsolationTests
{
    private readonly INotificationRepository _repository = Substitute.For<INotificationRepository>();
    private readonly IEmailNotificationSender _emailSender = Substitute.For<IEmailNotificationSender>();
    private readonly ISmsNotificationSender _smsSender = Substitute.For<ISmsNotificationSender>();
    private readonly IFcmNotificationSender _fcmSender = Substitute.For<IFcmNotificationSender>();
    private readonly IInAppNotificationSender _inAppSender = Substitute.For<IInAppNotificationSender>();
    private readonly FakeTimeProvider _clock = new();

    private NotificationDispatcher CreateDispatcher() => new(
        _repository, NullLogger<NotificationDispatcher>.Instance, _clock,
        _emailSender, _smsSender, _fcmSender, _inAppSender);

    [Fact]
    public async Task One_permanent_failure_does_not_block_other_channels()
    {
        _emailSender.SendAsync(Arg.Any<Notification>(), Arg.Any<CancellationToken>())
            .Returns(ChannelSendResult.PermanentFailure("SMTP cấu hình sai"));
        _smsSender.SendAsync(Arg.Any<Notification>(), Arg.Any<CancellationToken>()).Returns(ChannelSendResult.Ok());
        _fcmSender.SendAsync(Arg.Any<Notification>(), Arg.Any<CancellationToken>()).Returns(ChannelSendResult.Ok());
        _inAppSender.SendAsync(Arg.Any<Notification>(), Arg.Any<CancellationToken>()).Returns(ChannelSendResult.Ok());
        var dispatcher = CreateDispatcher();

        var results = await dispatcher.SendMultiChannelAsync(5, "K", "T", "B",
            [NotificationChannel.Email, NotificationChannel.Sms, NotificationChannel.Push, NotificationChannel.InApp]);

        Assert.Equal(NotificationStatus.Failed, results[NotificationChannel.Email].Status);
        Assert.Equal(NotificationStatus.Sent, results[NotificationChannel.Sms].Status);
        Assert.Equal(NotificationStatus.Sent, results[NotificationChannel.Push].Status);
        Assert.Equal(NotificationStatus.Sent, results[NotificationChannel.InApp].Status);
    }

    [Fact]
    public async Task Transient_failure_exhausting_budget_reports_failed_but_other_channel_still_sent()
    {
        // Email lỗi thoáng qua hết 3 lần thử (3 = TỔNG CỘNG gồm lần đầu) → Failed;
        // InApp không bị ảnh hưởng. Backoff chạy theo FakeTimeProvider → phải advance đồng hồ
        // cho tới khi dispatcher hoàn tất (như helper AdvanceUntilCompleteAsync của repo).
        _emailSender.SendAsync(Arg.Any<Notification>(), Arg.Any<CancellationToken>())
            .Returns(ChannelSendResult.TransientFailure("SMTP timeout"));
        _inAppSender.SendAsync(Arg.Any<Notification>(), Arg.Any<CancellationToken>()).Returns(ChannelSendResult.Ok());
        var dispatcher = CreateDispatcher();

        var task = dispatcher.SendMultiChannelAsync(5, "K", "T", "B",
            [NotificationChannel.Email, NotificationChannel.InApp]);
        for (var tick = 0; tick < 300 && !task.IsCompleted; tick++)
        {
            _clock.Advance(TimeSpan.FromMilliseconds(100));
            await Task.Delay(2);
        }
        var results = await task;

        Assert.Equal(NotificationStatus.Failed, results[NotificationChannel.Email].Status);
        Assert.Equal(NotificationStatus.Sent, results[NotificationChannel.InApp].Status);
        await _emailSender.Received(3).SendAsync(Arg.Any<Notification>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Empty_title_fails_all_channels_without_calling_any_sender()
    {
        // Payload rỗng (Title rỗng) → validate chặn, KHÔNG kênh nào được gọi, không throw ra ngoài.
        var dispatcher = CreateDispatcher();

        var results = await dispatcher.SendMultiChannelAsync(5, "K", "", "B",
            [NotificationChannel.Email, NotificationChannel.InApp]);

        Assert.All(results.Values, r => Assert.Equal(NotificationStatus.Failed, r.Status));
        await _emailSender.DidNotReceiveWithAnyArgs().SendAsync(Arg.Any<Notification>(), Arg.Any<CancellationToken>());
        await _inAppSender.DidNotReceiveWithAnyArgs().SendAsync(Arg.Any<Notification>(), Arg.Any<CancellationToken>());
    }
}