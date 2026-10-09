using AdminService.Application.Features;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AdminService.API.Controllers;

/// <summary>
/// UC-44 / US-098: Tham số hệ thống. Controller chỉ điều hướng HTTP → Use case.
/// Đọc: mọi tài khoản đã đăng nhập (ngưỡng chống gian lận không được lộ công khai qua Gateway).
/// </summary>
[ApiController]
[Route("api/v1/admin/settings")]
[Authorize]
public sealed class SettingsController(IGetPlatformSettingsUseCase getSettings, IGetSystemConfigByKeyUseCase getConfig) : ControllerBase
{
    /// <summary>GET /api/v1/admin/settings?at=2026-10-09T08:00:00Z – 30 tham số nghiệp vụ (giá trị hiệu lực tại <c>at</c>) + 8 feature flag.</summary>
    [HttpGet]
    public async Task<ActionResult<PlatformSettingsDto>> Get([FromQuery] DateTime? at, CancellationToken cancellationToken)
        => Ok(await getSettings.ExecuteAsync(at, cancellationToken));

    /// <summary>GET /api/v1/admin/settings/BOOKING_HOLD_MINUTES?at=2026-10-09T08:00:00Z – Một tham số theo khóa.</summary>
    [HttpGet("{key}")]
    public async Task<ActionResult<SystemConfigDetailDto>> GetByKey(string key, [FromQuery] DateTime? at, CancellationToken cancellationToken)
        => Ok(await getConfig.ExecuteAsync(key, at, cancellationToken));
}
