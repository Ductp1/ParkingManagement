using Microsoft.AspNetCore.Mvc;
using PaymentService.Application.Features.RateCards;

namespace PaymentService.API.Controllers;

/// <summary>
/// Controller quản lý cấu hình biểu giá đỗ xe, phụ thu lễ, cuối tuần, qua đêm và phạt quá giờ (US-070, US-071).
/// </summary>
[ApiController]
[Route("api/v1/rate-cards")]
public sealed class RateCardsController(
    ICreateRateCardUseCase createUseCase,
    IGetRateCardsByLotUseCase getUseCase,
    IUpdateRateCardUseCase updateUseCase) : ControllerBase
{
    /// <summary>
    /// POST /api/v1/rate-cards – Thiết lập và tạo mới bảng giá lũy tiến kèm các mức phụ thu (US-070, US-071).
    /// </summary>
    /// <param name="command">{CreateRateCardCommand} - FE hoặc Web Portal truyền qua Request Body - Thông tin bảng giá, các mức phụ thu và danh sách các block giá.</param>
    /// <param name="cancellationToken">{CancellationToken} - Hệ thống tự động truyền - Token hủy tác vụ bất đồng bộ khi client ngắt kết nối.</param>
    /// <returns>
    /// Đối tượng RateCardDetailDto chứa toàn bộ thông tin bảng giá vừa tạo.
    /// Mã HTTP trả về:
    /// - 201 Created: Tạo bảng giá mới thành công.
    /// - 400 BadRequest: Dữ liệu gửi lên không hợp lệ (hệ số nhỏ hơn 1.0, thiếu block giá, giá âm).
    /// </returns>
    [HttpPost]
    [ProducesResponseType<RateCardDetailDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<RateCardDetailDto>> Create(
        [FromBody] CreateRateCardCommand command,
        CancellationToken cancellationToken)
    {
        var result = await createUseCase.ExecuteAsync(command, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    /// <summary>
    /// GET /api/v1/rate-cards – Tra cứu danh sách toàn bộ bảng giá của một bãi xe (US-070).
    /// </summary>
    /// <param name="parkingLotId">{int} - FE truyền qua Query - Mã định danh của bãi đỗ xe.</param>
    /// <param name="cancellationToken">{CancellationToken} - Hệ thống tự động truyền - Token hủy tác vụ bất đồng bộ khi client ngắt kết nối.</param>
    /// <returns>
    /// Danh sách các đối tượng RateCardDetailDto thuộc bãi xe đó.
    /// Mã HTTP trả về:
    /// - 200 OK: Lấy danh sách thành công.
    /// - 400 BadRequest: ParkingLotId không hợp lệ (nhỏ hơn hoặc bằng 0).
    /// </returns>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<RateCardDetailDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<RateCardDetailDto>>> GetByParkingLot(
        [FromQuery] int parkingLotId,
        CancellationToken cancellationToken)
    {
        var result = await getUseCase.ExecuteAsync(parkingLotId, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// GET /api/v1/rate-cards/{id} – Xem thông tin chi tiết một bảng giá cụ thể theo ID (US-070, US-071).
    /// </summary>
    /// <param name="id">{int} - FE truyền qua Route - Mã định danh duy nhất của bảng giá.</param>
    /// <param name="cancellationToken">{CancellationToken} - Hệ thống tự động truyền - Token hủy tác vụ bất đồng bộ khi client ngắt kết nối.</param>
    /// <returns>
    /// Đối tượng RateCardDetailDto chứa cấu hình phụ thu và danh sách các block giá.
    /// Mã HTTP trả về:
    /// - 200 OK: Tìm thấy bảng giá.
    /// - 404 NotFound: Không tìm thấy bảng giá với ID tương ứng.
    /// </returns>
    [HttpGet("{id:int}")]
    [ProducesResponseType<RateCardDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RateCardDetailDto>> GetById(
        [FromRoute] int id,
        CancellationToken cancellationToken)
    {
        var result = await getUseCase.GetByIdAsync(id, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// PUT /api/v1/rate-cards/{id} – Cập nhật cấu hình phụ thu hoặc bật/tắt hiệu lực của bảng giá (US-071, US-072).
    /// </summary>
    /// <param name="id">{int} - FE truyền qua Route - Mã định danh duy nhất của bảng giá cần cập nhật.</param>
    /// <param name="command">{UpdateRateCardCommand} - FE truyền qua Request Body - Dữ liệu cập nhật mức phụ thu, tên và trạng thái hoạt động.</param>
    /// <param name="cancellationToken">{CancellationToken} - Hệ thống tự động truyền - Token hủy tác vụ bất đồng bộ khi client ngắt kết nối.</param>
    /// <returns>
    /// Đối tượng RateCardDetailDto sau khi đã cập nhật thành công.
    /// Mã HTTP trả về:
    /// - 200 OK: Cập nhật thành công.
    /// - 400 BadRequest: Dữ liệu cập nhật không hợp lệ (hệ số nhỏ hơn 1.0, ID trên Route không khớp với Body).
    /// - 404 NotFound: Không tìm thấy bảng giá tương ứng.
    /// </returns>
    [HttpPut("{id:int}")]
    [ProducesResponseType<RateCardDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RateCardDetailDto>> Update(
        [FromRoute] int id,
        [FromBody] UpdateRateCardCommand command,
        CancellationToken cancellationToken)
    {
        // Đảm bảo Id từ route khớp với command
        if (id != command.Id)
        {
            command = command with { Id = id };
        }

        var result = await updateUseCase.ExecuteAsync(command, cancellationToken);
        return Ok(result);
    }
}

