using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NotificationService.Application.Features.Dispatcher;
using NotificationService.Domain.Constants;
using NotificationService.Domain.Entities;
using NotificationService.Infrastructure.Persistence.Repositories;
using NotificationService.Infrastructure.Services.Dispatcher;
using NSubstitute;
using ParkingManagement.SharedKernel.Enums;
using ParkingManagement.SharedKernel.Exceptions;

namespace NotificationService.Test;

/// <summary>
/// Unit Tests cho NotificationDispatcher THẬT (Infrastructure, S1-T602) – bảo vệ 2 path
/// theo feedback PR: lỗi TẠM THỜI => giữ Pending + tăng RetryCount rồi retry;
/// lỗi VĨNH VIỄN => Failed ngay. Repository + 4 channel sender được NSubstitute thay thế
/// → KHÔNG cần database. Backoff Task.Delay chạy theo TimeProvider nên FakeTimeProvider
/// advance thời gian giả, mỗi test chạy < 1s (không chờ 5s/7.5s thật).
/// </summary>
public class NotificationDispatcherTests
{
    private const int NotificationId = 42;

    private readonly INotificationRepository _repository = Substitute.For<INotificationRepository>();
    private readonly IEmailNotificationSender _emailSender = Substitute.For<IEmailNotificationSender>();
    private readonly ISmsNotificationSender _smsSender = Substitute.For<ISmsNotificationSender>();
    private readonly IFcmNotificationSender _fcmSender = Substitute.For<IFcmNotificationSender>();
    private readonly IInAppNotificationSender _inAppSender = Substitute.For<IInAppNotificationSender>();
    private readonly FakeTimeProvider _clock = new();

    private NotificationDispatcher CreateDispatcher() => new(
        _repository, NullLogger<NotificationDispatcher>.Instance, _clock,
        _emailSender, _smsSender, _fcmSender, _inAppSender);

    private static SendNotificationRequest EmailRequest() => new(
        UserId: 5,
        Channel: NotificationChannel.Email,
        TemplateKey: "BOOKING_CONFIRMED",
        Title: "Đặt chỗ thành công",
        Body: "Booking BK-0002 đã được xác nhận.");

    /// <summary>Advance đồng hồ giả cho tới khi task của dispatcher hoàn tất (nhảy qua backoff).</summary>
    private static async Task<T> AdvanceUntilCompleteAsync<T>(FakeTimeProvider clock, Task<T> task)
    {
        for (var tick = 0; tick < 500 && !task.IsCompleted; tick++)
        {
            clock.Advance(TimeSpan.FromMilliseconds(100));
            await Task.Delay(1); // nhường thread-pool thật chạy tiếp vòng retry của dispatcher
        }

        if (!task.IsCompleted)
            throw new TimeoutException($"Dispatcher chưa hoàn tất sau khi advance thời gian giả (Status={task.Status}).");
        return await task;
    }

    // ===== SendAsync – path THÀNH CÔNG =====

    [Fact]
    public async Task SendAsync_success_on_first_attempt_marks_sent()
    {
        // Arrange
        _emailSender.SendAsync(Arg.Any<Notification>(), Arg.Any<CancellationToken>())
            .Returns(ChannelSendResult.Ok());
        var dispatcher = CreateDispatcher();

        // Act
        var response = await dispatcher.SendAsync(EmailRequest());

        // Assert: Sent + message "OK", gửi đúng 1 lần, DB add + update đúng 1 lần
        Assert.Equal(NotificationStatus.Sent, response.Status);
        Assert.Equal("OK", response.Message);
        await _emailSender.Received(1).SendAsync(Arg.Any<Notification>(), Arg.Any<CancellationToken>());
        await _repository.Received(1).AddAsync(Arg.Any<Notification>(), Arg.Any<CancellationToken>());
        await _repository.Received(1).UpdateAsync(Arg.Any<Notification>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SendAsync_success_sets_sent_at_utc_on_row()
    {
        // Arrange
        Notification? sentRow = null;
        _emailSender.SendAsync(Arg.Do<Notification>(n => sentRow = n), Arg.Any<CancellationToken>())
            .Returns(ChannelSendResult.Ok());
        var dispatcher = CreateDispatcher();

        // Act
        await dispatcher.SendAsync(EmailRequest());

        // Assert: row được đánh dấu Sent + timestamp gửi
        Assert.NotNull(sentRow);
        Assert.Equal(NotificationStatus.Sent, sentRow!.Status);
        Assert.NotNull(sentRow.SentAtUtc);
        Assert.Equal(1, sentRow.RetryCount);
    }

    // ===== SendAsync – path VĨNH VIỄN: Failed ngay, KHÔNG retry =====

    [Fact]
    public async Task SendAsync_permanent_failure_marks_failed_immediately_without_retry()
    {
        // Arrange
        _emailSender.SendAsync(Arg.Any<Notification>(), Arg.Any<CancellationToken>())
            .Returns(ChannelSendResult.PermanentFailure("SMTP sai cấu hình"));
        var dispatcher = CreateDispatcher();

        // Act
        var response = await dispatcher.SendAsync(EmailRequest());

        // Assert (feedback PR): permanent → Failed NGAY, gửi đúng 1 lần, không retry vô ích
        Assert.Equal(NotificationStatus.Failed, response.Status);
        Assert.Contains("SMTP sai cấu hình", response.Message);
        await _emailSender.Received(1).SendAsync(Arg.Any<Notification>(), Arg.Any<CancellationToken>());
    }

    // ===== SendAsync – path TẠM THỜI: retry với backoff rồi mới chốt kết quả =====

    [Fact]
    public async Task SendAsync_transient_failure_exhausts_retries_marks_failed()
    {
        // Arrange: sender luôn lỗi tạm thời (VD mất mạng); bắt row được lưu lúc AddAsync
        // (Arg.Do phải đăng ký TRƯỚC khi dispatcher chạy thì callback mới fire)
        Notification? row = null;
        _repository.AddAsync(Arg.Do<Notification>(n => row = n), Arg.Any<CancellationToken>())
            .Returns(callInfo => callInfo.Arg<Notification>());
        _emailSender.SendAsync(Arg.Any<Notification>(), Arg.Any<CancellationToken>())
            .Returns(ChannelSendResult.TransientFailure("mạng chập chờn"));
        var dispatcher = CreateDispatcher();

        // Act: chạy dispatcher trên thread-pool (tách khỏi sync context đơn luồng của xUnit),
        // rồi advance đồng hồ giả để chạy qua các backoff 5s/7.5s
        var response = await AdvanceUntilCompleteAsync(
            _clock, Task.Run(() => dispatcher.SendAsync(EmailRequest())));

        // Assert (feedback PR): thử đủ MaxRetryAttempts (3) lần rồi MỚI chốt Failed,
        // không giữ Pending "treo" gây sai monitoring
        await _emailSender.Received(NotificationConstants.MaxRetryAttempts)
            .SendAsync(Arg.Any<Notification>(), Arg.Any<CancellationToken>());
        await _repository.Received(1).AddAsync(Arg.Any<Notification>(), Arg.Any<CancellationToken>());

        Assert.Equal(NotificationStatus.Failed, row!.Status);
        Assert.Equal(NotificationConstants.MaxRetryAttempts, row.RetryCount);
        Assert.Equal(NotificationStatus.Failed, response.Status);
    }

    [Fact]
    public async Task SendAsync_transient_failure_then_success_retries_until_sent()
    {
        // Arrange: fail 2 lần đầu, lần thứ 3 thành công
        _emailSender.SendAsync(Arg.Any<Notification>(), Arg.Any<CancellationToken>())
            .Returns(
                ChannelSendResult.TransientFailure("lần 1 fail"),
                ChannelSendResult.TransientFailure("lần 2 fail"),
                ChannelSendResult.Ok());
        var dispatcher = CreateDispatcher();

        // Act: chạy trên thread-pool + advance đồng hồ giả qua các backoff
        var response = await AdvanceUntilCompleteAsync(
            _clock, Task.Run(() => dispatcher.SendAsync(EmailRequest())));

        // Assert: Sent sau 3 lần thử; mỗi lần thử đều persist tiến độ (sống sót qua restart)
        Assert.Equal(NotificationStatus.Sent, response.Status);
        await _emailSender.Received(3).SendAsync(Arg.Any<Notification>(), Arg.Any<CancellationToken>());
        await _repository.Received(3).UpdateAsync(Arg.Any<Notification>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SendAsync_unsupported_channel_fails_without_calling_any_sender()
    {
        // Arrange: channel ngoài 4 kênh đã đăng ký (InApp/Email/Sms/Push)
        var dispatcher = CreateDispatcher();

        // Act
        var response = await dispatcher.SendAsync(EmailRequest() with { Channel = (NotificationChannel)99 });

        // Assert: Failed ngay, không sender nào bị gọi
        Assert.Equal(NotificationStatus.Failed, response.Status);
        Assert.Contains("không được hỗ trợ", response.Message);
        await _emailSender.DidNotReceiveWithAnyArgs().SendAsync(Arg.Any<Notification>(), Arg.Any<CancellationToken>());
        await _smsSender.DidNotReceiveWithAnyArgs().SendAsync(Arg.Any<Notification>(), Arg.Any<CancellationToken>());
    }

    // ===== SendAsync – validation ở tầng dispatcher =====

    [Fact]
    public async Task SendAsync_zero_user_id_throws_validation_exception()
    {
        var dispatcher = CreateDispatcher();

        await Assert.ThrowsAsync<ValidationException>(
            () => dispatcher.SendAsync(EmailRequest() with { UserId = 0 }));
    }

    [Fact]
    public async Task SendAsync_blank_title_throws_validation_exception()
    {
        var dispatcher = CreateDispatcher();

        await Assert.ThrowsAsync<ValidationException>(
            () => dispatcher.SendAsync(EmailRequest() with { Title = "   " }));
    }

    // ===== RetryAsync – do background job / trigger tay gọi =====

    [Fact]
    public async Task RetryAsync_notification_not_found_returns_failed_without_sending()
    {
        // Arrange: id không tồn tại trong DB
        _repository.GetByIdAsync(99, Arg.Any<CancellationToken>()).Returns((Notification?)null);
        var dispatcher = CreateDispatcher();

        // Act
        var response = await dispatcher.RetryAsync(99);

        // Assert: trả Failed kèm message, KHÔNG gọi sender nào
        Assert.Equal(NotificationStatus.Failed, response.Status);
        Assert.Equal("Notification không tìm thấy", response.Message);
        await _emailSender.DidNotReceiveWithAnyArgs().SendAsync(Arg.Any<Notification>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RetryAsync_already_sent_is_idempotent_and_never_resends()
    {
        // Arrange: notification đã Sent (idempotent – tránh duplicate cho user)
        _repository.GetByIdAsync(NotificationId, Arg.Any<CancellationToken>())
            .Returns(new Notification { Status = NotificationStatus.Sent, Channel = NotificationChannel.Email });
        var dispatcher = CreateDispatcher();

        // Act
        var response = await dispatcher.RetryAsync(NotificationId);

        // Assert
        Assert.Equal(NotificationStatus.Sent, response.Status);
        await _emailSender.DidNotReceiveWithAnyArgs().SendAsync(Arg.Any<Notification>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RetryAsync_exhausted_budget_marks_failed_without_sending()
    {
        // Arrange: đã dùng hết 3 lượt thử mà vẫn Pending trong DB
        _repository.GetByIdAsync(NotificationId, Arg.Any<CancellationToken>())
            .Returns(new Notification
            {
                Status = NotificationStatus.Pending,
                RetryCount = NotificationConstants.MaxRetryAttempts,
                Channel = NotificationChannel.Email
            });
        var dispatcher = CreateDispatcher();

        // Act
        var response = await dispatcher.RetryAsync(NotificationId);

        // Assert: chốt Failed, không gửi thêm
        Assert.Equal(NotificationStatus.Failed, response.Status);
        Assert.Contains("Vượt quá 3 lần thử", response.Message);
        await _emailSender.DidNotReceiveWithAnyArgs().SendAsync(Arg.Any<Notification>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RetryAsync_transient_with_remaining_budget_stays_pending()
    {
        // Arrange: RetryCount=1 (< 3) → còn lượt thử; sender lỗi tạm thời
        var row = new Notification
        {
            Status = NotificationStatus.Pending,
            RetryCount = 1,
            Channel = NotificationChannel.Email
        };
        _repository.GetByIdAsync(NotificationId, Arg.Any<CancellationToken>()).Returns(row);
        _emailSender.SendAsync(row, Arg.Any<CancellationToken>())
            .Returns(ChannelSendResult.TransientFailure("SMTP timeout"));
        var dispatcher = CreateDispatcher();

        // Act
        var response = await dispatcher.RetryAsync(NotificationId);

        // Assert (feedback PR): CHỈ transient + còn ngân sách mới giữ Pending (1 → 2 < 3)
        Assert.Equal(NotificationStatus.Pending, response.Status);
        Assert.Equal(2, row.RetryCount);
        Assert.Equal(NotificationStatus.Pending, row.Status);
        await _repository.Received(1).UpdateAsync(row, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RetryAsync_permanent_failure_marks_failed_immediately()
    {
        // Arrange: lỗi vĩnh viễn (VD sai cấu hình SMTP)
        var row = new Notification { Status = NotificationStatus.Pending, RetryCount = 0, Channel = NotificationChannel.Email };
        _repository.GetByIdAsync(NotificationId, Arg.Any<CancellationToken>()).Returns(row);
        _emailSender.SendAsync(row, Arg.Any<CancellationToken>())
            .Returns(ChannelSendResult.PermanentFailure("sai cấu hình"));
        var dispatcher = CreateDispatcher();

        // Act
        var response = await dispatcher.RetryAsync(NotificationId);

        // Assert (feedback PR): permanent → Failed ngay cả khi còn ngân sách retry
        Assert.Equal(NotificationStatus.Failed, response.Status);
        Assert.Equal(NotificationStatus.Failed, row.Status);
        await _repository.Received(1).UpdateAsync(row, Arg.Any<CancellationToken>());
    }
}
