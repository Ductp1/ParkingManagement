using Microsoft.AspNetCore.Mvc;
using NotificationService.Application.Features;

namespace NotificationService.API.Controllers;

[ApiController]
[Route("api/notifications")]
public sealed class NotificationsController(IGetInboxUseCase getInbox) : ControllerBase
{
    /// <summary>GET /api/notifications?userId=5&amp;unreadOnly=false&amp;page=1&amp;pageSize=20 – Hộp thông báo của người dùng.</summary>
    [HttpGet]
    public async Task<ActionResult<NotificationInboxDto>> Inbox([FromQuery] int userId, [FromQuery] bool unreadOnly = false,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default)
        => Ok(await getInbox.ExecuteAsync(userId, unreadOnly, page, pageSize, cancellationToken));
}
