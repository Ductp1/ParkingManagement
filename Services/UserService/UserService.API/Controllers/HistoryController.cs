using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UserService.Application.Features.History;

namespace UserService.API.Controllers;

/// <summary>US-082: lịch sử gửi xe và giao dịch của chính người dùng (dữ liệu đọc từ Booking/PaymentService).</summary>
[ApiController]
[Authorize]
[Route("api/v1/users/me/history")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class HistoryController(ParkingHistoryUseCases history) : ControllerBase
{
    /// <summary>GET /api/v1/users/me/history?filter=completed&amp;page=1</summary>
    [HttpGet]
    public async Task<ActionResult<ParkingHistoryPage>> Mine([FromQuery] string? filter, [FromQuery] int page = 1, CancellationToken ct = default)
        => Ok(await history.ListMineAsync(filter, page, ct));
}
