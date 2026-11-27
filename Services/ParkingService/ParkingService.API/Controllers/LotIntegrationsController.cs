using Microsoft.AspNetCore.Mvc;
using ParkingService.Application.Features.Integrations;

namespace ParkingService.API.Controllers;

/// <summary>Body đẩy booking mới về PMS (BookingService gửi sau khi tạo booking).</summary>
public sealed record PushBookingToPmsRequest(string BookingCode, string PlateNumber, string VehicleType,
    DateTime StartAtUtc, DateTime EndAtUtc, decimal TotalAmount);

/// <summary>
/// US-052 / UC-51: kết nối PMS Mức 1. ParkingService là chủ dữ liệu LotIntegrations (API key hash, webhook).
/// Hai endpoint nội bộ cho GateService (xác thực event PMS) và BookingService (đẩy booking mới).
/// </summary>
[ApiController]
[Route("api/v1/parking-lots")]
public sealed class LotIntegrationsController(
    IVerifyPmsApiKeyUseCase verifyApiKey,
    IPushBookingToPmsUseCase pushBooking) : ControllerBase
{
    /// <summary>
    /// POST /api/v1/parking-lots/integrations/verify – Body: { apiKey }. 200 = { parkingLotId, integrationId, providerName };
    /// 401 = key sai / kết nối không Active. Nội bộ cho GateService (TV7).
    /// </summary>
    [HttpPost("integrations/verify")]
    [ProducesResponseType<PmsIdentityDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<PmsIdentityDto>> Verify([FromBody] VerifyPmsApiKeyCommand command, CancellationToken cancellationToken)
        => Ok(await verifyApiKey.ExecuteAsync(command, cancellationToken));

    /// <summary>
    /// POST /api/v1/parking-lots/{lotId}/integrations/push-booking – đẩy booking mới về webhook PMS (ký HMAC).
    /// 404 = bãi chưa cấu hình PMS; 503 = PMS không nhận (đã lưu LastError). Nội bộ cho BookingService (TV3).
    /// </summary>
    [HttpPost("{lotId:int}/integrations/push-booking")]
    [ProducesResponseType<PushBookingToPmsResultDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<PushBookingToPmsResultDto>> PushBooking(
        int lotId, [FromBody] PushBookingToPmsRequest request, CancellationToken cancellationToken)
        => Ok(await pushBooking.ExecuteAsync(new PushBookingToPmsCommand(lotId, request.BookingCode, request.PlateNumber,
            request.VehicleType, request.StartAtUtc, request.EndAtUtc, request.TotalAmount), cancellationToken));
}
