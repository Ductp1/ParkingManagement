using Microsoft.AspNetCore.Mvc;
using ParkingManagement.ServiceDefaults.Security;
using PaymentService.Application.Features;
using PaymentService.Application.Features.Payments;

namespace PaymentService.API.Controllers;

/// <summary>
/// UC-22 A2 / US-048: ghi và tra tiền thu tại cổng – CHỈ GateService gọi (khóa nội bộ, Gateway không route /internal).
/// </summary>
[ApiController]
[Route("internal/v1/gate-settlements")]
public sealed class InternalGateSettlementsController(InternalApiKeyAuthenticator authenticator,
    IRecordGateSettlementUseCase record, IGateSettlementQueries queries) : ControllerBase
{
    public const string Caller = "GateService";

    /// <summary>Ghi khoản thu của một lượt gửi (idempotent theo SessionCode); khác số tiền đã ghi → 409 GATE_PAYMENT_EXISTS.</summary>
    [HttpPost]
    public async Task<ActionResult<PaymentDto>> Record([FromBody] GateSettlementCommand command, CancellationToken cancellationToken)
    {
        Require();
        return Ok(await record.ExecuteAsync(command, cancellationToken));
    }

    /// <summary>Lượt gửi đã có khoản thu chưa: 200 hoặc 204.</summary>
    [HttpGet("{sessionCode}")]
    public async Task<ActionResult<PaymentDto>> Find(string sessionCode, CancellationToken cancellationToken)
    {
        Require();
        var payment = await queries.FindBySessionAsync(sessionCode, cancellationToken);
        return payment is null ? NoContent() : Ok(payment);
    }

    /// <summary>GET ?shiftId= – các khoản thu của một ca và tổng tiền mặt.</summary>
    [HttpGet]
    public async Task<ActionResult<GateShiftSettlementsDto>> ByShift([FromQuery] int shiftId, CancellationToken cancellationToken)
    {
        Require();
        return Ok(await queries.ListByShiftAsync(shiftId, cancellationToken));
    }

    private void Require() => authenticator.Require(Request.Headers.Authorization, Caller);
}
