using Microsoft.AspNetCore.Mvc;
using ParkingManagement.SharedKernel.Enums;
using SupportService.Application.Features;

namespace SupportService.API.Controllers;

[ApiController]
[Route("api/v1/complaints")]
public sealed class ComplaintsController(IListOwnerComplaintsUseCase listForOwner) : ControllerBase
{
    /// <summary>GET /api/complaints?ownerProfileId=1&amp;status=AwaitingOwner – Hộp tranh chấp của chủ bãi (UC-38).</summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ComplaintDto>>> ListForOwner(
        [FromQuery] int ownerProfileId, [FromQuery] ComplaintStatus? status, CancellationToken cancellationToken)
        => Ok(await listForOwner.ExecuteAsync(ownerProfileId, status, cancellationToken));
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
