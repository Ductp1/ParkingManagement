using ParkingManagement.SharedKernel.Contracts;
using ParkingManagement.SharedKernel.Exceptions;

namespace NotificationService.Application.Features;

public sealed record NotificationDto(int Id, string Channel, string TemplateKey, string Title, string Body, string? DataJson,
    string Status, bool IsRead, DateTime CreatedAtUtc);

public interface INotificationQueries
{
    Task<PagedResult<NotificationDto>> ListByUserAsync(int UserId, bool unreadOnly, int page, int pageSize, CancellationToken cancellationToken);
    Task<int> CountUnreadAsync(int userId, CancellationToken cancellationToken);
}

public sealed record NotificationInboxDto(int UnreadCount, PagedResult<NotificationDto> Items);

public interface IGetInboxUseCase
{
    Task<NotificationInboxDto> ExecuteAsync(int userId, bool unreadOnly, int page, int pageSize, CancellationToken cancellationToken = default);
}

/// <summary>Chuông thông báo trong app: số chưa đọc + danh sách mới nhất. Thông báo mới được đẩy real-time qua SignalR (/hubs/notify).</summary>
public sealed class GetInboxUseCase(INotificationQueries queries) : IGetInboxUseCase
{
    public async Task<NotificationInboxDto> ExecuteAsync(int userId, bool unreadOnly, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        if (userId <= 0) throw new ValidationException("userId phải là số nguyên dương.");
        if (page < 1 || pageSize is < 1 or > 100) throw new ValidationException("page ≥ 1 và pageSize trong khoảng 1–100.");

        var unread = await queries.CountUnreadAsync(userId, cancellationToken);
        var items = await queries.ListByUserAsync(userId, unreadOnly, page, pageSize, cancellationToken);
        return new NotificationInboxDto(unread, items);
    }
}
