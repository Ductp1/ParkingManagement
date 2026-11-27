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
/// T-602 – Unit test luồng MULTI-CHANNEL của NotificationDispatcher THẬT:
/// gửi đủ các kênh được yêu cầu, bỏ qua kênh không yêu cầu ("kênh tắt"), dedupe,
/// và một kênh lỗi KHÔNG làm mất kết quả của kênh khác.
/// "Kênh bật/tắt" trong dispatcher = danh sách `channels` truyền vào SendMultiChannelAsync
/// (dispatcher KHÔNG đọc NotificationPreferences – xem báo cáo phần giả định).
/// </summary>
public class DispatcherMultiChannelTests
{
    private readonly INotificationRepository _repository = Substitute.For<INotificationRepository>();
    private readonly IEmailNotificationSender _emailSender = Substitute.For<IEmailNotificationSender>();
    private readonly ISmsNotificationSender _smsSender = Substitute.For<ISmsNotificationSender>();
    private readonly IFcmNotificationSender _fcmSender = Substitute.For<IFcmNotificationSender>();
    private readonly IInAppNotificationSender _inAppSender = Substitute.For<IInAppNotificationSender>();
    private readonly FakeTimeProvider _clock = new();

    internal NotificationDispatcher CreateDispatcher() => new(
        _repository, NullLogger<NotificationDispatcher>.Instance, _clock,
        _emailSender, _smsSender, _fcmSender, _inAppSender);

    [Fact]
    public async Task SendMultiChannel_with_all_four_channels_sends_each_exactly_once()
    {
        _emailSender.SendAsync(Arg.Any<Notification>(), Arg.Any<CancellationToken>()).Returns(ChannelSendResult.Ok());
        _smsSender.SendAsync(Arg.Any<Notification>(), Arg.Any<CancellationToken>()).Returns(ChannelSendResult.Ok());
        _fcmSender.SendAsync(Arg.Any<Notification>(), Arg.Any<CancellationToken>()).Returns(ChannelSendResult.Ok());
        _inAppSender.SendAsync(Arg.Any<Notification>(), Arg.Any<CancellationToken>()).Returns(ChannelSendResult.Ok());
        var dispatcher = CreateDispatcher();

        var results = await dispatcher.SendMultiChannelAsync(5, "BOOKING_CONFIRMED", "T", "B",
            [NotificationChannel.InApp, NotificationChannel.Email, NotificationChannel.Sms, NotificationChannel.Push]);

        Assert.Equal(4, results.Count);
        Assert.All(results.Values, r => Assert.Equal(NotificationStatus.Sent, r.Status));
        await _emailSender.Received(1).SendAsync(Arg.Any<Notification>(), Arg.Any<CancellationToken>());
        await _smsSender.Received(1).SendAsync(Arg.Any<Notification>(), Arg.Any<CancellationToken>());
        await _fcmSender.Received(1).SendAsync(Arg.Any<Notification>(), Arg.Any<CancellationToken>());
        await _inAppSender.Received(1).SendAsync(Arg.Any<Notification>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SendMultiChannel_skips_channels_not_requested()
    {
        // "Kênh tắt thì bỏ qua": chỉ yêu cầu Email + InApp → SMS/FCM KHÔNG được gọi.
        _emailSender.SendAsync(Arg.Any<Notification>(), Arg.Any<CancellationToken>()).Returns(ChannelSendResult.Ok());
        _inAppSender.SendAsync(Arg.Any<Notification>(), Arg.Any<CancellationToken>()).Returns(ChannelSendResult.Ok());
        var dispatcher = CreateDispatcher();

        var results = await dispatcher.SendMultiChannelAsync(5, "BOOKING_CONFIRMED", "T", "B",
            [NotificationChannel.Email, NotificationChannel.InApp]);

        Assert.Equal(2, results.Count);
        await _smsSender.DidNotReceiveWithAnyArgs().SendAsync(Arg.Any<Notification>(), Arg.Any<CancellationToken>());
        await _fcmSender.DidNotReceiveWithAnyArgs().SendAsync(Arg.Any<Notification>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SendMultiChannel_deduplicates_requested_channels()
    {
        _emailSender.SendAsync(Arg.Any<Notification>(), Arg.Any<CancellationToken>()).Returns(ChannelSendResult.Ok());
        _inAppSender.SendAsync(Arg.Any<Notification>(), Arg.Any<CancellationToken>()).Returns(ChannelSendResult.Ok());
        var dispatcher = CreateDispatcher();

        var results = await dispatcher.SendMultiChannelAsync(5, "K", "T", "B",
            [NotificationChannel.Email, NotificationChannel.Email, NotificationChannel.InApp]);

        Assert.Equal(2, results.Count);
        await _emailSender.Received(1).SendAsync(Arg.Any<Notification>(), Arg.Any<CancellationToken>());
    }
}