using System.Text.Json;
using AdminService.API.Security;
using AdminService.Application.Features.Commission;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using ParkingManagement.SharedKernel.Exceptions;

namespace AdminService.API.Controllers;

/// <summary>
/// US-079: endpoint NỘI BỘ cho job tính hoa hồng của PaymentService. Không nằm dưới /api/v1/admin nên Gateway không định tuyến tới:
/// PaymentService gọi thẳng AdminService (:5108). Xác thực bằng khóa nội bộ "InternalEvents:ApiKeys:PaymentService"
/// (InternalApiKeyAuthenticator, như T-802) – không dùng JWT người dùng.
/// </summary>
[ApiController]
[Route("internal/v1/commission-rates")]
[AllowAnonymous]
[ApiExplorerSettings(IgnoreApi = true)]
public sealed class InternalCommissionRatesController(InternalApiKeyAuthenticator authenticator, IResolveCommissionRatesUseCase resolve,
    IOptions<JsonOptions> jsonOptions) : ControllerBase
{
    private const long MaxBodyBytes = 64 * 1024;

    /// <summary>
    /// POST /internal/v1/commission-rates/resolve – body { items: [ { parkingLotId, atUtc } ] } (1–200 mục), header Authorization: Bearer.
    /// 200 { items: [ { parkingLotId, atUtc, rate (phân số), source, reason, contractNumber, contractVersion, effectiveFrom, effectiveTo } ] };
    /// 400 body sai; 401 thiếu / sai khóa; 403 khóa của service khác; 503 chưa cấu hình khóa hoặc thiếu DEFAULT_COMMISSION_RATE.
    /// </summary>
    [HttpPost("resolve")]
    [RequestSizeLimit(MaxBodyBytes)]
    [ProducesResponseType<ResolveCommissionRatesResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<ResolveCommissionRatesResponse>> Resolve(CancellationToken cancellationToken)
    {
        // Xác thực trước khi đọc body: request không có khóa không được tốn bộ nhớ của AdminService.
        var callerService = authenticator.Authenticate(Request.Headers.Authorization.ToString());

        ResolveCommissionRatesRequest? request;
        try
        {
            request = await JsonSerializer.DeserializeAsync<ResolveCommissionRatesRequest>(Request.Body,
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

        return Ok(await resolve.ExecuteAsync(callerService, request, cancellationToken));
    }
}
