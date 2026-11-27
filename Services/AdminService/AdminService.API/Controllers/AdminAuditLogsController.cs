using AdminService.Application.Features.AuditLogs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ParkingManagement.SharedKernel.Contracts;

namespace AdminService.API.Controllers;

/// <summary>
/// UC-45 / US-100: tra cứu audit log toàn hệ thống. Controller chỉ điều hướng HTTP → Use case. Chỉ role Admin.
/// Audit log chỉ đọc: không có endpoint sửa hoặc xóa.
/// </summary>
[ApiController]
[Route("api/v1/admin/audit-logs")]
[Authorize(Policy = "RequireAdmin")]
public sealed class AdminAuditLogsController(IListAuditLogsUseCase listAuditLogs, IGetAuditLogUseCase getAuditLog,
    IGetAuditLogFilterOptionsUseCase getFilterOptions, IExportAuditLogsUseCase exportAuditLogs) : ControllerBase
{
    /// <summary>
    /// GET /api/v1/admin/audit-logs?from=2026-10-01T00:00:00Z&amp;to=2026-10-24T00:00:00Z&amp;sourceService=AdminService&amp;action=Owner.Locked
    /// &amp;entityName=OwnerProfile&amp;entityId=2&amp;userId=1&amp;ownerProfileId=2&amp;q=gian lận&amp;page=1&amp;pageSize=20
    /// Tham số bind riêng từng cái từ query string: "action" trùng route value của MVC nếu bind cả record.
    /// </summary>
    [HttpGet]
    [ProducesResponseType<PagedResult<AuditLogSummaryDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResult<AuditLogSummaryDto>>> List([FromQuery] DateTime? from, [FromQuery] DateTime? to,
        [FromQuery] string? sourceService, [FromQuery(Name = "action")] string? auditAction, [FromQuery] string? entityName,
        [FromQuery] string? entityId, [FromQuery] int? userId, [FromQuery] int? ownerProfileId, [FromQuery] string? q,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default)
        => Ok(await listAuditLogs.ExecuteAsync(
            new AuditLogListQuery(from, to, sourceService, auditAction, entityName, entityId, userId, ownerProfileId, q),
            page, pageSize, cancellationToken));

    /// <summary>GET /api/v1/admin/audit-logs/18240 – giá trị cũ/mới đã che dữ liệu nhạy cảm + danh sách trường thay đổi.</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType<AuditLogDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AuditLogDetailDto>> GetById(int id, CancellationToken cancellationToken)
        => Ok(await getAuditLog.ExecuteAsync(id, cancellationToken));

    /// <summary>GET /api/v1/admin/audit-logs/filters – SourceService / Action / EntityName đang có kèm số dòng (dropdown, facet).</summary>
    [HttpGet("filters")]
    [ProducesResponseType<AuditLogFilterOptionsDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<AuditLogFilterOptionsDto>> FilterOptions(CancellationToken cancellationToken)
        => Ok(await getFilterOptions.ExecuteAsync(cancellationToken));

    /// <summary>GET /api/v1/admin/audit-logs/export?(cùng tham số lọc của danh sách) – file CSV, tối đa 10.000 dòng.</summary>
    [HttpGet("export")]
    [Produces("text/csv", "application/problem+json")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Export([FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] string? sourceService,
        [FromQuery(Name = "action")] string? auditAction, [FromQuery] string? entityName, [FromQuery] string? entityId,
        [FromQuery] int? userId, [FromQuery] int? ownerProfileId, [FromQuery] string? q, CancellationToken cancellationToken)
    {
        var file = await exportAuditLogs.ExecuteAsync(
            new AuditLogListQuery(from, to, sourceService, auditAction, entityName, entityId, userId, ownerProfileId, q), cancellationToken);
        return File(file.Content, AuditLogCsvFile.ContentType, file.FileName);
    }
}
