using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using ParkingManagement.SharedKernel.Contracts;
using ParkingManagement.SharedKernel.Enums;
using UserService.Application.Features.Users;

namespace UserService.API.Controllers;

[ApiController]
[Route("api/v1/users")]
[Authorize]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class UsersController(
    IGetUserByIdUseCase getUserById,
    IListUsersUseCase listUsers,
    ProfileUseCases profiles,
    AvatarUseCases avatars,
    IAvatarStorage avatarStorage,
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

    [HttpPut("me")]
    public async Task<IActionResult> UpdateMe(UpdateProfileRequest request, CancellationToken ct)
    {
        await profiles.UpdateAsync(request, ct);
        return NoContent();
    }

    [HttpPost("me/avatar")]
    [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("auth")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(AvatarUseCases.MaxBytes + 65536)]
    [RequestFormLimits(MultipartBodyLengthLimit = AvatarUseCases.MaxBytes + 65536)]
    public async Task<ActionResult<AvatarResponse>> UploadAvatar(IFormFile file, CancellationToken ct)
    {
        await using var image = file.OpenReadStream();
        return Ok(await avatars.UploadAsync(image, file.Length, ct));
    }

    [HttpDelete("me/avatar")]
    public async Task<IActionResult> RemoveAvatar(CancellationToken ct)
    {
        await avatars.RemoveAsync(ct);
        return NoContent();
    }

    // Random immutable identifiers expose only normalized avatar pixels, never storage paths.
    [AllowAnonymous]
    [HttpGet("avatars/{id:guid}.png")]
    public async Task<IActionResult> Avatar(Guid id, CancellationToken ct)
    {
        var content = await avatarStorage.ReadAsync(id, ct);
        if (content is null) return NotFound();
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        return File(content, "image/png");
    }

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
