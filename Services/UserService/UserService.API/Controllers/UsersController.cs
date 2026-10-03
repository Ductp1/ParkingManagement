using Microsoft.AspNetCore.Mvc;
using ParkingManagement.SharedKernel.Contracts;
using ParkingManagement.SharedKernel.Enums;
using UserService.Application.Features.Users;

namespace UserService.API.Controllers;

[ApiController]
[Route("api/users")]
public sealed class UsersController(IGetUserByIdUseCase getUserById, IListUsersUseCase listUsers) : ControllerBase
{
    /// <summary>GET /api/users/{id} – Hồ sơ người dùng kèm vai trò và hồ sơ chủ bãi (nếu có).</summary>
    [HttpGet("{id:int}")]
    public async Task<ActionResult<UserDto>> GetById(int id, CancellationToken cancellationToken)
        => Ok(await getUserById.ExecuteAsync(id, cancellationToken));

    /// <summary>GET /api/users?role=Driver&amp;page=1&amp;pageSize=20 – Danh sách người dùng (UC-40).</summary>
    [HttpGet]
    public async Task<ActionResult<PagedResult<UserSummaryDto>>> List(
        [FromQuery] UserRoleType? role, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default)
        => Ok(await listUsers.ExecuteAsync(role, page, pageSize, cancellationToken));
}
