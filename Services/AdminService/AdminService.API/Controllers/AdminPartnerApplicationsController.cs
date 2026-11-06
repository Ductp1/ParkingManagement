using AdminService.Application.Features.PartnerOnboarding;
using AdminService.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ParkingManagement.SharedKernel.Contracts;

namespace AdminService.API.Controllers;

/// <summary>
/// US-116: Admin thẩm định hồ sơ đăng ký đối tác chủ bãi và cấp tài khoản (Partner Onboarding, mục 14.2).
/// Hợp đồng ký theo từng bãi ở AdminLotContractsController.
/// Controller chỉ điều hướng HTTP → Use case. Chỉ role Admin; người thao tác lấy từ JWT, không nhận từ body.
/// </summary>
[ApiController]
[Route("api/v1/admin/partner-applications")]
[Authorize(Policy = "RequireAdmin")]
public sealed class AdminPartnerApplicationsController(
    IListPartnerApplicationsUseCase listApplications,
    IGetPartnerApplicationUseCase getApplication,
    IStartPartnerReviewUseCase startReview,
    IRequestPartnerInfoUseCase requestInfo,
    IRejectPartnerApplicationUseCase reject,
    ICancelPartnerApplicationUseCase cancel,
    IIssueOwnerAccountUseCase issueAccount,
    IResendOwnerActivationUseCase resendActivation,
    ICurrentUser currentUser) : ControllerBase
{
    /// <summary>GET /api/v1/admin/partner-applications?status=Submitted&amp;search=thu%20duc&amp;page=1&amp;pageSize=20 (A1).</summary>
    [HttpGet]
    [ProducesResponseType<PagedResult<PartnerApplicationSummaryDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResult<PartnerApplicationSummaryDto>>> List(
        [FromQuery] PartnerApplicationStatus? status, [FromQuery] string? search, [FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
        => Ok(await listApplications.ExecuteAsync(status, search, page, pageSize, cancellationToken));

    /// <summary>GET /api/v1/admin/partner-applications/12 (A2).</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType<PartnerApplicationDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PartnerApplicationDetailDto>> GetById(int id, CancellationToken cancellationToken)
        => Ok(await getApplication.ExecuteAsync(id, cancellationToken));

    /// <summary>POST /api/v1/admin/partner-applications/12/start-review (A3, T2).</summary>
    [HttpPost("{id:int}/start-review")]
    [ProducesResponseType<PartnerApplicationDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public Task<ActionResult<PartnerApplicationDetailDto>> StartReview(int id, CancellationToken cancellationToken)
        => AsAdmin(adminId => startReview.ExecuteAsync(id, adminId, cancellationToken));

    /// <summary>POST /api/v1/admin/partner-applications/12/request-info { note } (A4, T3).</summary>
    [HttpPost("{id:int}/request-info")]
    [ProducesResponseType<PartnerApplicationDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public Task<ActionResult<PartnerApplicationDetailDto>> RequestInfo(int id, [FromBody] PartnerRequestInfoRequest request,
        CancellationToken cancellationToken)
        => AsAdmin(adminId => requestInfo.ExecuteAsync(id, request, adminId, cancellationToken));

    /// <summary>POST /api/v1/admin/partner-applications/12/reject { reason } (A11, T9).</summary>
    [HttpPost("{id:int}/reject")]
    [ProducesResponseType<PartnerApplicationDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public Task<ActionResult<PartnerApplicationDetailDto>> Reject(int id, [FromBody] PartnerReasonRequest request,
        CancellationToken cancellationToken)
        => AsAdmin(adminId => reject.ExecuteAsync(id, request, adminId, cancellationToken));

    /// <summary>POST /api/v1/admin/partner-applications/12/cancel { reason } (A12, T10).</summary>
    [HttpPost("{id:int}/cancel")]
    [ProducesResponseType<PartnerApplicationDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public Task<ActionResult<PartnerApplicationDetailDto>> Cancel(int id, [FromBody] PartnerReasonRequest request,
        CancellationToken cancellationToken)
        => AsAdmin(adminId => cancel.ExecuteAsync(id, request, adminId, cancellationToken));

    /// <summary>
    /// POST /api/v1/admin/partner-applications/12/issue-account { bank: { bin, name, accountNumber, accountName } } (A9, T6).
    /// Tạo tài khoản chủ bãi bên UserService; tài khoản ngân hàng là tài khoản nhận thanh toán mặc định (Phụ lục 02 của hợp đồng bãi).
    /// </summary>
    [HttpPost("{id:int}/issue-account")]
    [ProducesResponseType<PartnerApplicationDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public Task<ActionResult<PartnerApplicationDetailDto>> IssueAccount(int id, [FromBody] IssueOwnerAccountRequest request,
        CancellationToken cancellationToken)
        => AsAdmin(adminId => issueAccount.ExecuteAsync(id, request, adminId, cancellationToken));

    /// <summary>POST /api/v1/admin/partner-applications/12/resend-activation (A10, T7).</summary>
    [HttpPost("{id:int}/resend-activation")]
    [ProducesResponseType<PartnerApplicationDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public Task<ActionResult<PartnerApplicationDetailDto>> ResendActivation(int id, CancellationToken cancellationToken)
        => AsAdmin(adminId => resendActivation.ExecuteAsync(id, adminId, cancellationToken));

    private async Task<ActionResult<PartnerApplicationDetailDto>> AsAdmin(Func<int, Task<PartnerApplicationDetailDto>> action)
        => currentUser.UserId is { } adminUserId ? Ok(await action(adminUserId)) : Unauthorized();
}
