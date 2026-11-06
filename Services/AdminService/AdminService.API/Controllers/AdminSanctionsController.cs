using AdminService.Application.Features.Sanctions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ParkingManagement.SharedKernel.Contracts;
using ParkingManagement.SharedKernel.Enums;

namespace AdminService.API.Controllers;

/// <summary>US-097 (UC-41): Admin quản lý chế tài SLA 4 cấp. Controller chỉ điều hướng HTTP → Use case. Chỉ role Admin được gọi.</summary>
[ApiController]
[Route("api/v1/admin/sanctions")]
[Authorize(Policy = "RequireAdmin")]
public sealed class AdminSanctionsController(IListSanctionsUseCase listSanctions, IGetSanctionUseCase getSanction,
    IGetSanctionStatsUseCase getStats, IIssueSanctionUseCase issueSanction, IRevokeSanctionUseCase revokeSanction,
    IRetrySanctionSyncUseCase retrySync, ICurrentUser currentUser) : ControllerBase
{
    /// <summary>
    /// GET /api/v1/admin/sanctions?status=Active&amp;level=RankingPenalty&amp;ownerProfileId=2&amp;parkingLotId=4&amp;search=overbooking
    /// &amp;from=2026-11-01T00:00:00Z&amp;to=2026-12-01T00:00:00Z&amp;syncFailed=true&amp;page=1&amp;pageSize=20
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<PagedResult<SanctionSummaryDto>>> List(
        [FromQuery] SanctionStatus? status, [FromQuery] SanctionLevel? level, [FromQuery] int? ownerProfileId,
        [FromQuery] int? parkingLotId, [FromQuery] string? search, [FromQuery] DateTime? from, [FromQuery] DateTime? to,
        [FromQuery] bool? syncFailed, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default)
        => Ok(await listSanctions.ExecuteAsync(
            new SanctionListQuery(status, level, ownerProfileId, parkingLotId, search, from, to, syncFailed), page, pageSize, cancellationToken));

    /// <summary>GET /api/v1/admin/sanctions/stats – KPI: đang hiệu lực theo cấp, đồng bộ lỗi / chờ, sắp hết hạn 7 ngày.</summary>
    [HttpGet("stats")]
    public async Task<ActionResult<SanctionStatsDto>> Stats(CancellationToken cancellationToken)
        => Ok(await getStats.ExecuteAsync(cancellationToken));

    /// <summary>GET /api/v1/admin/sanctions/12 – Chi tiết + trạng thái đồng bộ + timeline audit.</summary>
    [HttpGet("{id:int}")]
    public async Task<ActionResult<SanctionDetailDto>> Get(int id, CancellationToken cancellationToken)
        => Ok(await getSanction.ExecuteAsync(id, cancellationToken));

    /// <summary>POST /api/v1/admin/sanctions – Áp chế tài (1 chế tài toàn chủ bãi hoặc 1 chế tài / bãi). Người áp lấy từ JWT.</summary>
    [HttpPost]
    public async Task<ActionResult<IssueSanctionResultDto>> Issue([FromBody] IssueSanctionRequest request, CancellationToken cancellationToken)
        => currentUser.UserId is { } adminUserId
            ? StatusCode(StatusCodes.Status201Created, await issueSanction.ExecuteAsync(request, adminUserId, cancellationToken))
            : Unauthorized();

    /// <summary>POST /api/v1/admin/sanctions/12/revoke – Gỡ chế tài còn hiệu lực (bắt buộc lý do).</summary>
    [HttpPost("{id:int}/revoke")]
    public async Task<ActionResult<SanctionDetailDto>> Revoke(int id, [FromBody] RevokeSanctionRequest request, CancellationToken cancellationToken)
        => currentUser.UserId is { } adminUserId
            ? Ok(await revokeSanction.ExecuteAsync(id, request, adminUserId, cancellationToken))
            : Unauthorized();

    /// <summary>
    /// POST /api/v1/admin/sanctions/12/retry-sync – gửi lại các lệnh đang chờ / lỗi (UserService, ParkingService, thông báo) bằng JWT
    /// của Admin. Không còn gì để đồng bộ → 409.
    /// </summary>
    [HttpPost("{id:int}/retry-sync")]
    public async Task<ActionResult<SanctionDetailDto>> RetrySync(int id, CancellationToken cancellationToken)
        => currentUser.UserId is { } adminUserId
            ? Ok(await retrySync.ExecuteAsync(id, adminUserId, cancellationToken))
            : Unauthorized();
}
