using BookingService.Application.Features.Bookings;
using Microsoft.AspNetCore.Mvc;
using ParkingManagement.SharedKernel.Contracts;

namespace BookingService.API.Controllers;

/// <summary>
/// Internal API cho ParkingService (US-067/069): đếm booking theo bãi cho dashboard vận hành
/// và danh sách booking bị ảnh hưởng khi đóng bãi khẩn cấp (fan-out thông báo US-086).
/// Không auth — chỉ gọi service-to-service trong mạng nội bộ (Giống pattern GateService internal).
/// </summary>
[ApiController]
[Route("api/v1/internal/lot-bookings")]
public sealed class InternalLotBookingsController(ILotBookingSummaryQueries queries) : ControllerBase
{
    /// <summary>GET /api/v1/internal/lot-bookings/{lotId}/counts – { active, upcoming } cho dashboard (US-067).</summary>
    [HttpGet("{lotId:int}/counts")]
    [ProducesResponseType<object>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Counts(int lotId, CancellationToken cancellationToken)
    {
        var (active, upcoming) = await queries.GetCountsAsync(lotId, cancellationToken);
        return Ok(new { active, upcoming });
    }

    /// <summary>GET /api/v1/internal/lot-bookings/{lotId}/affected – booking chưa check-in bị ảnh hưởng khi đóng bãi (US-069).</summary>
    [HttpGet("{lotId:int}/affected")]
    [ProducesResponseType<IReadOnlyList<EmergencyAffectedBooking>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<EmergencyAffectedBooking>>> Affected(int lotId, CancellationToken cancellationToken)
        => Ok(await queries.GetAffectedAsync(lotId, cancellationToken));
}