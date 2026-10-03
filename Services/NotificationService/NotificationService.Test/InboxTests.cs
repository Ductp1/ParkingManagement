using NotificationService.Application.Features;
using ParkingManagement.SharedKernel.Contracts;
using ParkingManagement.SharedKernel.Exceptions;

namespace NotificationService.Test;

public class InboxTests
{
    [Fact]
    public async Task Inbox_returns_unread_count_with_items()
    {
        var inbox = await new GetInboxUseCase(new FakeQueries(unread: 3)).ExecuteAsync(5, unreadOnly: false, page: 1, pageSize: 20);
        Assert.Equal(3, inbox.UnreadCount);
    }

    [Fact]
    public async Task User_id_must_be_positive()
        => await Assert.ThrowsAsync<ValidationException>(() => new GetInboxUseCase(new FakeQueries(0)).ExecuteAsync(0, false, 1, 20));

    private sealed class FakeQueries(int unread) : INotificationQueries
    {
        public Task<PagedResult<NotificationDto>> ListByUserAsync(int userId, bool unreadOnly, int page, int pageSize, CancellationToken cancellationToken)
            => Task.FromResult(new PagedResult<NotificationDto>([], page, pageSize, 0));

        public Task<int> CountUnreadAsync(int userId, CancellationToken cancellationToken) => Task.FromResult(unread);
    }
}
