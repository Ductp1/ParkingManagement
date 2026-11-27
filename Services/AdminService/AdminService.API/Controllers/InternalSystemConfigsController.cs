using System.Globalization;
using AdminService.API.Security;
using AdminService.Application.Features.Settings;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ParkingManagement.SharedKernel.Exceptions;

namespace AdminService.API.Controllers;

/// <summary>
/// US-080: endpoint NỘI BỘ để PaymentService đọc chu kỳ quyết toán (SETTLEMENT_CYCLE) có hiệu lực tại một thời điểm.
/// Không nằm dưới /api/v1/admin nên Gateway không định tuyến tới: PaymentService gọi thẳng AdminService (:5108)
/// với khóa "InternalEvents:ApiKeys:PaymentService" (InternalApiKeyAuthenticator, như US-079) – không dùng JWT người dùng.
/// </summary>
[ApiController]
[Route("internal/v1/system-configs")]
[AllowAnonymous]
[ApiExplorerSettings(IgnoreApi = true)]
public sealed class InternalSystemConfigsController(InternalApiKeyAuthenticator authenticator, IResolveInternalSystemConfigUseCase resolve)
    : ControllerBase
{
    /// <summary>
    /// GET /internal/v1/system-configs/SETTLEMENT_CYCLE/resolve?atUtc=2026-11-02T00:00:00Z – header Authorization: Bearer {khóa nội bộ}.
    /// 200 { key, value, atUtc, effectiveFromUtc, isDefault }; atUtc bỏ trống = hiện tại.
    /// 400 atUtc sai / ở tương lai; 401 thiếu / sai khóa; 403 khóa của service khác hoặc tham số ngoài danh sách được đọc;
    /// 503 chưa cấu hình khóa hoặc thiếu tham số.
    /// </summary>
    [HttpGet("{key}/resolve")]
    [ProducesResponseType<InternalSystemConfigValueDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<InternalSystemConfigValueDto>> Resolve(string key, [FromQuery] string? atUtc,
        CancellationToken cancellationToken)
    {
        // Xác thực trước khi đọc tham số: request không có khóa luôn nhận 401, kể cả khi atUtc sai định dạng.
        var callerService = authenticator.Authenticate(Request.Headers.Authorization.ToString());
        return Ok(await resolve.ExecuteAsync(callerService, key, ParseUtc(atUtc), cancellationToken));
    }

    /// <summary>ISO-8601; không ghi múi giờ coi như UTC. Bỏ trống = null (hiện tại).</summary>
    private static DateTime? ParseUtc(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        return DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out var parsed)
            ? parsed
            : throw new ValidationException("atUtc phải là thời điểm ISO-8601, ví dụ 2026-11-02T00:00:00Z.");
    }
}
