using NotificationService.Application.Features.Dispatcher;
using NSubstitute;
using ParkingManagement.SharedKernel.Enums;
using ParkingManagement.SharedKernel.Exceptions;

namespace NotificationService.Test;

/// <summary>
/// Unit Tests cho Dispatcher use cases (S1-T602) – theo Test Plan v3 (TESTING_GUIDE.md).
/// SendNotificationUseCase / RetryNotificationUseCase là Application layer: chúng ủy thác cho
/// port INotificationDispatcher. Implementation thật (NotificationDispatcher ở Infrastructure,
/// route qua Email/Sms/Fcm/InApp senders, retry tối đa MaxRetryAttempts với exponential backoff)
/// được thay bằng NSubstitute substitute – unit test chỉ verify hợp đồng ủy thác + lan truyền lỗi.
/// </summary>
public class DispatcherTests
{
    private readonly INotificationDispatcher _dispatcher = Substitute.For<INotificationDispatcher>();

    /// <summary>Request hợp lệ mẫu (Email channel như kịch bản gửi mail xác nhận booking).</summary>
    private static SendNotificationRequest ValidRequest() => new(
        UserId: 5,
        Channel: NotificationChannel.Email,
        TemplateKey: "BOOKING_CONFIRMED",
        Title: "Đặt chỗ thành công",
        Body: "Bạn có booking BK-0002 tại bãi Bách Khoa."
    );

    [Fact]
    public async Task SendAsync_valid_request_returns_dispatcher_response()
    {
        // Arrange: Dispatcher gửi thành công → Sent
        var expected = new SendNotificationResponse(42, NotificationStatus.Sent, "Đã gửi qua Email");
        _dispatcher.SendAsync(Arg.Any<SendNotificationRequest>(), Arg.Any<CancellationToken>())
            .Returns(expected);
        var useCase = new SendNotificationUseCase(_dispatcher);

        // Act
        var response = await useCase.ExecuteAsync(ValidRequest());

        // Assert: response của dispatcher được trả về nguyên vẹn
        Assert.Equal(42, response.NotificationId);
        Assert.Equal(NotificationStatus.Sent, response.Status);
        Assert.Equal("Đã gửi qua Email", response.Message);
    }

    [Fact]
    public async Task SendAsync_passes_request_and_cancellation_token_to_dispatcher()
    {
        // Arrange
        var useCase = new SendNotificationUseCase(_dispatcher);
        var request = ValidRequest();
        using var cts = new CancellationTokenSource();

        // Act
        await useCase.ExecuteAsync(request, cts.Token);

        // Assert: request + token được truyền đúng, gọi đúng 1 lần
        await _dispatcher.Received(1).SendAsync(request, cts.Token);
    }

    [Fact]
    public async Task SendAsync_preserves_pending_status_from_dispatcher()
    {
        // Arrange: Dispatcher lưu row TRƯỚC khi gửi, lỗi tạm thời → row ở trạng thái Pending chờ retry job
        var expected = new SendNotificationResponse(7, NotificationStatus.Pending, "Lỗi tạm thời, chờ retry");
        _dispatcher.SendAsync(Arg.Any<SendNotificationRequest>(), Arg.Any<CancellationToken>())
            .Returns(expected);
        var useCase = new SendNotificationUseCase(_dispatcher);

        // Act
        var response = await useCase.ExecuteAsync(ValidRequest());

        // Assert
        Assert.Equal(NotificationStatus.Pending, response.Status);
    }

    [Fact]
    public async Task SendAsync_dispatcher_validation_failure_propagates()
    {
        // Arrange: Dispatcher (Infrastructure) validate UserId/Title/Body và ném ValidationException
        _dispatcher.SendAsync(Arg.Any<SendNotificationRequest>(), Arg.Any<CancellationToken>())
            .Returns<SendNotificationResponse>(_ => throw new ValidationException("UserId phải > 0"));
        var useCase = new SendNotificationUseCase(_dispatcher);

        // Act & Assert: lỗi không bị nuốt, lan truyền lên controller
        await Assert.ThrowsAsync<ValidationException>(
            () => useCase.ExecuteAsync(new SendNotificationRequest(
                UserId: 0, Channel: NotificationChannel.InApp,
                TemplateKey: "X", Title: "t", Body: "b"))
        );
    }

    [Fact]
    public async Task RetryAsync_returns_dispatcher_response()
    {
        // Arrange: retry thành công sau lỗi tạm thời
        var expected = new SendNotificationResponse(9, NotificationStatus.Sent, "Retry OK");
        _dispatcher.RetryAsync(9, Arg.Any<CancellationToken>()).Returns(expected);
        var useCase = new RetryNotificationUseCase(_dispatcher);

        // Act
        var response = await useCase.ExecuteAsync(9);

        // Assert
        Assert.Equal(NotificationStatus.Sent, response.Status);
    }

    [Fact]
    public async Task RetryAsync_passes_notification_id_to_dispatcher()
    {
        // Arrange
        var useCase = new RetryNotificationUseCase(_dispatcher);
        using var cts = new CancellationTokenSource();

        // Act
        await useCase.ExecuteAsync(123, cts.Token);

        // Assert
        await _dispatcher.Received(1).RetryAsync(123, cts.Token);
    }

    [Fact]
    public async Task RetryAsync_dispatcher_not_found_propagates()
    {
        // Arrange: retry 1 notification không tồn tại
        _dispatcher.RetryAsync(999, Arg.Any<CancellationToken>())
            .Returns<SendNotificationResponse>(_ => throw new NotFoundException("Notification", 999));
        var useCase = new RetryNotificationUseCase(_dispatcher);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() => useCase.ExecuteAsync(999));
    }

    [Fact]
    public async Task SendAsync_failed_status_propagates_with_error_message()
    {
        // Arrange: vượt MaxRetryAttempts (NotificationConstants.MaxRetryAttempts = 3) → Failed
        var expected = new SendNotificationResponse(11, NotificationStatus.Failed,
            "Gửi thất bại sau 3 lần thử");
        _dispatcher.SendAsync(Arg.Any<SendNotificationRequest>(), Arg.Any<CancellationToken>())
            .Returns(expected);
        var useCase = new SendNotificationUseCase(_dispatcher);

        // Act
        var response = await useCase.ExecuteAsync(ValidRequest());

        // Assert: trạng thái Failed + lỗi cuối cùng được giữ nguyên để API trả về
        Assert.Equal(NotificationStatus.Failed, response.Status);
        Assert.Contains("3", response.Message);
    }
}
