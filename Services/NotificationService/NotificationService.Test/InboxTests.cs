using NotificationService.Application.Features;
using ParkingManagement.SharedKernel.Contracts;
using ParkingManagement.SharedKernel.Exceptions;

namespace NotificationService.Test;

/// <summary>
/// Unit Tests cho NotificationService Inbox (S1-T601).
/// Kiểm tra: Get Inbox, validation, paging.
/// </summary>
public class InboxTests
{
    [Fact]
    public async Task Inbox_returns_unread_count_with_items()
    {
        // Arrange: Setup fake queries
        var inbox = new GetInboxUseCase(new FakeQueries(unread: 3));

        // Act: Execute with valid userId
        var result = await inbox.ExecuteAsync(userId: 5, unreadOnly: false, page: 1, pageSize: 20);

        // Assert
        Assert.Equal(3, result.UnreadCount);
        Assert.NotNull(result.Items);
    }

    [Fact]
    public async Task Inbox_negative_user_id_throws_validation_exception()
    {
        // Arrange
        var inbox = new GetInboxUseCase(new FakeQueries(unread: 0));

        // Act & Assert
        await Assert.ThrowsAsync<ValidationException>(
            () => inbox.ExecuteAsync(userId: -5, unreadOnly: false, page: 1, pageSize: 20)
        );
    }

    [Fact]
    public async Task Inbox_zero_user_id_throws_validation_exception()
    {
        // Arrange
        var inbox = new GetInboxUseCase(new FakeQueries(unread: 0));

        // Act & Assert
        await Assert.ThrowsAsync<ValidationException>(
            () => inbox.ExecuteAsync(userId: 0, unreadOnly: false, page: 1, pageSize: 20)
        );
    }

    [Theory]
    [InlineData(0)]        // page < 1
    [InlineData(-1)]       // page < 0
    public async Task Inbox_invalid_page_throws_validation_exception(int page)
    {
        // Arrange
        var inbox = new GetInboxUseCase(new FakeQueries(unread: 5));

        // Act & Assert
        await Assert.ThrowsAsync<ValidationException>(
            () => inbox.ExecuteAsync(userId: 5, unreadOnly: false, page: page, pageSize: 20)
        );
    }

    [Theory]
    [InlineData(0)]        // pageSize < 1
    [InlineData(-1)]       // pageSize < 0
    [InlineData(101)]      // pageSize > 100
    public async Task Inbox_invalid_page_size_throws_validation_exception(int pageSize)
    {
        // Arrange
        var inbox = new GetInboxUseCase(new FakeQueries(unread: 5));

        // Act & Assert
        await Assert.ThrowsAsync<ValidationException>(
            () => inbox.ExecuteAsync(userId: 5, unreadOnly: false, page: 1, pageSize: pageSize)
        );
    }

    [Fact]
    public async Task Inbox_unread_only_filters_correctly()
    {
        // Arrange: Return 0 items when unreadOnly=true (fake implementation)
        var inbox = new GetInboxUseCase(new FakeQueriesUnreadOnly());

        // Act
        var result = await inbox.ExecuteAsync(userId: 5, unreadOnly: true, page: 1, pageSize: 20);

        // Assert
        Assert.Equal(1, result.UnreadCount);
        Assert.Empty(result.Items.Items);  // Fake returns empty for unreadOnly
    }

    [Fact]
    public async Task Inbox_empty_result_returns_zero_count()
    {
        // Arrange
        var inbox = new GetInboxUseCase(new FakeQueriesEmpty());

        // Act
        var result = await inbox.ExecuteAsync(userId: 999, unreadOnly: false, page: 1, pageSize: 20);

        // Assert
        Assert.Equal(0, result.UnreadCount);
        Assert.Empty(result.Items.Items);
    }

    /// <summary>Fake implementation of INotificationQueries for testing (without mocks).</summary>
    private sealed class FakeQueries(int unread) : INotificationQueries
    {
        public Task<PagedResult<NotificationDto>> ListByUserAsync(
            int userId, bool unreadOnly, int page, int pageSize, CancellationToken cancellationToken)
        {
            var items = new List<NotificationDto>
            {
                new(1, "InApp", "TEST", "Title", "Body", null, "Sent", false, DateTime.UtcNow)
            };
            var result = new PagedResult<NotificationDto>(items, page, pageSize, unreadOnly ? 0 : 5);
            return Task.FromResult(result);
        }

        public Task<int> CountUnreadAsync(int userId, CancellationToken cancellationToken)
            => Task.FromResult(unread);
    }

    /// <summary>Fake implementation that simulates unreadOnly filter.</summary>
    private sealed class FakeQueriesUnreadOnly : INotificationQueries
    {
        public Task<PagedResult<NotificationDto>> ListByUserAsync(
            int userId, bool unreadOnly, int page, int pageSize, CancellationToken cancellationToken)
        {
            var items = unreadOnly ? new List<NotificationDto>() : new List<NotificationDto>
            {
                new(1, "InApp", "TEST", "Title", "Body", null, "Sent", false, DateTime.UtcNow)
            };
            var result = new PagedResult<NotificationDto>(items, page, pageSize, 1);
            return Task.FromResult(result);
        }

        public Task<int> CountUnreadAsync(int userId, CancellationToken cancellationToken)
            => Task.FromResult(1);
    }

    /// <summary>Fake implementation that returns empty results.</summary>
    private sealed class FakeQueriesEmpty : INotificationQueries
    {
        public Task<PagedResult<NotificationDto>> ListByUserAsync(
            int userId, bool unreadOnly, int page, int pageSize, CancellationToken cancellationToken)
        {
            var result = new PagedResult<NotificationDto>(new List<NotificationDto>(), page, pageSize, 0);
            return Task.FromResult(result);
        }

        public Task<int> CountUnreadAsync(int userId, CancellationToken cancellationToken)
            => Task.FromResult(0);
    }
}
