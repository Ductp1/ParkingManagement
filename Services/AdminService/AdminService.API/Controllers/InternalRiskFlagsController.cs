using System.Text.Json;
using AdminService.API.Security;
using AdminService.Application.Features.RiskFlags;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using ParkingManagement.SharedKernel.Exceptions;

namespace AdminService.API.Controllers;

/// <summary>
/// US-077: endpoint NỘI BỘ nhận cảnh báo bất thường từ service khác. Không nằm dưới /api/v1/admin nên Gateway không định tuyến tới:
/// GateService gọi thẳng AdminService (:5108). Xác thực bằng khóa nội bộ "InternalEvents:ApiKeys:GateService"
/// (InternalApiKeyAuthenticator, như T-802 / US-079) – không dùng JWT người dùng.
/// </summary>
[ApiController]
[Route("internal/v1/risk-flags")]
[AllowAnonymous]
[ApiExplorerSettings(IgnoreApi = true)]
public sealed class InternalRiskFlagsController(InternalApiKeyAuthenticator authenticator,
    IRaiseStaffCancellationAlertUseCase raiseStaffCancellationAlert, IOptions<JsonOptions> jsonOptions) : ControllerBase
{
    private const long MaxBodyBytes = 16 * 1024;

    /// <summary>
    /// POST /internal/v1/risk-flags/staff-abnormal-cancel – header Authorization: Bearer {khóa GateService}.
    /// Body: { staffUserId, parkingLotId, cancelCount, windowFromUtc, windowToUtc, warningThreshold, criticalThreshold,
    /// lastCancelAtUtc?, lastBookingCode? }. 200 { riskFlagId, ruleCode, severity, status, created, escalated, dueAtUtc };
    /// 400 body sai / chưa đạt ngưỡng; 401 thiếu / sai khóa; 403 khóa không phải GateService; 503 chưa cấu hình khóa.
    /// </summary>
    [HttpPost("staff-abnormal-cancel")]
    [RequestSizeLimit(MaxBodyBytes)]
    [ProducesResponseType<RiskFlagAlertResultDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<RiskFlagAlertResultDto>> StaffAbnormalCancel(CancellationToken cancellationToken)
    {
        // Xác thực trước khi đọc body: request không có khóa không được tốn bộ nhớ của AdminService.
        var callerService = authenticator.Authenticate(Request.Headers.Authorization.ToString());

        StaffCancellationAlertRequest? request;
        try
        {
            request = await JsonSerializer.DeserializeAsync<StaffCancellationAlertRequest>(Request.Body,
                jsonOptions.Value.JsonSerializerOptions, cancellationToken);
        }
        catch (JsonException)
        {
            throw new ValidationException("Body không phải JSON hợp lệ.");
        }
        catch (BadHttpRequestException ex) when (ex.StatusCode == StatusCodes.Status413PayloadTooLarge)
        {
            throw new ValidationException($"Body tối đa {MaxBodyBytes / 1024} KB.");
        }

        return Ok(await raiseStaffCancellationAlert.ExecuteAsync(callerService, request, cancellationToken));
    }
}
