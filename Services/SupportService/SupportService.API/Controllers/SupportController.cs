using Microsoft.AspNetCore.Mvc;
using ParkingManagement.SharedKernel.Enums;
using SupportService.Application.DTOs;
using SupportService.Application.Features;
using SupportService.Application.Interfaces;

namespace SupportService.API.Controllers;

[ApiController]
[Route("api/v1/complaints")]
public sealed class ComplaintsController(IListOwnerComplaintsUseCase listForOwner, IComplaintService complaintService) : ControllerBase
{
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
public sealed class ReviewsController(IGetLotReviewsUseCase getLotReviews) : ControllerBase
{
    /// <summary>GET /api/reviews?parkingLotId=1 – Điểm trung bình và đánh giá mới nhất của bãi (UC-18).</summary>
    [HttpGet]
    public async Task<ActionResult<LotReviewsDto>> ForLot([FromQuery] int parkingLotId, [FromQuery] int take = 10, CancellationToken cancellationToken = default)
        => Ok(await getLotReviews.ExecuteAsync(parkingLotId, take, cancellationToken));
}
