using AdminService.Application.Features.Users;
using Microsoft.AspNetCore.Mvc;
using ParkingManagement.SharedKernel.Contracts;
using ParkingManagement.SharedKernel.Enums;

namespace AdminService.API.Controllers;

/// <summary>US-096: Admin tra cứu người dùng. Controller chỉ điều hướng HTTP → Use case.</summary>
[ApiController]
[Route("api/v1/admin/users")]
public sealed class AdminUsersController(IListAdminUsersUseCase listUsers, IGetAdminUserByIdUseCase getUser) : ControllerBase
{
    /// <summary>GET /api/v1/admin/users?role=LotOwner&amp;page=1&amp;pageSize=20 – Danh sách người dùng.</summary>
    [HttpGet]
    public async Task<ActionResult<PagedResult<AdminUserSummaryDto>>> List(
        [FromQuery] UserRoleType? role, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default)
        => Ok(await listUsers.ExecuteAsync(role, page, pageSize, cancellationToken));

    /// <summary>GET /api/v1/admin/users/2 – Chi tiết một người dùng.</summary>
    [HttpGet("{userId:int}")]
    public async Task<ActionResult<AdminUserDto>> GetById(int userId, CancellationToken cancellationToken)
        => Ok(await getUser.ExecuteAsync(userId, cancellationToken));
}
