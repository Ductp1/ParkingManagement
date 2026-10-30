using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ParkingManagement.SharedKernel.Contracts;
using ParkingManagement.SharedKernel.Enums;
using SupportService.Application.DTOs;
using SupportService.Application.Features;
using SupportService.Application.Interfaces;

namespace SupportService.API.Controllers;

[ApiController]
[Route("api/v1/complaints")]
public sealed class ComplaintsController(IListOwnerComplaintsUseCase listForOwner, IComplaintService complaintService,
    IOwnerDisputeService ownerDisputes, ICurrentUser currentUser) : ControllerBase
{
    /// <summary>GET /api/v1/complaints/payout-holds – Khoản quyết toán đang bị phong tỏa vì tranh chấp (US-089 AC3).</summary>
    /// <remarks>Chủ bãi chỉ thấy khoản của mình (lấy từ JWT hoặc fallback ownerProfileId); Admin xem tất cả hoặc lọc bằng <paramref name="ownerProfileId"/>.</remarks>
    [HttpGet("payout-holds")]
    [ProducesResponseType<IReadOnlyList<PayoutHoldDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<PayoutHoldDto>>> PayoutHolds([FromQuery] int? ownerProfileId, CancellationToken cancellationToken)
    {
        if (currentUser.Roles.Contains("Admin"))
            return Ok(await ownerDisputes.ListPayoutHoldsAsync(ownerProfileId, cancellationToken));
        var owner = currentUser.OwnerProfileId ?? ownerProfileId ?? 1;
        return Ok(await ownerDisputes.ListPayoutHoldsAsync(owner, cancellationToken));
    }

    /// <summary>GET /api/v1/complaints/CP-0001/owner-view – Chủ bãi xem tranh chấp về bãi của mình (US-089 AC1).</summary>
    [HttpGet("{code}/owner-view")]
    [ProducesResponseType<ComplaintDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ComplaintDetailDto>> OwnerView([FromRoute] string code, [FromQuery] int? ownerProfileId, CancellationToken cancellationToken)
    {
        var owner = currentUser.OwnerProfileId ?? ownerProfileId ?? 1;
        return Ok(await ownerDisputes.GetAsync(owner, code, cancellationToken));
    }

    /// <summary>POST /api/v1/complaints/CP-0001/owner-response – Chủ bãi phản hồi tranh chấp trong 48 giờ kèm bằng chứng (US-089 AC1).</summary>
    [HttpPost("{code}/owner-response")]
    [ProducesResponseType<ComplaintDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ComplaintDetailDto>> OwnerRespond([FromRoute] string code, [FromQuery] int? ownerProfileId, [FromBody] OwnerResponseRequestDto request,
        CancellationToken cancellationToken)
    {
        var owner = currentUser.OwnerProfileId ?? ownerProfileId ?? 1;
        var userId = currentUser.UserId ?? 1;
        return Ok(await ownerDisputes.RespondAsync(owner, userId, code, request, cancellationToken));
    }

    /// <summary>Token có vai trò LotOwner nhưng không gắn 1 hồ sơ chủ bãi (claim "OwnerProfileId").</summary>
    private ObjectResult OwnerProfileMissing()
        => Problem(statusCode: StatusCodes.Status403Forbidden, title: "Không có quyền",
            detail: "Tài khoản chưa gắn với một hồ sơ chủ bãi.");

    /// <summary>GET /api/complaints?ownerProfileId=1&amp;status=AwaitingOwner – Hộp tranh chấp của chủ bãi (UC-38).</summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ComplaintDto>>> ListForOwner(
        [FromQuery] int ownerProfileId, [FromQuery] ComplaintStatus? status, CancellationToken cancellationToken)
        => Ok(await listForOwner.ExecuteAsync(ownerProfileId, status, cancellationToken));

    /// <summary>POST /api/v1/complaints – Tài xế gửi khiếu nại / ticket hỗ trợ cho 1 booking (US-088).</summary>
    [HttpPost]
    [ProducesResponseType<ComplaintDetailDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ComplaintDetailDto>> Create([FromBody] CreateComplaintRequestDto request, CancellationToken cancellationToken)
    {
        var result = await complaintService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetMine), new { code = result.Code, userId = request.UserId }, result);
    }

    /// <summary>GET /api/v1/complaints/my?userId=5 – Danh sách khiếu nại của tôi (US-088 AC4).</summary>
    [HttpGet("my")]
    public async Task<ActionResult<IReadOnlyList<ComplaintSummaryDto>>> ListMine([FromQuery] int userId, CancellationToken cancellationToken)
        => Ok(await complaintService.ListForUserAsync(userId, cancellationToken));

    /// <summary>GET /api/v1/complaints/CP-20261009-1234?userId=5 – Chi tiết + hội thoại để theo dõi trạng thái (US-088 AC4).</summary>
    [HttpGet("{code}")]
    [ProducesResponseType<ComplaintDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ComplaintDetailDto>> GetMine(string code, [FromQuery] int userId, CancellationToken cancellationToken)
        => Ok(await complaintService.GetForUserAsync(code, userId, cancellationToken));
}

[ApiController]
[Route("api/v1/reviews")]
public sealed class ReviewsController(IGetLotReviewsUseCase getLotReviews, IReviewService reviewService,
    IReviewModerationService moderation, ICurrentUser currentUser) : ControllerBase
{
    /// <summary>POST /api/v1/reviews/5/owner-reply – Chủ bãi viết 1 phản hồi công khai cho đánh giá về bãi mình (US-094 AC1).</summary>
    [HttpPost("{id:int}/owner-reply")]
    [ProducesResponseType<ModeratedReviewDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ModeratedReviewDto>> OwnerReply([FromRoute] int id, [FromQuery] int? ownerProfileId, [FromBody] OwnerReplyRequestDto request,
        CancellationToken cancellationToken)
    {
        var owner = currentUser.OwnerProfileId ?? ownerProfileId ?? 1;
        return Ok(await moderation.ReplyAsync(owner, id, request, cancellationToken));
    }

    /// <summary>POST /api/v1/reviews/5/report – Chủ bãi khiếu nại đánh giá, chuyển Admin xem xét (US-094 AC2).</summary>
    [HttpPost("{id:int}/report")]
    [ProducesResponseType<ModeratedReviewDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ModeratedReviewDto>> Report([FromRoute] int id, [FromQuery] int? ownerProfileId, [FromBody] ReviewReasonRequestDto request,
        CancellationToken cancellationToken)
    {
        var owner = currentUser.OwnerProfileId ?? ownerProfileId ?? 1;
        return Ok(await moderation.ReportAsync(owner, id, request, cancellationToken));
    }

    /// <summary>GET /api/v1/reviews/reported – Hàng chờ Admin: đánh giá đang bị chủ bãi khiếu nại (US-094 AC2).</summary>
    [HttpGet("reported")]
    [Authorize(Policy = "RequireAdmin")]
    public async Task<ActionResult<IReadOnlyList<ModeratedReviewDto>>> Reported(CancellationToken cancellationToken)
        => Ok(await moderation.ListReportedAsync(cancellationToken));

    /// <summary>POST /api/v1/reviews/5/hide – Chỉ Admin được gỡ đánh giá; ghi audit log (US-094 AC2).</summary>
    [HttpPost("{id:int}/hide")]
    [Authorize(Policy = "RequireAdmin")]
    [ProducesResponseType<ModeratedReviewDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ModeratedReviewDto>> Hide([FromRoute] int id, [FromBody] ReviewReasonRequestDto request,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } admin) return Unauthorized();
        return Ok(await moderation.HideAsync(admin, id, request, cancellationToken));
    }

    /// <summary>POST /api/v1/reviews/5/dismiss-report – Admin bác khiếu nại, giữ nguyên đánh giá; ghi audit log (US-094 AC2).</summary>
    [HttpPost("{id:int}/dismiss-report")]
    [Authorize(Policy = "RequireAdmin")]
    [ProducesResponseType<ModeratedReviewDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ModeratedReviewDto>> DismissReport([FromRoute] int id, [FromBody] ReviewReasonRequestDto request,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } admin) return Unauthorized();
        return Ok(await moderation.DismissReportAsync(admin, id, request, cancellationToken));
    }

    private ObjectResult OwnerProfileMissing()
        => Problem(statusCode: StatusCodes.Status403Forbidden, title: "Không có quyền",
            detail: "Tài khoản chưa gắn với một hồ sơ chủ bãi.");

    /// <summary>POST /api/v1/reviews – Tài xế đang đăng nhập đánh giá bãi 1–5 sao sau khi booking hoàn tất, mỗi booking 1 lần (US-093).</summary>
    [HttpPost]
    [Authorize(Policy = "RequireDriver")]
    [ProducesResponseType<ReviewDetailDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ReviewDetailDto>> Create([FromBody] CreateReviewRequestDto request, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId) return Unauthorized();
        var result = await reviewService.CreateAsync(userId, request, cancellationToken);
        return CreatedAtAction(nameof(ForLot), new { parkingLotId = result.ParkingLotId }, result);
    }

    /// <summary>
    /// GET /api/v1/reviews/bookings/BK-0001/eligibility – Booking của người đang đăng nhập có hiện nút "Đánh giá" không (US-093 AC2).
    /// Mã booking là định danh bắt buộc nên đặt trên path; userId lấy từ JWT để không dò được booking của người khác (IDOR).
    /// </summary>
    [HttpGet("bookings/{bookingCode}/eligibility")]
    [Authorize(Policy = "RequireDriver")]
    [ProducesResponseType<ReviewEligibilityDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ReviewEligibilityDto>> Eligibility([FromRoute] string bookingCode, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId) return Unauthorized();
        return Ok(await reviewService.CheckEligibilityAsync(userId, bookingCode, cancellationToken));
    }

    /// <summary>GET /api/reviews?parkingLotId=1 – Điểm trung bình và đánh giá mới nhất của bãi (UC-18).</summary>
    [HttpGet]
    public async Task<ActionResult<LotReviewsDto>> ForLot([FromQuery] int parkingLotId, [FromQuery] int take = 10, CancellationToken cancellationToken = default)
        => Ok(await getLotReviews.ExecuteAsync(parkingLotId, take, cancellationToken));
}
