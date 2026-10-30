using BookingService.Application.Features.MonthlyPasses;
using Microsoft.AspNetCore.Mvc;

namespace BookingService.API.Controllers;

public sealed record RegisterMonthlyPassRequest(
    int ParkingLotId, int UserId, int VehicleId, string PlateNumber,
    int Months = 1, int? OwnerProfileId = null, int? SlotId = null, string? SlotCode = null);

/// <summary>
/// US-066 (luật 4): đăng ký thuê bao mới — bị chặn khi A &gt;= Q
/// (400 kèm mã SUBSCRIBER_QUOTA_FULL trong message). BookingService hiện chưa đăng ký JWT.
/// </summary>
[ApiController]
[Route("api/v1/monthly-passes")]
public class MonthlyPassesController(IRegisterMonthlyPassUseCase registerMonthlyPass) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType<MonthlyPassDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<MonthlyPassDto>> Register(
        [FromBody] RegisterMonthlyPassRequest request, CancellationToken cancellationToken)
    {
        var dto = await registerMonthlyPass.ExecuteAsync(new RegisterMonthlyPassCommand(
            request.ParkingLotId, request.UserId, request.VehicleId, request.PlateNumber,
            request.Months, request.OwnerProfileId, request.SlotId, request.SlotCode), cancellationToken);
        return StatusCode(StatusCodes.Status201Created, dto);
    }
}

/// <summary>Internal API cho GateService: đếm thuê bao đang hiệu lực (A) để hiển thị runtime.</summary>
[ApiController]
[Route("api/v1/internal/monthly-passes")]
public class MonthlyPassInternalController(IMonthlyPassRegistrationStore store) : ControllerBase
{
    [HttpGet("active-count")]
    [ProducesResponseType<int>(StatusCodes.Status200OK)]
    public async Task<ActionResult<int>> ActiveCount([FromQuery] int lotId, CancellationToken cancellationToken)
        => Ok(await store.CountActiveAsync(lotId, cancellationToken));
}