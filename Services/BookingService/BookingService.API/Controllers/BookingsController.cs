using BookingService.Application.Features.Bookings;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ParkingManagement.SharedKernel.Contracts;
using ParkingManagement.SharedKernel.Enums;

namespace BookingService.API.Controllers;

[ApiController]
[Route("api/v1/bookings")]
public sealed class BookingsController(
    IGetBookingByCodeUseCase getByCode,
    IListMyBookingsUseCase listMine,
    IPreviewCancellationUseCase previewCancellation,
    ICreateBookingUseCase createBooking,
    ICancelBookingUseCase cancelBooking,
    IModifyBookingUseCase modifyBooking,
    IExtendBookingUseCase extendBooking,
    IReviewBookingUseCase reviewBooking,
    ILotCancelBookingUseCase lotCancelBooking,
    IGateBookingActionsUseCase gateActions,
    IListNoShowCandidatesUseCase listNoShowCandidates,
    IListGateBookingsUseCase listGateBookings,
    IGetApprovalLogsUseCase getApprovalLogs,
    ICurrentUser currentUser) : ControllerBase
{
    /// <summary>GET /api/v1/bookings/BK-0002 – Chi tiết booking kèm giá đã khóa và lịch sử trạng thái.</summary>
    [HttpGet("{code}")]
    [ProducesResponseType<BookingDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BookingDetailDto>> GetByCode(string code, CancellationToken cancellationToken)
    {
        var result = await getByCode.ExecuteAsync(code, cancellationToken);
        // SEC-003 / UAT-SEC-03: Nếu là tài xế đã đăng nhập, chỉ được xem booking của chính mình
        if (currentUser.IsAuthenticated && currentUser.Roles.Contains("Driver") && !currentUser.Roles.Contains("Admin"))
        {
            if (currentUser.UserId.HasValue && result.UserId != currentUser.UserId.Value)
            {
                return Forbid();
            }
        }
        // Chủ bãi chỉ xem được chi tiết booking của bãi mình sở hữu
        if (currentUser.IsAuthenticated && currentUser.Roles.Contains("LotOwner") && !currentUser.Roles.Contains("Admin"))
        {
            if (currentUser.OwnerProfileId.HasValue && result.OwnerProfileId != currentUser.OwnerProfileId.Value)
            {
                return Forbid();
            }
        }
        return Ok(result);
    }

    /// <summary>
    /// GET /api/v1/bookings/gate?parkingLotId=1&amp;fromUtc=...&amp;toUtc=...&amp;search=BK|biển số – UC-20 (US-076).
    /// Booking trong ca trực của Staff App / Owner Portal, sắp theo giờ đến, có cờ "Quá giờ" (grace 15' – US-044).
    /// Khách vãng lai không nằm ở đây (đó là ParkingSessions của GateService). Không trả SĐT/CCCD.
    /// </summary>
    [HttpGet("gate")]
    [Authorize(Roles = "LotOwner,Staff,Admin")]
    [ProducesResponseType<IReadOnlyList<GateBookingListItemDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<GateBookingListItemDto>>> GateList(
        [FromQuery] int parkingLotId, [FromQuery] DateTime fromUtc, [FromQuery] DateTime toUtc,
        [FromQuery] string? search, [FromQuery] int gracePeriodMinutes = 15, CancellationToken cancellationToken = default)
    {
        int? ownerProfileId = null;
        if (currentUser.Roles.Contains("LotOwner") && !currentUser.Roles.Contains("Admin"))
        {
            if (!currentUser.OwnerProfileId.HasValue || currentUser.OwnerProfileId.Value <= 0)
            {
                return Forbid();
            }
            ownerProfileId = currentUser.OwnerProfileId.Value;
        }

        return Ok(await listGateBookings.ExecuteAsync(
            new GateBookingListQuery(parkingLotId,
                DateTime.SpecifyKind(fromUtc.ToUniversalTime(), DateTimeKind.Utc),
                DateTime.SpecifyKind(toUtc.ToUniversalTime(), DateTimeKind.Utc),
                search, gracePeriodMinutes, ownerProfileId), cancellationToken));
    }

    /// <summary>GET /api/v1/bookings?userId=5&amp;status=Confirmed&amp;page=1&amp;pageSize=20 – Booking của tôi (UC-13, BUG-LOAD-03).</summary>
    [HttpGet]
    [Authorize]
    [ProducesResponseType<IReadOnlyList<BookingSummaryDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<BookingSummaryDto>>> ListMine(
        [FromQuery] int userId, [FromQuery] BookingStatus? status,
        [FromQuery] int? page, [FromQuery] int? pageSize, CancellationToken cancellationToken)
    {
        // SEC-003 / UAT-SEC-03: Tài xế chỉ xem được danh sách của chính mình; Admin không truyền userId thì mặc định xem của mình
        if (userId == 0 && currentUser.UserId.HasValue)
        {
            userId = currentUser.UserId.Value;
        }
        else if (userId > 0 && !currentUser.Roles.Contains("Admin") && currentUser.UserId.HasValue && userId != currentUser.UserId.Value)
        {
            return Forbid();
        }
        return Ok(await listMine.ExecuteAsync(userId, status, page, pageSize, cancellationToken));
    }

    /// <summary>GET /api/v1/bookings/BK-0002/cancellation-preview – Xem trước hủy lúc này được hoàn bao nhiêu (UC-16).</summary>
    [HttpGet("{code}/cancellation-preview")]
    [Authorize]
    [ProducesResponseType<CancellationPreviewDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CancellationPreviewDto>> PreviewCancellation(string code, CancellationToken cancellationToken)
    {
        var detail = await getByCode.ExecuteAsync(code, cancellationToken);
        if (!currentUser.Roles.Contains("Admin"))
        {
            if (!currentUser.UserId.HasValue || detail.UserId != currentUser.UserId.Value)
            {
                return Forbid();
            }
        }
        return Ok(await previewCancellation.ExecuteAsync(code, cancellationToken));
    }

    /// <summary>
    /// GET /api/v1/bookings/approval-logs?parkingLotId=1&amp;fromUtc=...&amp;toUtc=...&amp;result=Approved|Rejected – T-303.
    /// Tra cứu lịch sử duyệt / từ chối đơn đặt chỗ bãi Mức 0 (phục vụ Owner Portal và Admin Portal).
    /// </summary>
    [HttpGet("approval-logs")]
    [Authorize(Roles = "LotOwner,Admin")]
    [ProducesResponseType<IReadOnlyList<ApprovalLogDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<ApprovalLogDto>>> GetApprovalLogs(
        [FromQuery] int? parkingLotId,
        [FromQuery] DateTime? fromUtc,
        [FromQuery] DateTime? toUtc,
        [FromQuery] string? result,
        CancellationToken cancellationToken)
    {
        int? ownerProfileId = null;
        if (!currentUser.Roles.Contains("Admin"))
        {
            if (!currentUser.OwnerProfileId.HasValue || currentUser.OwnerProfileId.Value <= 0)
            {
                return Forbid();
            }
            ownerProfileId = currentUser.OwnerProfileId.Value;
        }

        return Ok(await getApprovalLogs.ExecuteAsync(
            parkingLotId,
            fromUtc.HasValue ? DateTime.SpecifyKind(fromUtc.Value.ToUniversalTime(), DateTimeKind.Utc) : null,
            toUtc.HasValue ? DateTime.SpecifyKind(toUtc.Value.ToUniversalTime(), DateTimeKind.Utc) : null,
            result,
            ownerProfileId,
            cancellationToken));
    }

    /// <summary>POST /api/v1/bookings – Tạo booking và giữ chỗ 15 phút (US-022, US-024, US-025, US-026, US-027).</summary>
    [HttpPost]
    [Authorize]
    [ProducesResponseType<BookingDetailDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<BookingDetailDto>> Create([FromBody] CreateBookingCommand command, CancellationToken cancellationToken)
    {
        // SEC-001/SEC-004: Gắn userId từ JWT để chống giả mạo danh tính cho người dùng thường
        if (currentUser.UserId.HasValue && !currentUser.Roles.Contains("Admin"))
        {
            command = command with { UserId = currentUser.UserId.Value };
        }
        var result = await createBooking.ExecuteAsync(command, cancellationToken);
        return CreatedAtAction(nameof(GetByCode), new { code = result.Code }, result);
    }

    /// <summary>POST /api/v1/bookings/{code}/cancel – Khách hủy booking theo Cancellation Window 60 phút (US-038).</summary>
    [HttpPost("{code}/cancel")]
    [Authorize]
    [ProducesResponseType<CancellationResultDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CancellationResultDto>> Cancel(string code, [FromBody] CancelBookingCommand command, CancellationToken cancellationToken)
    {
        if (currentUser.UserId.HasValue && !currentUser.Roles.Contains("Admin"))
        {
            command = command with { UserId = currentUser.UserId.Value };
        }
        var detail = await getByCode.ExecuteAsync(code, cancellationToken);
        if (!currentUser.Roles.Contains("Admin"))
        {
            if (!currentUser.UserId.HasValue || detail.UserId != currentUser.UserId.Value)
            {
                return Forbid();
            }
        }
        return Ok(await cancelBooking.ExecuteAsync(code, command, cancellationToken));
    }

    /// <summary>PUT /api/v1/bookings/{code} – Sửa khung giờ hoặc xe trước Check-in (US-029).</summary>
    [HttpPut("{code}")]
    [Authorize]
    [ProducesResponseType<BookingDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BookingDetailDto>> Modify(string code, [FromBody] ModifyBookingCommand command, CancellationToken cancellationToken)
    {
        var detail = await getByCode.ExecuteAsync(code, cancellationToken);
        if (!currentUser.Roles.Contains("Admin"))
        {
            if (!currentUser.UserId.HasValue || detail.UserId != currentUser.UserId.Value)
            {
                return Forbid();
            }
        }
        return Ok(await modifyBooking.ExecuteAsync(code, command, cancellationToken));
    }

    /// <summary>POST /api/v1/bookings/{code}/extend – Gia hạn thời gian đỗ xe (US-030).</summary>
    [HttpPost("{code}/extend")]
    [Authorize]
    [ProducesResponseType<BookingDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BookingDetailDto>> Extend(string code, [FromBody] ExtendBookingCommand command, CancellationToken cancellationToken)
    {
        var detail = await getByCode.ExecuteAsync(code, cancellationToken);
        if (!currentUser.Roles.Contains("Admin"))
        {
            if (!currentUser.UserId.HasValue || detail.UserId != currentUser.UserId.Value)
            {
                return Forbid();
            }
        }
        return Ok(await extendBooking.ExecuteAsync(code, command, cancellationToken));
    }

    /// <summary>POST /api/v1/bookings/{code}/approve – Chủ bãi duyệt booking (Bãi Mức 0 - US-031).</summary>
    [HttpPost("{code}/approve")]
    [Authorize(Roles = "LotOwner,Admin")]
    [ProducesResponseType<BookingDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BookingDetailDto>> Approve(string code, [FromBody] ReviewBookingCommand command, CancellationToken cancellationToken)
    {
        if (!currentUser.Roles.Contains("Admin"))
        {
            if (!currentUser.OwnerProfileId.HasValue || currentUser.OwnerProfileId.Value <= 0)
            {
                return Forbid();
            }
            command = command with { OwnerProfileId = currentUser.OwnerProfileId.Value };
        }
        return Ok(await reviewBooking.ApproveAsync(code, command, cancellationToken));
    }

    /// <summary>POST /api/v1/bookings/{code}/reject – Chủ bãi từ chối booking (Bãi Mức 0 - US-031).</summary>
    [HttpPost("{code}/reject")]
    [Authorize(Roles = "LotOwner,Admin")]
    [ProducesResponseType<BookingDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BookingDetailDto>> Reject(string code, [FromBody] ReviewBookingCommand command, CancellationToken cancellationToken)
    {
        if (!currentUser.Roles.Contains("Admin"))
        {
            if (!currentUser.OwnerProfileId.HasValue || currentUser.OwnerProfileId.Value <= 0)
            {
                return Forbid();
            }
            command = command with { OwnerProfileId = currentUser.OwnerProfileId.Value };
        }
        return Ok(await reviewBooking.RejectAsync(code, command, cancellationToken));
    }

    /// <summary>POST /api/v1/bookings/{code}/lot-cancel – Chủ bãi hủy booking do sự cố bãi (Hoàn 100% - US-042).</summary>
    [HttpPost("{code}/lot-cancel")]
    [Authorize(Roles = "LotOwner,Admin")]
    [ProducesResponseType<BookingDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BookingDetailDto>> LotCancel(string code, [FromBody] LotCancelBookingCommand command, CancellationToken cancellationToken)
    {
        if (!currentUser.Roles.Contains("Admin"))
        {
            if (!currentUser.OwnerProfileId.HasValue || currentUser.OwnerProfileId.Value <= 0)
            {
                return Forbid();
            }
            command = command with { OwnerProfileId = currentUser.OwnerProfileId.Value };
        }
        return Ok(await lotCancelBooking.ExecuteAsync(code, command, cancellationToken));
    }

    /// <summary>POST /api/v1/bookings/{code}/check-in – Cổng xác nhận xe vào (Nội bộ cho TV7 - GateService).</summary>
    [HttpPost("{code}/check-in")]
    [ProducesResponseType<BookingDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BookingDetailDto>> CheckIn(string code, [FromBody] GateActionCommand command, CancellationToken cancellationToken)
        => Ok(await gateActions.CheckInAsync(code, command, cancellationToken));

    /// <summary>POST /api/v1/bookings/{code}/check-out – Cổng xác nhận xe ra (Nội bộ cho TV7 - GateService).</summary>
    [HttpPost("{code}/check-out")]
    [ProducesResponseType<BookingDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BookingDetailDto>> CheckOut(string code, [FromBody] GateActionCommand command, CancellationToken cancellationToken)
        => Ok(await gateActions.CheckOutAsync(code, command, cancellationToken));

    /// <summary>POST /api/v1/bookings/{code}/no-show – Cổng đánh dấu No-show sau 30 phút (Nội bộ cho TV7 - GateService).</summary>
    [HttpPost("{code}/no-show")]
    [ProducesResponseType<BookingDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BookingDetailDto>> NoShow(string code, [FromBody] GateActionCommand command, CancellationToken cancellationToken)
        => Ok(await gateActions.NoShowAsync(code, command, cancellationToken));

    /// <summary>
    /// GET /api/v1/bookings/no-show-candidates?parkingLotId=2&amp;graceMinutes=30 – US-039 (UC-24 bước 1):
    /// booking Confirmed chưa check-in đã quá mốc ân hạn (mặc định 30 phút – NO_SHOW_CANCEL_AFTER_MINUTES)
    /// kể từ giờ bắt đầu, cho Staff bấm "Hủy No-show". Nội bộ cho TV7 - GateService.
    /// </summary>
    [HttpGet("no-show-candidates")]
    [ProducesResponseType<IReadOnlyList<NoShowCandidateDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<NoShowCandidateDto>>> NoShowCandidates(
        [FromQuery] int parkingLotId, [FromQuery] int graceMinutes = 30, CancellationToken cancellationToken = default)
        => Ok(await listNoShowCandidates.ExecuteAsync(parkingLotId, graceMinutes, cancellationToken));
}
