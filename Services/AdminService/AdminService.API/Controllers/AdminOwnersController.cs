using AdminService.Application.Features.Owners;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ParkingManagement.SharedKernel.Contracts;

namespace AdminService.API.Controllers;

/// <summary>US-096: Admin quản lý chủ bãi. Controller chỉ điều hướng HTTP → Use case. Chỉ role Admin được gọi.</summary>
[ApiController]
[Route("api/v1/admin/owners")]
[Authorize(Policy = "RequireAdmin")]
public sealed class AdminOwnersController(IGetOwnerSanctionsUseCase getSanctions, ILockOwnerUseCase lockOwner,
    IUnlockOwnerUseCase unlockOwner, ICurrentUser currentUser) : ControllerBase
{
    /// <summary>POST /api/v1/admin/owners/2/lock – Khóa tài khoản chủ bãi (gọi lại khi chưa đồng bộ được = thử lại).</summary>
    [HttpPost("{ownerProfileId:int}/lock")]
    public async Task<ActionResult<SanctionDto>> Lock(int ownerProfileId, [FromBody] LockOwnerRequest request, CancellationToken cancellationToken)
        => currentUser.UserId is { } adminUserId          // người thao tác = chủ token, không nhận từ body
            ? Ok(await lockOwner.ExecuteAsync(ownerProfileId, request, adminUserId, cancellationToken))
            : Unauthorized();

    /// <summary>POST /api/v1/admin/owners/2/unlock – Mở khóa tài khoản chủ bãi (gọi lại khi chưa đồng bộ được = thử lại).</summary>
    [HttpPost("{ownerProfileId:int}/unlock")]
    public async Task<ActionResult<SanctionDto>> Unlock(int ownerProfileId, [FromBody] UnlockOwnerRequest request, CancellationToken cancellationToken)
        => currentUser.UserId is { } adminUserId
            ? Ok(await unlockOwner.ExecuteAsync(ownerProfileId, request, adminUserId, cancellationToken))
            : Unauthorized();

    /// <summary>GET /api/v1/admin/owners/2/sanctions?page=1&amp;pageSize=20 – Lịch sử vi phạm (chế tài) của một chủ bãi.</summary>
    [HttpGet("{ownerProfileId:int}/sanctions")]
    public async Task<ActionResult<PagedResult<SanctionDto>>> ListSanctions(
        int ownerProfileId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default)
        => Ok(await getSanctions.ExecuteAsync(ownerProfileId, page, pageSize, cancellationToken));
}
