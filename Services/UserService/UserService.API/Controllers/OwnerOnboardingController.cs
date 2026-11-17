using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UserService.Application.Features.OwnerOnboarding;

namespace UserService.API.Controllers;

/// <summary>
/// US-053 / US-116 (U2): AdminService gọi khi Admin duyệt hồ sơ đối tác – thêm role LotOwner + OwnerProfile cho chính tài khoản
/// người nộp. Chỉ Admin (AdminService chuyển tiếp JWT của Admin qua Gateway). Header Idempotency-Key được nhận nhưng việc chống
/// tạo trùng dựa trên chính tài khoản: một user chỉ có một OwnerProfile, gọi lại trả Id cũ.
/// Controller riêng để không sửa UsersController của module TV1.
/// </summary>
[ApiController]
[Route("api/v1/users/owner-profiles")]
[Authorize(Policy = "RequireAdmin")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class OwnerOnboardingController(IGrantLotOwnerRoleUseCase grantLotOwner) : ControllerBase
{
    /// <summary>POST /api/v1/users/owner-profiles – trả { userId, ownerProfileId }. 409 kèm lý do khi không cấp được.</summary>
    [HttpPost]
    [ProducesResponseType<GrantedLotOwnerDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<GrantedLotOwnerDto>> Grant([FromBody] GrantLotOwnerRoleCommand command, CancellationToken cancellationToken)
        => Ok(await grantLotOwner.ExecuteAsync(command, cancellationToken));
}
