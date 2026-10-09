using Microsoft.AspNetCore.Mvc;
using NotificationService.Application.Features;
using NotificationService.Application.Features.Dispatcher;
using NotificationService.Application.Features.DeviceTokens;
using NotificationService.Application.Features.Notifications;
using NotificationService.Application.Features.Preferences;

namespace NotificationService.API.Controllers;

/// <summary>
/// Notification Management API. Module: TV6 (S1-T602, S2+).
/// Endpoints:
/// - GET /api/notifications (inbox)
/// - PUT /api/notifications/{id}/read
/// - POST /api/notifications/send (internal)
/// - GET/PUT /api/notifications/preferences
/// - POST /api/devices (device token registration)
/// </summary>
[ApiController]
[Route("api/v1/notifications")]
public sealed class NotificationsController(
    IGetInboxUseCase getInbox,
    INotificationDispatcher dispatcher,
    IGetNotificationsUseCase getNotifications,
    IMarkNotificationAsReadUseCase markAsRead,
    IGetNotificationPreferencesUseCase getPreferences,
    IUpdateNotificationPreferencesUseCase updatePreferences,
    IRegisterDeviceTokenUseCase registerDeviceToken,
    IUnregisterDeviceTokenUseCase unregisterDeviceToken
) : ControllerBase
{
    /// <summary>
    /// GET /api/v1/notifications?userId=5&unreadOnly=false&page=1&pageSize=20
    /// Lấy danh sách thông báo của user (hộp thư).
    /// Module: TV6 (S1).
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<NotificationInboxDto>> Inbox(
        [FromQuery] int userId,
        [FromQuery] bool unreadOnly = false,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default
    )
    {
        if (userId <= 0)
            return BadRequest("userId phải > 0");

        return Ok(await getInbox.ExecuteAsync(userId, unreadOnly, page, pageSize, cancellationToken));
    }

    /// <summary>
    /// PUT /api/v1/notifications/{id}/read
    /// Đánh dấu thông báo là đã đọc.
    /// Module: TV6 (S2).
    /// </summary>
    [HttpPut("{notificationId}/read")]
    public async Task<ActionResult> MarkAsRead(
        [FromRoute] int notificationId,
        [FromQuery] int userId,
        CancellationToken cancellationToken = default
    )
    {
        if (notificationId <= 0 || userId <= 0)
            return BadRequest("notificationId và userId phải > 0");

        try
        {
            await markAsRead.ExecuteAsync(notificationId, userId, cancellationToken);
            return NoContent();
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Lỗi khi đánh dấu đã đọc", error = ex.Message });
        }
    }

    /// <summary>
    /// POST /api/v1/notifications/send
    /// Gửi thông báo (internal API - dùng cho service internal hoặc admin).
    /// Module: TV6 (S1-T602).
    /// </summary>
    [HttpPost("send")]
    public async Task<ActionResult<SendNotificationResponse>> Send(
        [FromBody] SendNotificationRequest request,
        CancellationToken cancellationToken = default
    )
    {
        if (request?.UserId <= 0)
            return BadRequest("UserId phải > 0");

        if (string.IsNullOrWhiteSpace(request.Title) || string.IsNullOrWhiteSpace(request.Body))
            return BadRequest("Title và Body không được trống");

        try
        {
            var response = await dispatcher.SendAsync(request, cancellationToken);
            return Ok(response);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Lỗi gửi thông báo", error = ex.Message });
        }
    }

    /// <summary>
    /// GET /api/v1/notifications/preferences?userId=5
    /// Lấy notification preferences của user.
    /// Module: TV6 (S4).
    /// </summary>
    [HttpGet("preferences")]
    public async Task<ActionResult<GetPreferencesResponse>> GetPreferences(
        [FromQuery] int userId,
        CancellationToken cancellationToken = default
    )
    {
        if (userId <= 0)
            return BadRequest("userId phải > 0");

        try
        {
            var prefs = await getPreferences.ExecuteAsync(userId, cancellationToken);
            return Ok(prefs);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Lỗi lấy preferences", error = ex.Message });
        }
    }

    /// <summary>
    /// PUT /api/v1/notifications/preferences?userId=5
    /// Cập nhật notification preferences.
    /// Module: TV6 (S4).
    /// </summary>
    [HttpPut("preferences")]
    public async Task<ActionResult> UpdatePreferences(
        [FromQuery] int userId,
        [FromBody] UpdatePreferencesRequest request,
        CancellationToken cancellationToken = default
    )
    {
        if (userId <= 0)
            return BadRequest("userId phải > 0");

        try
        {
            await updatePreferences.ExecuteAsync(userId, request, cancellationToken);
            return NoContent();
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Lỗi cập nhật preferences", error = ex.Message });
        }
    }
}

/// <summary>
/// Device Token Management API (FCM). Module: TV6 (S1-T602).
/// </summary>
[ApiController]
[Route("api/v1/devices")]
public sealed class DeviceTokenController(
    IRegisterDeviceTokenUseCase registerDeviceToken,
    IUnregisterDeviceTokenUseCase unregisterDeviceToken
) : ControllerBase
{
    /// <summary>
    /// POST /api/v1/devices
    /// Đăng ký device token cho push notifications (FCM).
    /// Module: TV6 (S1-T602).
    /// </summary>
    [HttpPost]
    public async Task<ActionResult> RegisterDevice(
        [FromBody] RegisterDeviceTokenRequest request,
        CancellationToken cancellationToken = default
    )
    {
        if (request?.UserId <= 0 || string.IsNullOrWhiteSpace(request.Token))
            return BadRequest("UserId > 0 và Token không được trống");

        try
        {
            await registerDeviceToken.ExecuteAsync(request, cancellationToken);
            return Created($"/api/v1/devices/{request.DeviceId}", null);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Lỗi đăng ký device token", error = ex.Message });
        }
    }

    /// <summary>
    /// DELETE /api/v1/devices/{deviceId}?userId=5
    /// Hủy đăng ký device token.
    /// Module: TV6 (S1-T602).
    /// </summary>
    [HttpDelete("{deviceId}")]
    public async Task<ActionResult> UnregisterDevice(
        [FromRoute] string deviceId,
        [FromQuery] int userId,
        CancellationToken cancellationToken = default
    )
    {
        if (string.IsNullOrWhiteSpace(deviceId) || userId <= 0)
            return BadRequest("deviceId và userId không được trống");

        try
        {
            await unregisterDeviceToken.ExecuteAsync(userId, deviceId, cancellationToken);
            return NoContent();
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Lỗi hủy device token", error = ex.Message });
        }
    }
}
