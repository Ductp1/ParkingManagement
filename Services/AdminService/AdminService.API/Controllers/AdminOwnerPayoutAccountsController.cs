using AdminService.Application.Features.PartnerOnboarding;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AdminService.API.Controllers;

/// <summary>
/// US-080: tài khoản nhận tiền quyết toán của chủ bãi (đã che, 4 số cuối) cho trang Quyết toán / Chi trả của Admin portal.
/// Tách khỏi AdminOwnersController (US-096/US-097) để không đụng file của nhánh khác. Chỉ role Admin được gọi.
/// </summary>
[ApiController]
[Route("api/v1/admin/owners")]
[Authorize(Policy = "RequireAdmin")]
public sealed class AdminOwnerPayoutAccountsController(IGetOwnerPayoutAccountUseCase getOne, IListOwnerPayoutAccountsUseCase list)
    : ControllerBase
{
    /// <summary>GET /api/v1/admin/owners/2/payout-account – 200 tài khoản (đã che); 404 chủ bãi chưa có hồ sơ đối tác / tài khoản nhận tiền.</summary>
    [HttpGet("{ownerProfileId:int}/payout-account")]
    [ProducesResponseType<OwnerPayoutAccountDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OwnerPayoutAccountDto>> GetOne(int ownerProfileId, CancellationToken cancellationToken)
        => Ok(await getOne.ExecuteAsync(ownerProfileId, cancellationToken));

    /// <summary>
    /// GET /api/v1/admin/owners/payout-accounts?ownerProfileIds=1,2,3 – tối đa 100 chủ bãi;
    /// 200 { items, missingOwnerProfileIds } theo đúng thứ tự gửi lên; 400 danh sách rỗng / sai / quá 100.
    /// </summary>
    [HttpGet("payout-accounts")]
    [ProducesResponseType<OwnerPayoutAccountListDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<OwnerPayoutAccountListDto>> List([FromQuery] string? ownerProfileIds, CancellationToken cancellationToken)
        => Ok(await list.ExecuteAsync(ownerProfileIds, cancellationToken));
}
