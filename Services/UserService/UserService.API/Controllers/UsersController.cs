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
public sealed class UsersController(IGetUserByIdUseCase getUserById, IListUsersUseCase listUsers, ICurrentUser currentUser) : ControllerBase
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
}