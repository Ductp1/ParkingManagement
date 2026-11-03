using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ParkingManagement.SharedKernel.Contracts;
using ParkingService.Application.Features.Layouts;

namespace ParkingService.API.Controllers;

/// <summary>
/// Controller quản lý sơ đồ lưới mặt bằng 2D của tầng (T-502, US-059).
/// Phục vụ component LayoutRenderer (SVG) cho Driver, Owner dashboard, Gate console
/// và công cụ Layout Configurator (Draft, Publish, Version History).
/// </summary>
[ApiController]
[Route("api/v1/parking-lots")]
public sealed class FloorLayoutsController(
    IGetFloorLayoutUseCase getFloorLayout,
    IGetFloorLayoutDraftUseCase getFloorLayoutDraft,
    ISaveFloorLayoutDraftUseCase saveFloorLayoutDraft,
    IPublishFloorLayoutUseCase publishFloorLayout,
    IGetFloorLayoutVersionsUseCase getFloorLayoutVersions,
    IGetFloorLayoutByVersionUseCase getFloorLayoutByVersion,
    ICurrentUser currentUser) : ControllerBase
{
    /// <summary>
    /// GET /api/v1/parking-lots/floors/{floorId}/layout
    /// Lấy sơ đồ mặt bằng lưới 2D của tầng phục vụ component LayoutRenderer (SVG) - Task T-502.
    /// Tự động ghép trạng thái realtime của ô đỗ và có cơ chế fallback tổng hợp khi chưa có bản vẽ.
    /// <para>
    /// <b>Security Architecture Decision:</b> Endpoint này được cấu hình <see cref="AllowAnonymousAttribute"/> công khai
    /// theo thiết kế kiến trúc hệ thống (SRS v2 F1.4 &amp; F5.3):
    /// <list type="bullet">
    ///   <item>Cho phép khách vãng lai / tài xế chưa đăng nhập (Guest Driver) có thể duyệt sơ đồ tầng và kiểm tra chỗ trống trực quan trước khi quyết định đặt chỗ hoặc đăng nhập.</item>
    ///   <item>Cho phép Kiosk tại cổng bãi xe (Gate Console) và camera AI có thể tra cứu sơ đồ hiển thị vị trí đỗ xe nhanh chóng.</item>
    ///   <item>Nhất quán với các API đọc danh mục công khai khác: GET /api/v1/parking-lots/{id}, GET /api/v1/parking-lots/search, GET /api/v1/parking-lots/floors/{floorId}/slots.</item>
    ///   <item><i>Lưu ý:</i> Các API thay đổi dữ liệu sơ đồ (US-059: Save Draft, Publish Layout) bắt buộc phải gắn [Authorize] cho Owner/Admin.</item>
    /// </list>
    /// </para>
    /// </summary>
    [HttpGet("floors/{floorId:int}/layout")]
    [AllowAnonymous]
    [ProducesResponseType<FloorLayoutDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FloorLayoutDto>> GetLayout(int floorId, CancellationToken cancellationToken)
    {
        var layout = await getFloorLayout.ExecuteAsync(floorId, cancellationToken);
        return Ok(layout);
    }

    /// <summary>
    /// GET /api/v1/parking-lots/floors/{floorId}/layout/draft
    /// Lấy bản nháp sơ đồ mặt bằng (Draft) để mở trong công cụ Layout Configurator (US-059).
    /// Nếu tầng chưa có bản nháp, tự động tổng hợp từ bản vẽ xuất bản hiện tại hoặc từ danh sách ô đỗ hiện có.
    /// </summary>
    [HttpGet("floors/{floorId:int}/layout/draft")]
    [Authorize]
    [ProducesResponseType<FloorLayoutDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FloorLayoutDto>> GetDraft(int floorId, CancellationToken cancellationToken)
    {
        var draft = await getFloorLayoutDraft.ExecuteAsync(floorId, cancellationToken);
        return Ok(draft);
    }

    /// <summary>
    /// POST /api/v1/parking-lots/floors/{floorId}/layout/draft
    /// Lưu bản nháp sơ đồ mặt bằng vào CSDL mà không làm thay đổi trạng thái và dữ liệu ô đỗ vật lý (US-059).
    /// </summary>
    [HttpPost("floors/{floorId:int}/layout/draft")]
    [Authorize]
    [ProducesResponseType<FloorLayoutDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FloorLayoutDto>> SaveDraft(
        int floorId,
        [FromBody] SaveFloorLayoutDraftRequest request,
        CancellationToken cancellationToken)
    {
        var command = new SaveFloorLayoutDraftCommand(
            floorId,
            request.Columns,
            request.Rows,
            request.Cells,
            request.ChangeNote,
            currentUser.UserId ?? 0);

        var result = await saveFloorLayoutDraft.ExecuteAsync(command, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// POST /api/v1/parking-lots/floors/{floorId}/layout/publish
    /// Xuất bản phiên bản sơ đồ mới và tự động kích hoạt engine đồng bộ hóa ô đỗ vật lý (Slots) (US-059).
    /// Chặn và báo lỗi 409 Conflict nếu có ô đỗ đang có xe (Occupied) hoặc giữ chỗ (Reserved) bị xóa khỏi bản vẽ.
    /// </summary>
    [HttpPost("floors/{floorId:int}/layout/publish")]
    [Authorize]
    [ProducesResponseType<FloorLayoutDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<FloorLayoutDto>> Publish(
        int floorId,
        [FromBody] PublishFloorLayoutRequest request,
        CancellationToken cancellationToken)
    {
        var command = new PublishFloorLayoutCommand(
            floorId,
            request.Columns,
            request.Rows,
            request.Cells,
            request.ChangeNote,
            currentUser.UserId ?? 0);

        var result = await publishFloorLayout.ExecuteAsync(command, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// GET /api/v1/parking-lots/floors/{floorId}/layout/versions
    /// Lấy danh sách lịch sử các phiên bản sơ đồ mặt bằng của tầng (US-059).
    /// </summary>
    [HttpGet("floors/{floorId:int}/layout/versions")]
    [Authorize]
    [ProducesResponseType<IReadOnlyList<LayoutVersionSummaryDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<LayoutVersionSummaryDto>>> GetVersions(
        int floorId,
        CancellationToken cancellationToken)
    {
        var versions = await getFloorLayoutVersions.ExecuteAsync(floorId, cancellationToken);
        return Ok(versions);
    }

    /// <summary>
    /// GET /api/v1/parking-lots/floors/{floorId}/layout/versions/{versionNo:int}
    /// Lấy chi tiết sơ đồ của một phiên bản lịch sử cụ thể theo VersionNo (US-059).
    /// </summary>
    [HttpGet("floors/{floorId:int}/layout/versions/{versionNo:int}")]
    [Authorize]
    [ProducesResponseType<FloorLayoutDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FloorLayoutDetailDto>> GetVersionByNo(
        int floorId,
        int versionNo,
        CancellationToken cancellationToken)
    {
        var layout = await getFloorLayoutByVersion.ExecuteAsync(floorId, versionNo, cancellationToken);
        return Ok(layout);
    }
}
