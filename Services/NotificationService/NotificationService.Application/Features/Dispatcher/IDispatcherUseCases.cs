namespace NotificationService.Application.Features.Dispatcher;

/// <summary>
/// Use case (Application layer) cho SendNotificationRequest.
/// Module: TV6 (S1-T602).
/// </summary>
public interface ISendNotificationUseCase
{
    Task<SendNotificationResponse> ExecuteAsync(
        SendNotificationRequest request,
        CancellationToken cancellationToken = default
    );
}

/// <summary>
/// Use case cho retry thông báo pending.
/// Module: TV6 (S1-T602).
/// </summary>
public interface IRetryNotificationUseCase
{
    Task<SendNotificationResponse> ExecuteAsync(
        int notificationId,
        CancellationToken cancellationToken = default
    );
}
