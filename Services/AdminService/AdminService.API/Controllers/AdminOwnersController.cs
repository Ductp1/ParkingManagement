using AdminService.Application.Features.Owners;
using Microsoft.AspNetCore.Mvc;
using ParkingManagement.SharedKernel.Contracts;

namespace AdminService.API.Controllers;

/// <summary>US-096: Admin quản lý chủ bãi. Controller chỉ điều hướng HTTP → Use case.</summary>
[ApiController]
[Route("api/v1/admin/owners")]
public sealed class AdminOwnersController(IGetOwnerSanctionsUseCase getSanctions) : ControllerBase
{
    /// <summary>GET /api/v1/admin/owners/2/sanctions?page=1&amp;pageSize=20 – Lịch sử vi phạm (chế tài) của một chủ bãi.</summary>
    [HttpGet("{ownerProfileId:int}/sanctions")]
    public async Task<ActionResult<PagedResult<SanctionDto>>> ListSanctions(
        int ownerProfileId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default)
        => Ok(await getSanctions.ExecuteAsync(ownerProfileId, page, pageSize, cancellationToken));
}
