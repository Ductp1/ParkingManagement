using Microsoft.AspNetCore.Mvc;
using ParkingService.Application.Features.ParkingLots;

namespace ParkingService.API.Controllers;

/// <summary>
/// Controller chỉ làm 3 việc: nhận HTTP request → gọi Use Case → trả HTTP response.
/// Không chứa business logic, không truy cập dữ liệu trực tiếp.
/// </summary>
[ApiController]
[Route("api/parking-lots")]
public sealed class ParkingLotsController(
    IGetParkingLotByIdUseCase getParkingLotById,
    ISearchParkingLotsUseCase searchParkingLots) : ControllerBase
{
    /// <summary>GET /api/parking-lots/{id} – Xem chi tiết bãi đỗ (UC-09).</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType<ParkingLotDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ParkingLotDto>> GetById(int id, CancellationToken cancellationToken)
        => Ok(await getParkingLotById.ExecuteAsync(new GetParkingLotByIdQuery(id), cancellationToken));

    /// <summary>GET /api/parking-lots/search?lat=10.776&amp;lng=106.70&amp;radiusKm=5&amp;vehicleHeightCm=168 – Tìm bãi gần nhất (UC-08).</summary>
    [HttpGet("search")]
    [ProducesResponseType<IReadOnlyList<ParkingLotSearchItemDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ParkingLotSearchItemDto>>> Search(
        [FromQuery] double lat, [FromQuery] double lng, [FromQuery] double radiusKm = 5, [FromQuery] int? vehicleHeightCm = null,
        CancellationToken cancellationToken = default)
        => Ok(await searchParkingLots.ExecuteAsync(new SearchParkingLotsQuery(lat, lng, radiusKm, vehicleHeightCm), cancellationToken));
}
