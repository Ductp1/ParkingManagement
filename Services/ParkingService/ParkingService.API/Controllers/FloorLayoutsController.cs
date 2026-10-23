using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ParkingService.Application.Features.Layouts;

namespace ParkingService.API.Controllers;

/// <summary>
/// Controller quản lý sơ đồ lưới mặt bằng 2D của tầng (T-502, US-059).
/// Phục vụ component LayoutRenderer (SVG) cho Driver, Owner dashboard và Gate console.
/// </summary>
[ApiController]
[Route("api/v1/parking-lots")]
public sealed class FloorLayoutsController(IGetFloorLayoutUseCase getFloorLayout) : ControllerBase
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
}
