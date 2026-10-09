using Microsoft.AspNetCore.Mvc;
using ParkingManagement.SharedKernel.Enums;
using ParkingService.Application.Features.ParkingLots;

namespace ParkingService.API.Controllers;

public sealed record CreateParkingLotRequest(
    int OwnerProfileId,
    string Name,
    string Address,
    string City,
    string? District,
    double Latitude,
    double Longitude,
    int TotalSlots,
    int MaxHeightCm,
    TimeOnly OpenTime,
    TimeOnly CloseTime,
    IntegrationTier IntegrationTier = IntegrationTier.Manual,
    string? Description = null,
    string? HotlinePhone = null);

public sealed record UpdateParkingLotRequest(
    int OwnerProfileId,
    string Name,
    string Address,
    string City,
    string? District,
    int MaxHeightCm,
    TimeOnly OpenTime,
    TimeOnly CloseTime,
    string? Description = null,
    string? HotlinePhone = null);

/// <summary>
/// Controller quản lý bãi đỗ xe: Tìm kiếm, chi tiết, đăng ký bãi mới và cây phân cấp.
/// </summary>
[ApiController]
[Route("api/v1/parking-lots")]
public sealed class ParkingLotsController(
    IGetParkingLotByIdUseCase getParkingLotById,
    ISearchParkingLotsUseCase searchParkingLots,
    ICreateParkingLotUseCase createParkingLot,
    IUpdateParkingLotUseCase updateParkingLot,
    IGetOwnerParkingLotsUseCase getOwnerParkingLots,
    IGetParkingLotHierarchyUseCase getParkingLotHierarchy) : ControllerBase
{
    /// <summary>GET /api/v1/parking-lots/{id} – Xem chi tiết bãi đỗ (UC-09).</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType<ParkingLotDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ParkingLotDto>> GetById(int id, CancellationToken cancellationToken)
        => Ok(await getParkingLotById.ExecuteAsync(new GetParkingLotByIdQuery(id), cancellationToken));

    /// <summary>GET /api/v1/parking-lots/search – Tìm bãi gần nhất theo tọa độ (UC-08).</summary>
    [HttpGet("search")]
    [ProducesResponseType<IReadOnlyList<ParkingLotSearchItemDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ParkingLotSearchItemDto>>> Search(
        [FromQuery] double lat, [FromQuery] double lng, [FromQuery] double radiusKm = 5, [FromQuery] int? vehicleHeightCm = null,
        CancellationToken cancellationToken = default)
        => Ok(await searchParkingLots.ExecuteAsync(new SearchParkingLotsQuery(lat, lng, radiusKm, vehicleHeightCm), cancellationToken));

    /// <summary>POST /api/v1/parking-lots – Đăng ký bãi đỗ xe mới (US-053).</summary>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult> Create([FromBody] CreateParkingLotRequest request, CancellationToken cancellationToken)
    {
        var command = new CreateParkingLotCommand(
            request.OwnerProfileId,
            request.Name,
            request.Address,
            request.City,
            request.District,
            request.Latitude,
            request.Longitude,
            request.TotalSlots,
            request.MaxHeightCm,
            request.OpenTime,
            request.CloseTime,
            request.IntegrationTier,
            request.Description,
            request.HotlinePhone);

        var id = await createParkingLot.ExecuteAsync(command, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id }, new { id });
    }

    /// <summary>PUT /api/v1/parking-lots/{id} – Cập nhật thông tin bãi đỗ xe.</summary>
    [HttpPut("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult> Update(int id, [FromBody] UpdateParkingLotRequest request, CancellationToken cancellationToken)
    {
        var command = new UpdateParkingLotCommand(
            id,
            request.OwnerProfileId,
            request.Name,
            request.Address,
            request.City,
            request.District,
            request.MaxHeightCm,
            request.OpenTime,
            request.CloseTime,
            request.Description,
            request.HotlinePhone);

        await updateParkingLot.ExecuteAsync(command, cancellationToken);
        return NoContent();
    }

    /// <summary>GET /api/v1/parking-lots/owner/{ownerProfileId} – Danh sách bãi của chủ bãi (Owner Portal).</summary>
    [HttpGet("owner/{ownerProfileId:int}")]
    [ProducesResponseType<IReadOnlyList<OwnerParkingLotItemDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<OwnerParkingLotItemDto>>> GetByOwner(int ownerProfileId, CancellationToken cancellationToken)
        => Ok(await getOwnerParkingLots.ExecuteAsync(ownerProfileId, cancellationToken));

    /// <summary>
    /// GET /api/v1/parking-lots/{id}/hierarchy – Toàn bộ cây cấu trúc Lot → Zone → Floor → Slot.
    /// HỢP ĐỒNG BÀN GIAO SỐ 4 GIAO 23/10 cho TV2, TV6, TV7.
    /// </summary>
    [HttpGet("{id:int}/hierarchy")]
    [ProducesResponseType<ParkingLotHierarchyDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ParkingLotHierarchyDto>> GetHierarchy(int id, CancellationToken cancellationToken)
        => Ok(await getParkingLotHierarchy.ExecuteAsync(new GetParkingLotHierarchyQuery(id), cancellationToken));
}
