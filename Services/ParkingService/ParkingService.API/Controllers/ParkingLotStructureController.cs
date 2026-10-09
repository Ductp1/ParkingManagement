using Microsoft.AspNetCore.Mvc;
using ParkingManagement.SharedKernel.Enums;
using ParkingService.Application.Features.Floors;
using ParkingService.Application.Features.Slots;
using ParkingService.Application.Features.Zones;

namespace ParkingService.API.Controllers;

// ===== Requests =====
public sealed record CreateZoneRequest(string Code, string Name, bool IsOutdoor = false, int SortOrder = 0);
public sealed record UpdateZoneRequest(string Code, string Name, bool IsOutdoor, bool IsClosed, string? ClosedReason, int SortOrder);

public sealed record CreateFloorRequest(string Name, int Level, int? MaxHeightCm, int? MaxWeightKg, int GridColumns, int GridRows);
public sealed record UpdateFloorRequest(string Name, int? MaxHeightCm, int? MaxWeightKg, int GridColumns, int GridRows, bool IsClosed);

public sealed record CreateSlotRequest(string Code, SlotType SlotType = SlotType.Standard, VehicleType MaxVehicleType = VehicleType.Suv, int GridX = 0, int GridY = 0, int WidthCells = 1, int HeightCells = 1);
public sealed record BatchCreateSlotsRequest(string Prefix, int Count, int StartIndex = 1, SlotType SlotType = SlotType.Standard, VehicleType MaxVehicleType = VehicleType.Suv, int StartGridX = 0, int StartGridY = 0);
public sealed record UpdateSlotRequest(string Code, SlotType SlotType, VehicleType MaxVehicleType, int GridX, int GridY, int WidthCells, int HeightCells, bool IsActive);

/// <summary>
/// Controller quản lý cấu trúc phân tầng bãi xe: Zone → Floor → Slot (US-058).
/// Đi qua Gateway:5000 tại prefix /api/v1/parking-lots/...
/// </summary>
[ApiController]
[Route("api/v1/parking-lots")]
public sealed class ParkingLotStructureController(
    ICreateZoneUseCase createZone,
    IUpdateZoneUseCase updateZone,
    IDeleteZoneUseCase deleteZone,
    IGetZonesByLotUseCase getZonesByLot,
    ICreateFloorUseCase createFloor,
    IUpdateFloorUseCase updateFloor,
    IDeleteFloorUseCase deleteFloor,
    IGetFloorsByZoneUseCase getFloorsByZone,
    ICreateSlotUseCase createSlot,
    IBatchCreateSlotsUseCase batchCreateSlots,
    IUpdateSlotUseCase updateSlot,
    IDeleteSlotUseCase deleteSlot,
    IGetSlotsByFloorUseCase getSlotsByFloor) : ControllerBase
{
    // ==================== ZONES ====================

    /// <summary>GET /api/v1/parking-lots/{lotId}/zones – Lấy danh sách các khu vực trong bãi.</summary>
    [HttpGet("{lotId:int}/zones")]
    [ProducesResponseType<IReadOnlyList<ZoneDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ZoneDto>>> GetZones(int lotId, CancellationToken cancellationToken)
        => Ok(await getZonesByLot.ExecuteAsync(lotId, cancellationToken));

    /// <summary>POST /api/v1/parking-lots/{lotId}/zones – Tạo khu vực mới.</summary>
    [HttpPost("{lotId:int}/zones")]
    [ProducesResponseType<ZoneDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ZoneDto>> CreateZone(int lotId, [FromBody] CreateZoneRequest request, CancellationToken cancellationToken)
    {
        var command = new CreateZoneCommand(lotId, request.Code, request.Name, request.IsOutdoor, request.SortOrder);
        var created = await createZone.ExecuteAsync(command, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, created);
    }

    /// <summary>PUT /api/v1/parking-lots/zones/{id} – Cập nhật khu vực.</summary>
    [HttpPut("zones/{id:int}")]
    [ProducesResponseType<ZoneDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ZoneDto>> UpdateZone(int id, [FromBody] UpdateZoneRequest request, CancellationToken cancellationToken)
    {
        var command = new UpdateZoneCommand(id, request.Code, request.Name, request.IsOutdoor, request.IsClosed, request.ClosedReason, request.SortOrder);
        return Ok(await updateZone.ExecuteAsync(command, cancellationToken));
    }

    /// <summary>DELETE /api/v1/parking-lots/zones/{id} – Xóa khu vực (chặn xóa nếu còn tầng con).</summary>
    [HttpDelete("zones/{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult> DeleteZone(int id, CancellationToken cancellationToken)
    {
        await deleteZone.ExecuteAsync(id, cancellationToken);
        return NoContent();
    }

    // ==================== FLOORS ====================

    /// <summary>GET /api/v1/parking-lots/zones/{zoneId}/floors – Lấy danh sách tầng trong khu vực.</summary>
    [HttpGet("zones/{zoneId:int}/floors")]
    [ProducesResponseType<IReadOnlyList<FloorDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<FloorDto>>> GetFloors(int zoneId, CancellationToken cancellationToken)
        => Ok(await getFloorsByZone.ExecuteAsync(zoneId, cancellationToken));

    /// <summary>POST /api/v1/parking-lots/zones/{zoneId}/floors – Tạo tầng mới.</summary>
    [HttpPost("zones/{zoneId:int}/floors")]
    [ProducesResponseType<FloorDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<FloorDto>> CreateFloor(int zoneId, [FromBody] CreateFloorRequest request, CancellationToken cancellationToken)
    {
        var command = new CreateFloorCommand(zoneId, request.Name, request.Level, request.MaxHeightCm, request.MaxWeightKg, request.GridColumns, request.GridRows);
        var created = await createFloor.ExecuteAsync(command, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, created);
    }

    /// <summary>PUT /api/v1/parking-lots/floors/{id} – Cập nhật thông số tầng.</summary>
    [HttpPut("floors/{id:int}")]
    [ProducesResponseType<FloorDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FloorDto>> UpdateFloor(int id, [FromBody] UpdateFloorRequest request, CancellationToken cancellationToken)
    {
        var command = new UpdateFloorCommand(id, request.Name, request.MaxHeightCm, request.MaxWeightKg, request.GridColumns, request.GridRows, request.IsClosed);
        return Ok(await updateFloor.ExecuteAsync(command, cancellationToken));
    }

    /// <summary>DELETE /api/v1/parking-lots/floors/{id} – Xóa tầng (chặn xóa nếu còn ô đỗ).</summary>
    [HttpDelete("floors/{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult> DeleteFloor(int id, CancellationToken cancellationToken)
    {
        await deleteFloor.ExecuteAsync(id, cancellationToken);
        return NoContent();
    }

    // ==================== SLOTS ====================

    /// <summary>GET /api/v1/parking-lots/floors/{floorId}/slots – Lấy danh sách ô đỗ của tầng.</summary>
    [HttpGet("floors/{floorId:int}/slots")]
    [ProducesResponseType<IReadOnlyList<SlotDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<SlotDto>>> GetSlots(int floorId, CancellationToken cancellationToken)
        => Ok(await getSlotsByFloor.ExecuteAsync(floorId, cancellationToken));

    /// <summary>POST /api/v1/parking-lots/floors/{floorId}/slots – Tạo ô đỗ đơn lẻ.</summary>
    [HttpPost("floors/{floorId:int}/slots")]
    [ProducesResponseType<SlotDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<SlotDto>> CreateSlot(int floorId, [FromBody] CreateSlotRequest request, CancellationToken cancellationToken)
    {
        var command = new CreateSlotCommand(floorId, request.Code, request.SlotType, request.MaxVehicleType, request.GridX, request.GridY, request.WidthCells, request.HeightCells);
        var created = await createSlot.ExecuteAsync(command, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, created);
    }

    /// <summary>POST /api/v1/parking-lots/floors/{floorId}/slots/batch – Tạo hàng loạt ô đỗ tự động theo lưới.</summary>
    [HttpPost("floors/{floorId:int}/slots/batch")]
    [ProducesResponseType<int>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<int>> BatchCreateSlots(int floorId, [FromBody] BatchCreateSlotsRequest request, CancellationToken cancellationToken)
    {
        var command = new BatchCreateSlotsCommand(floorId, request.Prefix, request.Count, request.StartIndex, request.SlotType, request.MaxVehicleType, request.StartGridX, request.StartGridY);
        var count = await batchCreateSlots.ExecuteAsync(command, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, count);
    }

    /// <summary>PUT /api/v1/parking-lots/slots/{id} – Cập nhật cấu hình ô đỗ.</summary>
    [HttpPut("slots/{id:int}")]
    [ProducesResponseType<SlotDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<SlotDto>> UpdateSlot(int id, [FromBody] UpdateSlotRequest request, CancellationToken cancellationToken)
    {
        var command = new UpdateSlotCommand(id, request.Code, request.SlotType, request.MaxVehicleType, request.GridX, request.GridY, request.WidthCells, request.HeightCells, request.IsActive);
        return Ok(await updateSlot.ExecuteAsync(command, cancellationToken));
    }

    /// <summary>DELETE /api/v1/parking-lots/slots/{id} – Xóa ô đỗ (chặn xóa nếu slot đang có xe hoặc đang giữ chỗ).</summary>
    [HttpDelete("slots/{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult> DeleteSlot(int id, CancellationToken cancellationToken)
    {
        await deleteSlot.ExecuteAsync(id, cancellationToken);
        return NoContent();
    }
}
