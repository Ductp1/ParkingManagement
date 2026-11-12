using Microsoft.AspNetCore.Mvc;
using PaymentService.Application.Features.Holidays;

namespace PaymentService.API.Controllers;

/// <summary>
/// Controller quản lý danh mục ngày nghỉ lễ phục vụ tính hệ số phụ thu giá đỗ xe (US-071).
/// </summary>
[ApiController]
[Route("api/v1/holidays")]
public sealed class HolidaysController(
    ICreateHolidayUseCase createUseCase,
    IManageHolidaysUseCase manageUseCase) : ControllerBase
{
    /// <summary>
    /// GET /api/v1/holidays – Xem danh sách toàn bộ ngày nghỉ lễ, có thể lọc theo năm (US-071).
    /// </summary>
    /// <param name="year">{int?} - FE truyền qua Query - Năm cần tra cứu (để trống nếu muốn lấy tất cả).</param>
    /// <param name="cancellationToken">{CancellationToken} - Hệ thống tự động truyền - Token hủy tác vụ bất đồng bộ khi client ngắt kết nối.</param>
    /// <returns>
    /// Danh sách các đối tượng HolidayDto chứa ngày, tên dịp lễ và thành phố áp dụng.
    /// Mã HTTP trả về:
    /// - 200 OK: Lấy danh sách thành công.
    /// - 400 BadRequest: Năm tra cứu không hợp lệ.
    /// </returns>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<HolidayDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<HolidayDto>>> GetAll(
        [FromQuery] int? year,
        CancellationToken cancellationToken)
    {
        var result = await manageUseCase.GetAllAsync(year, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// POST /api/v1/holidays – Thêm một ngày nghỉ lễ mới vào hệ thống (US-071).
    /// </summary>
    /// <param name="command">{CreateHolidayCommand} - Admin truyền qua Request Body - Thông tin ngày lễ gồm ngày (Date), tên (Name), và thành phố nếu có (City).</param>
    /// <param name="cancellationToken">{CancellationToken} - Hệ thống tự động truyền - Token hủy tác vụ bất đồng bộ khi client ngắt kết nối.</param>
    /// <returns>
    /// Đối tượng HolidayDto vừa được thêm mới thành công.
    /// Mã HTTP trả về:
    /// - 201 Created: Thêm ngày lễ thành công.
    /// - 400 BadRequest: Dữ liệu gửi lên không hợp lệ hoặc ngày lễ đã tồn tại.
    /// </returns>
    [HttpPost]
    [ProducesResponseType<HolidayDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<HolidayDto>> Create(
        [FromBody] CreateHolidayCommand command,
        CancellationToken cancellationToken)
    {
        var result = await createUseCase.ExecuteAsync(command, cancellationToken);
        return CreatedAtAction(nameof(GetAll), new { year = result.Date.Year }, result);
    }

    /// <summary>
    /// DELETE /api/v1/holidays/{id} – Xóa một ngày nghỉ lễ khỏi hệ thống (US-071).
    /// </summary>
    /// <param name="id">{int} - FE truyền qua Route - Mã định danh duy nhất của ngày lễ cần xóa.</param>
    /// <param name="cancellationToken">{CancellationToken} - Hệ thống tự động truyền - Token hủy tác vụ bất đồng bộ khi client ngắt kết nối.</param>
    /// <returns>
    /// Không có nội dung trả về khi xóa thành công.
    /// Mã HTTP trả về:
    /// - 204 NoContent: Xóa thành công.
    /// - 400 BadRequest: ID không hợp lệ.
    /// - 404 NotFound: Không tìm thấy ngày lễ tương ứng.
    /// </returns>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(
        [FromRoute] int id,
        CancellationToken cancellationToken)
    {
        await manageUseCase.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}

