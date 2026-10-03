using Microsoft.AspNetCore.Mvc;
using VehicleService.Application.Features.Vehicles;

namespace VehicleService.API.Controllers;

[ApiController]
[Route("api/v1/vehicles")]
public sealed class VehiclesController(IGetVehiclesByUserUseCase getByUser, IFindVehicleByPlateUseCase findByPlate) : ControllerBase
{
    /// <summary>GET /api/vehicles?userId=5 – Garage xe của tài xế (UC-07).</summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<VehicleDto>>> ListByUser([FromQuery] int userId, CancellationToken cancellationToken)
        => Ok(await getByUser.ExecuteAsync(userId, cancellationToken));

    /// <summary>GET /api/vehicles/by-plate/51F-123.45 – Tra xe theo biển số (GateService dùng khi OCR).</summary>
    [HttpGet("by-plate/{plate}")]
    public async Task<ActionResult<VehicleDto>> FindByPlate(string plate, CancellationToken cancellationToken)
        => Ok(await findByPlate.ExecuteAsync(plate, cancellationToken));
}
