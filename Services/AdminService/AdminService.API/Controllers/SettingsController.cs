using AdminService.Application.Features;
using Microsoft.AspNetCore.Mvc;

namespace AdminService.API.Controllers;

[ApiController]
[Route("api/v1/admin/settings")]
public sealed class SettingsController(IGetPlatformSettingsUseCase getSettings) : ControllerBase
{
    /// <summary>GET /api/admin/settings – 24 tham số nghiệp vụ + 8 feature flag (UC-44).</summary>
    [HttpGet]
    public async Task<ActionResult<PlatformSettingsDto>> Get(CancellationToken cancellationToken)
        => Ok(await getSettings.ExecuteAsync(cancellationToken));
}
