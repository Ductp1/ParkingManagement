using Microsoft.AspNetCore.Mvc;
using NotificationService.Application.Features;
using NotificationService.Application.Features.Dispatcher;
using NotificationService.Application.Features.DeviceTokens;
using NotificationService.Application.Features.Notifications;
using NotificationService.Application.Features.Preferences;

namespace NotificationService.API.Controllers;

/// <summary>
/// Notification Management API. Module: TV6 (S1-T602, S2+).
/// Controller KHÔNG bắt try/catch – ExceptionHandlingMiddleware (ServiceDefaults) map:
/// ValidationException → 400 ProblemDetails, NotFoundException → 404, còn lại → 500.
/// POST /send trả kết quả CÓ CẤU TRÚC: status = Sent | Pending (lỗi tạm thời, còn retry)
/// | Failed (lỗi vĩnh viễn) để client/monitoring phân biệt rõ ràng.
/// </summary>
[ApiController]
[Route("api/v1/notifications")]
public sealed class NotificationsController(
    IGetInboxUseCase getInbox,
    INotificationDispatcher dispatcher,
    IMarkNotificationAsReadUseCase markAsRead,
    IGetNotificationPreferencesUseCase getPreferences,
    IUpdateNotificationPreferencesUseCase updatePreferences
) : ControllerBase
{
    /// <summary>GET /api/v1/notifications?userId=5&unreadOnly&page&pageSize – hộp thư (validate ở use case → 400).</summary>
    [HttpGet]
    public async Task<ActionResult<NotificationInboxDto>> Inbox(
        [FromQuery] int userId,
        [FromQuery] bool unreadOnly = false,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default
    ) => Ok(await getInbox.ExecuteAsync(userId, unreadOnly, page, pageSize, cancellationToken));

    /// <summary>PUT /api/v1/notifications/{id}/read – đánh dấu đã đọc. 404 nếu thông báo không thuộc user.</summary>
    [HttpPut("{notificationId}/read")]
    public async Task<ActionResult> MarkAsRead(
        [FromRoute] int notificationId,
        [FromQuery] int userId,
        CancellationToken cancellationToken = default
    )
    {
        await markAsRead.ExecuteAsync(notificationId, userId, cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// POST /api/v1/notifications/send
    /// Gửi thông báo (internal API - dùng cho service internal hoặc admin).
    /// Module: TV6 (S1-T602).
    /// </summary>
    /// <summary>
    /// POST /api/v1/notifications/send – internal API (service nội bộ / admin).
    /// Kết quả có cấu trúc: status = Sent | Pending (lỗi tạm thời → retry job xử lý tiếp)
    /// | Failed (lỗi vĩnh viễn, kèm message). Sai input → 400 ProblemDetails (middleware).
    /// </summary>
    [HttpPost("send")]
    public async Task<ActionResult<SendNotificationResponse>> Send(
        [FromBody] SendNotificationRequest request,
        CancellationToken cancellationToken = default
    ) => Ok(await dispatcher.SendAsync(request, cancellationToken));

    /// <summary>GET /api/v1/notifications/preferences?userId=5 – cài đặt thông báo (persist thật).</summary>
    [HttpGet("preferences")]
    public async Task<ActionResult<GetPreferencesResponse>> GetPreferences(
        [FromQuery] int userId,
        CancellationToken cancellationToken = default
    ) => Ok(await getPreferences.ExecuteAsync(userId, cancellationToken));

    /// <summary>PUT /api/v1/notifications/preferences?userId=5 – chỉ các flag gửi rõ ràng mới được cập nhật.</summary>
    [HttpPut("preferences")]
    public async Task<ActionResult> UpdatePreferences(
        [FromQuery] int userId,
        [FromBody] UpdatePreferencesRequest request,
        CancellationToken cancellationToken = default
    )
    {
        await updatePreferences.ExecuteAsync(userId, request, cancellationToken);
        return NoContent();
    }
}

/// <summary>
/// Device Token Management API (FCM). Module: TV6 (S1-T602).
/// deviceId = FCM token của thiết bị (mỗi cài đặt app 1 token). Đăng ký idempotent
/// (token đã tồn tại → cập nhật + bật lại). Thu hồi token không tồn tại → 404 (middleware).
/// </summary>
[ApiController]
[Route("api/v1/devices")]
public sealed class DeviceTokenController(
    IRegisterDeviceTokenUseCase registerDeviceToken,
    IUnregisterDeviceTokenUseCase unregisterDeviceToken
) : ControllerBase
{
    /// <summary>POST /api/v1/devices – đăng ký / re-đăng ký device token (idempotent).</summary>
    [HttpPost]
    public async Task<ActionResult> RegisterDevice(
        [FromBody] RegisterDeviceTokenRequest request,
        CancellationToken cancellationToken = default
    )
    {
        await registerDeviceToken.ExecuteAsync(request, cancellationToken);
        return Created($"/api/v1/devices/{request.DeviceId}", null);
    }

    /// <summary>DELETE /api/v1/devices/{deviceId}?userId=5 – thu hồi token (user logout / uninstall).</summary>
    [HttpDelete("{deviceId}")]
    public async Task<ActionResult> UnregisterDevice(
        [FromRoute] string deviceId,
        [FromQuery] int userId,
        CancellationToken cancellationToken = default
    )
    {
        await unregisterDeviceToken.ExecuteAsync(userId, deviceId, cancellationToken);
        return NoContent();
    }
}
