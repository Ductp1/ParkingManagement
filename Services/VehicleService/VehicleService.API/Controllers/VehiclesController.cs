using Microsoft.AspNetCore.Mvc;
using VehicleService.Application.DTOs;
using VehicleService.Application.Interfaces;

namespace VehicleService.API.Controllers;

[ApiController]
[Route("api/v1/vehicles")]
public sealed class VehiclesController(IVehicleService vehicleService) : ControllerBase
{
    /// <summary>GET /api/v1/vehicles?userId=5 – Garage xe của tài xế (UC-07).</summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<VehicleDto>>> ListByUser([FromQuery] int userId, CancellationToken cancellationToken)
        => Ok(await vehicleService.GetVehiclesByUserAsync(userId, cancellationToken));

    /// <summary>GET /api/v1/vehicles/by-plate/51F-123.45 – Tra xe theo biển số (GateService dùng khi OCR).</summary>
    [HttpGet("by-plate/{plate}")]
    public async Task<ActionResult<VehicleDto>> FindByPlate(string plate, CancellationToken cancellationToken)
        => Ok(await vehicleService.FindVehicleByPlateAsync(plate, cancellationToken));

    /// <summary>POST /api/v1/vehicles – Thêm xe mới vào Garage (US-009, US-010).</summary>
    [HttpPost]
    public async Task<ActionResult<VehicleDto>> Create([FromBody] CreateVehicleRequestDto request, CancellationToken cancellationToken)
    {
        var result = await vehicleService.CreateVehicleAsync(request, cancellationToken);
        return CreatedAtAction(nameof(ListByUser), new { userId = result.UserId }, result);
    }

    /// <summary>PATCH /api/v1/vehicles/{id}/default?userId=5 – Đặt xe làm mặc định (Task P0.3, US-010).</summary>
    [HttpPatch("{id:int}/default")]
    public async Task<ActionResult<VehicleDto>> SetDefault([FromRoute] int id, [FromQuery] int userId, CancellationToken cancellationToken)
        => Ok(await vehicleService.SetDefaultVehicleAsync(id, userId, cancellationToken));

    /// <summary>PUT /api/v1/vehicles/{id} – Cập nhật thông tin xe trong Garage (Task P0.4, US-011).</summary>
    [HttpPut("{id:int}")]
    public async Task<ActionResult<VehicleDto>> Update([FromRoute] int id, [FromBody] UpdateVehicleRequestDto request, CancellationToken cancellationToken)
        => Ok(await vehicleService.UpdateVehicleAsync(id, request, cancellationToken));
}
