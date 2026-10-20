using AdminService.Application.Features.PartnerOnboarding;
using AdminService.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ParkingManagement.SharedKernel.Contracts;

namespace AdminService.API.Controllers;

/// <summary>
/// US-116: Admin thẩm định hồ sơ đăng ký đối tác chủ bãi (Partner Onboarding, API 6.1).
/// Controller chỉ điều hướng HTTP → Use case. Chỉ role Admin; người thao tác lấy từ JWT.
/// </summary>
[ApiController]
[Route("api/v1/admin/partner-applications")]
[Authorize(Policy = "RequireAdmin")]
public sealed class AdminPartnerApplicationsController(
    IListPartnerApplicationsUseCase listApplications,
    IGetPartnerApplicationUseCase getApplication) : ControllerBase
{
    /// <summary>GET /api/v1/admin/partner-applications?status=Submitted&amp;search=thu%20duc&amp;page=1&amp;pageSize=20 (A1).</summary>
    [HttpGet]
    [ProducesResponseType<PagedResult<PartnerApplicationSummaryDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResult<PartnerApplicationSummaryDto>>> List(
        [FromQuery] PartnerApplicationStatus? status, [FromQuery] string? search, [FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
        => Ok(await listApplications.ExecuteAsync(status, search, page, pageSize, cancellationToken));

    /// <summary>GET /api/v1/admin/partner-applications/12 (A2).</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType<PartnerApplicationDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PartnerApplicationDetailDto>> GetById(int id, CancellationToken cancellationToken)
        => Ok(await getApplication.ExecuteAsync(id, cancellationToken));
}
