using AdminService.Application.Features.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ParkingManagement.SharedKernel.Contracts;
using ParkingManagement.SharedKernel.Enums;

namespace AdminService.API.Controllers;

/// <summary>US-096: Admin tra cứu người dùng. Controller chỉ điều hướng HTTP → Use case. Chỉ role Admin được gọi.</summary>
[ApiController]
[Route("api/v1/admin/users")]
[Authorize(Policy = "RequireAdmin")]
public sealed class AdminUsersController(IListAdminUsersUseCase listUsers, IGetAdminUserByIdUseCase getUser,
    IAdminUserLifecycleUseCase lifecycle, ICurrentUser currentUser) : ControllerBase
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

    /// <summary>POST /api/v1/admin/users/7/suspend { reason, lockedUntilUtc?, rowVersion } – đình chỉ (null = vô thời hạn). Lý do bắt buộc; RowVersion sai / tự khóa / Admin cuối → 409.</summary>
    [HttpPost("{userId:int}/suspend")]
    [ProducesResponseType<UserLifecycleResultDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public Task<ActionResult<UserLifecycleResultDto>> Suspend(int userId, [FromBody] SuspendAdminUserRequest request, CancellationToken cancellationToken)
        => AsAdmin(adminId => lifecycle.SuspendAsync(userId, request, adminId, cancellationToken));

    /// <summary>POST /api/v1/admin/users/7/reactivate { reason, rowVersion } – mở lại tài khoản đang bị đình chỉ.</summary>
    [HttpPost("{userId:int}/reactivate")]
    [ProducesResponseType<UserLifecycleResultDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public Task<ActionResult<UserLifecycleResultDto>> Reactivate(int userId, [FromBody] ReactivateAdminUserRequest request, CancellationToken cancellationToken)
        => AsAdmin(adminId => lifecycle.ReactivateAsync(userId, request, adminId, cancellationToken));

    /// <summary>
    /// POST /api/v1/admin/users/7/delete { reason, confirmEmail, rowVersion } – xóa = ẩn danh hóa, không hoàn tác. Còn booking mở, hợp đồng Signed,
    /// quyết toán chưa chi trả hoặc chế tài hiệu lực → 409 liệt kê lý do; không kiểm tra được → 503.
    /// </summary>
    [HttpPost("{userId:int}/delete")]
    [ProducesResponseType<UserLifecycleResultDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public Task<ActionResult<UserLifecycleResultDto>> Delete(int userId, [FromBody] DeleteAdminUserRequest request, CancellationToken cancellationToken)
        => AsAdmin(adminId => lifecycle.DeleteAsync(userId, request, adminId, cancellationToken));

    private async Task<ActionResult<UserLifecycleResultDto>> AsAdmin(Func<int, Task<UserLifecycleResultDto>> action)
        => currentUser.UserId is { } adminUserId ? Ok(await action(adminUserId)) : Unauthorized();
}
