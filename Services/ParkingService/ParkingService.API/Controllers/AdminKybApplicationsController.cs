using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ParkingManagement.SharedKernel.Contracts;
using ParkingManagement.SharedKernel.Enums;
using ParkingService.Application.Features.KybReview;

namespace ParkingService.API.Controllers;

/// <summary>
/// US-055: Admin duyệt / từ chối hồ sơ KYB của bãi mới. Route nằm dưới "parking-lots" để Gateway chuyển sang ParkingService
/// (route /api/v1/admin/** thuộc AdminService). Chỉ role Admin; người thao tác lấy từ JWT, không nhận từ body.
/// </summary>
[ApiController]
[Route("api/v1/parking-lots/kyb-applications")]
[Authorize(Policy = "RequireAdmin")]
public sealed class AdminKybApplicationsController(
    IListAdminKybApplicationsUseCase listApplications,
    IGetAdminKybApplicationUseCase getApplication,
    IStartKybReviewUseCase startReview,
    IScheduleKybFieldSurveyUseCase scheduleSurvey,
    IRecordKybFieldSurveyResultUseCase recordSurveyResult,
    IApproveKybApplicationUseCase approve,
    IRejectKybApplicationUseCase reject,
    IRequestKybMoreInfoUseCase requestMoreInfo,
    ICurrentUser currentUser) : ControllerBase
{
    /// <summary>GET /api/v1/parking-lots/kyb-applications?status=Submitted&amp;search=vincom&amp;page=1&amp;pageSize=20</summary>
    [HttpGet]
    [ProducesResponseType<PagedResult<AdminKybApplicationSummaryDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResult<AdminKybApplicationSummaryDto>>> List(
        [FromQuery] KybStatus? status, [FromQuery] string? search, [FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
        => Ok(await listApplications.ExecuteAsync(status, search, page, pageSize, cancellationToken));

    /// <summary>GET /api/v1/parking-lots/kyb-applications/5 – hồ sơ 4 phần + bãi chờ duyệt + so khớp GPS + điều kiện duyệt.</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType<AdminKybApplicationDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AdminKybApplicationDetailDto>> GetById(int id, CancellationToken cancellationToken)
        => Ok(await getApplication.ExecuteAsync(id, cancellationToken));

    /// <summary>POST …/5/start-review – Submitted → UnderReview.</summary>
    [HttpPost("{id:int}/start-review")]
    [ProducesResponseType<AdminKybApplicationDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public Task<ActionResult<AdminKybApplicationDetailDto>> StartReview(int id, CancellationToken cancellationToken)
        => AsAdmin(adminId => startReview.ExecuteAsync(id, adminId, cancellationToken));

    /// <summary>POST …/5/field-survey – lên / dời lịch khảo sát thực địa (UnderReview).</summary>
    [HttpPost("{id:int}/field-survey")]
    [ProducesResponseType<AdminKybApplicationDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public Task<ActionResult<AdminKybApplicationDetailDto>> ScheduleFieldSurvey(int id, [FromBody] ScheduleKybFieldSurveyRequest request,
        CancellationToken cancellationToken)
        => AsAdmin(adminId => scheduleSurvey.ExecuteAsync(id, request, adminId, cancellationToken));

    /// <summary>POST …/5/field-survey/result – xác nhận đã khảo sát thực địa kèm kết quả (sau giờ hẹn).</summary>
    [HttpPost("{id:int}/field-survey/result")]
    [ProducesResponseType<AdminKybApplicationDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public Task<ActionResult<AdminKybApplicationDetailDto>> RecordFieldSurveyResult(int id,
        [FromBody] RecordKybFieldSurveyResultRequest request, CancellationToken cancellationToken)
        => AsAdmin(adminId => recordSurveyResult.ExecuteAsync(id, request, adminId, cancellationToken));

    /// <summary>POST …/5/approve – UnderReview → Approved, bãi → Active.</summary>
    [HttpPost("{id:int}/approve")]
    [ProducesResponseType<AdminKybApplicationDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public Task<ActionResult<AdminKybApplicationDetailDto>> Approve(int id, [FromBody] ApproveKybApplicationRequest request,
        CancellationToken cancellationToken)
        => AsAdmin(adminId => approve.ExecuteAsync(id, request, adminId, cancellationToken));

    /// <summary>POST …/5/reject – UnderReview → Rejected, lý do bắt buộc.</summary>
    [HttpPost("{id:int}/reject")]
    [ProducesResponseType<AdminKybApplicationDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public Task<ActionResult<AdminKybApplicationDetailDto>> Reject(int id, [FromBody] RejectKybApplicationRequest request,
        CancellationToken cancellationToken)
        => AsAdmin(adminId => reject.ExecuteAsync(id, request, adminId, cancellationToken));

    /// <summary>POST …/5/request-info – UnderReview → NeedsMoreInfo, nội dung cần bổ sung bắt buộc.</summary>
    [HttpPost("{id:int}/request-info")]
    [ProducesResponseType<AdminKybApplicationDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public Task<ActionResult<AdminKybApplicationDetailDto>> RequestMoreInfo(int id, [FromBody] RequestKybMoreInfoRequest request,
        CancellationToken cancellationToken)
        => AsAdmin(adminId => requestMoreInfo.ExecuteAsync(id, request, adminId, cancellationToken));

    private async Task<ActionResult<AdminKybApplicationDetailDto>> AsAdmin(Func<int, Task<AdminKybApplicationDetailDto>> action)
        => currentUser.UserId is { } adminUserId ? Ok(await action(adminUserId)) : Unauthorized();
}
