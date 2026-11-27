using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UserService.Application.Features.Identity;

namespace UserService.API.Controllers;

/// <summary>
/// US-096 – Admin đình chỉ / mở lại / xóa (ẩn danh hóa) một user. AdminService gọi qua Gateway (chuyển tiếp JWT Admin) và ghi AuditLog;
/// admin thực hiện lấy từ JWT, không từ body. Lý do bắt buộc, RowVersion sai → 409.
/// </summary>
[ApiController]
[Route("api/v1/users")]
[Authorize(Policy = "RequireAdmin")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class UserLifecycleController(UserLifecycleUseCases lifecycle) : ControllerBase
{
    /// <summary>POST /api/v1/users/7/suspend { reason, lockedUntilUtc?, rowVersion } – 200; null lockedUntilUtc = vô thời hạn.</summary>
    [HttpPost("{id:int}/suspend")]
    [ProducesResponseType<UserLifecycleResult>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<UserLifecycleResult>> Suspend(int id, [FromBody] SuspendUserRequest request, CancellationToken ct)
        => Ok(await lifecycle.SuspendAsync(id, request, ct));

    /// <summary>POST /api/v1/users/7/reactivate { reason, rowVersion }.</summary>
    [HttpPost("{id:int}/reactivate")]
    [ProducesResponseType<UserLifecycleResult>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<UserLifecycleResult>> Reactivate(int id, [FromBody] ReactivateUserRequest request, CancellationToken ct)
        => Ok(await lifecycle.ReactivateAsync(id, request, ct));

    /// <summary>POST /api/v1/users/7/erase { reason, rowVersion } – ẩn danh hóa, không hoàn tác. Nghĩa vụ ở service khác do AdminService kiểm trước.</summary>
    [HttpPost("{id:int}/erase")]
    [ProducesResponseType<UserLifecycleResult>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<UserLifecycleResult>> Erase(int id, [FromBody] EraseUserRequest request, CancellationToken ct)
        => Ok(await lifecycle.EraseAsync(id, request, ct));
}
