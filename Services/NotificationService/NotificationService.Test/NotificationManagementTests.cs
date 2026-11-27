using NotificationService.Application.Features;
using NotificationService.Application.Features.Notifications;
using NSubstitute;
using ParkingManagement.SharedKernel.Contracts;
using ParkingManagement.SharedKernel.Exceptions;

namespace NotificationService.Test;

/// <summary>
/// Unit Tests cho Notification Management (S1+S2) – theo Test Plan v3 (TESTING_GUIDE.md).
/// GetNotificationsUseCase: hợp lệ, validation UserId/paging, forwarding unreadOnly, đếm chưa đọc.
/// MarkNotificationAsReadUseCase: validation + NotFound khi thông báo không thuộc user.
/// </summary>
public class NotificationManagementTests
{
    private readonly INotificationQueries _queries = Substitute.For<INotificationQueries>();
    private readonly INotificationCommands _commands = Substitute.For<INotificationCommands>();

    /// <summary>Cấu hình substitute trả về 1 trang kết quả mẫu.</summary>
    private void SetupQueries(int unreadCount, int total)
    {
        var items = new List<NotificationDto>
        {
            new(1, "InApp", "BOOKING_CONFIRMED", "Đặt chỗ thành công", "BK-0002",
                null, "Sent", false, DateTime.UtcNow)
        };
        _queries.CountUnreadAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(unreadCount);
        _queries.ListByUserAsync(Arg.Any<int>(), Arg.Any<bool>(), Arg.Any<int>(),
                Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new PagedResult<NotificationDto>(items, 1, 20, total));
    }

    // ===== GetNotificationsUseCase =====

    [Fact]
    public async Task GetNotifications_valid_request_returns_unread_count_and_items()
    {
        // Arrange
        SetupQueries(unreadCount: 3, total: 15);
        var useCase = new GetNotificationsUseCase(_queries);

        // Act
        var (unread, items) = await useCase.ExecuteAsync(new GetNotificationsRequest(5));

        // Assert
        Assert.Equal(3, unread);
        Assert.Single(items);
        Assert.Equal("BOOKING_CONFIRMED", items[0].TemplateKey);
        Assert.Equal("Sent", items[0].Status);
    }

    [Theory]
    [InlineData(0)]    // userId = 0
    [InlineData(-5)]   // userId âm
    public async Task GetNotifications_invalid_user_id_throws_argument_exception(int userId)
    {
        // Arrange (không cần setup queries – validation chặn trước khi gọi)
        var useCase = new GetNotificationsUseCase(_queries);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            () => useCase.ExecuteAsync(new GetNotificationsRequest(userId)));
    }

    [Fact]
    public async Task GetNotifications_zero_page_throws_argument_exception()
    {
        // Arrange
        var useCase = new GetNotificationsUseCase(_queries);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            () => useCase.ExecuteAsync(new GetNotificationsRequest(5, Page: 0)));
    }

    [Theory]
    [InlineData(0)]     // pageSize < 1
    [InlineData(101)]   // pageSize > 100
    public async Task GetNotifications_invalid_page_size_throws_argument_exception(int pageSize)
    {
        // Arrange
        var useCase = new GetNotificationsUseCase(_queries);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            () => useCase.ExecuteAsync(new GetNotificationsRequest(5, PageSize: pageSize)));
    }

    [Fact]
    public async Task GetNotifications_passes_unread_only_flag_and_paging_to_queries()
    {
        // Arrange
        SetupQueries(unreadCount: 2, total: 2);
        var useCase = new GetNotificationsUseCase(_queries);

        // Act
        await useCase.ExecuteAsync(new GetNotificationsRequest(5, UnreadOnly: true, Page: 2, PageSize: 50));

        // Assert: cờ unreadOnly + phân trang được truyền xuống query layer
        await _queries.Received(1).ListByUserAsync(5, true, 2, 50, Arg.Any<CancellationToken>());
        await _queries.Received(1).CountUnreadAsync(5, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetNotifications_queries_are_called_exactly_once_each()
    {
        // Arrange
        SetupQueries(unreadCount: 1, total: 1);
        var useCase = new GetNotificationsUseCase(_queries);

        // Act
        await useCase.ExecuteAsync(new GetNotificationsRequest(5));

        // Assert: 1 lần count + 1 lần list (không query thừa)
        await _queries.Received(1).CountUnreadAsync(Arg.Any<int>(), Arg.Any<CancellationToken>());
        await _queries.Received(1).ListByUserAsync(
            Arg.Any<int>(), Arg.Any<bool>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetNotifications_empty_inbox_returns_zero_unread_and_no_items()
    {
        // Arrange: user chưa có thông báo nào
        _queries.CountUnreadAsync(Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(0);
        _queries.ListByUserAsync(Arg.Any<int>(), Arg.Any<bool>(), Arg.Any<int>(),
                Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new PagedResult<NotificationDto>(
                new List<NotificationDto>(), 1, 20, 0));
        var useCase = new GetNotificationsUseCase(_queries);

        // Act
        var (unread, items) = await useCase.ExecuteAsync(new GetNotificationsRequest(999));

        // Assert
        Assert.Equal(0, unread);
        Assert.Empty(items);
    }

    // ===== MarkNotificationAsReadUseCase (S2) =====

    [Fact]
    public async Task MarkAsRead_valid_notification_marks_read_once()
    {
        // Arrange: thông báo thuộc về user
        _commands.MarkAsReadForUserAsync(7, 5, Arg.Any<CancellationToken>()).Returns(true);
        var useCase = new MarkNotificationAsReadUseCase(_commands);

        // Act
        await useCase.ExecuteAsync(notificationId: 7, userId: 5);

        // Assert
        await _commands.Received(1).MarkAsReadForUserAsync(7, 5, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task MarkAsRead_notification_not_owned_throws_not_found()
    {
        // Arrange: thông báo không tồn tại / không thuộc user
        _commands.MarkAsReadForUserAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(false);
        var useCase = new MarkNotificationAsReadUseCase(_commands);

        // Act & Assert: NotFoundException → 404 qua middleware
        await Assert.ThrowsAsync<NotFoundException>(() => useCase.ExecuteAsync(7, 999));
    }

    [Theory]
    [InlineData(0, 5)]     // notificationId = 0
    [InlineData(-1, 5)]    // notificationId âm
    [InlineData(7, 0)]     // userId = 0
    [InlineData(7, -3)]    // userId âm
    public async Task MarkAsRead_invalid_input_throws_argument_exception(int notificationId, int userId)
    {
        // Arrange
        var useCase = new MarkNotificationAsReadUseCase(_commands);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            () => useCase.ExecuteAsync(notificationId, userId));
    }
}
