using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using ParkingManagement.SharedKernel.Contracts;
using ParkingManagement.SharedKernel.Enums;
using UserService.Application.Features.Users;
using UserService.Application.Features.Identity;

namespace UserService.API.Controllers;

[ApiController]
[Route("api/v1/users")]
[Authorize]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class UsersController(
    IGetUserByIdUseCase getUserById,
    IListUsersUseCase listUsers,
    ILockOwnerProfileUseCase lockOwnerProfile,
    IUnlockOwnerProfileUseCase unlockOwnerProfile,
    ICurrentUser currentUser) : ControllerBase
{
    /// <summary>GET /api/users/{id} – Hồ sơ người dùng kèm vai trò và hồ sơ chủ bãi (nếu có).</summary>
    [HttpGet("{id:int}")]
    public async Task<ActionResult<UserDto>> GetById(int id, CancellationToken cancellationToken)
    {
        if (currentUser.UserId != id && !currentUser.Roles.Contains("Admin")) return Forbid();
        return Ok(await getUserById.ExecuteAsync(id, cancellationToken));
    }

    /// <summary>GET /api/users?role=Driver&amp;page=1&amp;pageSize=20 – Danh sách người dùng (UC-40).</summary>
    [HttpGet]
    [Authorize(Policy = "RequireAdmin")]
    public async Task<ActionResult<PagedResult<UserSummaryDto>>> List(
        [FromQuery] UserRoleType? role, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default)
        => Ok(await listUsers.ExecuteAsync(role, page, pageSize, cancellationToken));

    [HttpGet("me")]
    public async Task<ActionResult<UserDto>> Me(CancellationToken ct)
        => currentUser.UserId is { } id ? Ok(await getUserById.ExecuteAsync(id, ct)) : Unauthorized();

    /// <summary>POST /api/v1/users/owner-profiles/{ownerProfileId}/lock – Khóa chủ bãi (chỉ Admin).</summary>
    [HttpPost("owner-profiles/{ownerProfileId:int}/lock")]
    [Authorize(Policy = "RequireAdmin")]
    public async Task<IActionResult> LockOwnerProfile(
        [FromRoute] int ownerProfileId,
        [FromBody] LockOwnerProfileRequest request,
        CancellationToken cancellationToken)
    {
        var result = await lockOwnerProfile.ExecuteAsync(ownerProfileId, request, cancellationToken);
        if (result is null)
        {
            return Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "OwnerProfileNotFound",
                detail: $"Hồ sơ chủ bãi với ID {ownerProfileId} không tồn tại.",
                instance: Request.Path);
        }

        return Ok(result);
    }

    /// <summary>POST /api/v1/users/owner-profiles/{ownerProfileId}/unlock – Mở khóa chủ bãi (chỉ Admin).</summary>
    [HttpPost("owner-profiles/{ownerProfileId:int}/unlock")]
    [Authorize(Policy = "RequireAdmin")]
    public async Task<IActionResult> UnlockOwnerProfile(
        [FromRoute] int ownerProfileId,
        [FromBody] UnlockOwnerProfileRequest request,
        CancellationToken cancellationToken)
    {
        var result = await unlockOwnerProfile.ExecuteAsync(ownerProfileId, request, cancellationToken);
        if (result is null)
        {
            return Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "OwnerProfileNotFound",
                detail: $"Hồ sơ chủ bãi với ID {ownerProfileId} không tồn tại.",
                instance: Request.Path);
        }

        return Ok(result);
    }
}