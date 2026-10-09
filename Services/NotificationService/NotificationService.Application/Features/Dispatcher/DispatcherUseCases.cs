namespace NotificationService.Application.Features.Dispatcher;

/// <summary>
/// Use case: Gửi thông báo. Module: TV6 (S1-T602).
/// </summary>
public sealed class SendNotificationUseCase(INotificationDispatcher dispatcher) : ISendNotificationUseCase
{
    public async Task<SendNotificationResponse> ExecuteAsync(
        SendNotificationRequest request,
        CancellationToken cancellationToken = default
    )
    {
        return await dispatcher.SendAsync(request, cancellationToken);
    }
}

/// <summary>
/// Use case: Retry gửi thông báo. Module: TV6 (S1-T602).
/// </summary>
public sealed class RetryNotificationUseCase(INotificationDispatcher dispatcher) : IRetryNotificationUseCase
{
    public async Task<SendNotificationResponse> ExecuteAsync(
        int notificationId,
        CancellationToken cancellationToken = default
    )
    {
        return await dispatcher.RetryAsync(notificationId, cancellationToken);
    }
}
