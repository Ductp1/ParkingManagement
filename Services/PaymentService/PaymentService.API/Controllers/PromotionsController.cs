using Microsoft.AspNetCore.Mvc;
using PaymentService.Application.Features.Promotions;

namespace PaymentService.API.Controllers;

/// <summary>
/// Controller quản lý danh mục mã khuyến mãi và áp dụng mã giảm giá tại Checkout (US-073).
/// </summary>
[ApiController]
[Route("api/v1/promotions")]
public sealed class PromotionsController(
    ICreatePromotionUseCase createUseCase,
    IApplyPromotionUseCase applyUseCase,
    IGetPromotionsUseCase getUseCase,
    IUpdatePromotionUseCase updateUseCase) : ControllerBase
{
    /// <summary>
    /// POST /api/v1/promotions – Tạo mới một mã khuyến mãi với điều kiện áp dụng và bên tài trợ (US-073 AC1, AC2).
    /// </summary>
    /// <param name="command">{CreatePromotionCommand} - FE hoặc Web Portal truyền qua Request Body - Thông tin mã code, loại giảm giá, thời gian và bên tài trợ.</param>
    /// <param name="cancellationToken">{CancellationToken} - Hệ thống tự động truyền - Token hủy tác vụ bất đồng bộ khi client ngắt kết nối.</param>
    /// <returns>
    /// Đối tượng PromotionDto chứa thông tin mã khuyến mãi vừa tạo.
    /// Mã HTTP trả về:
    /// - 201 Created: Tạo mã khuyến mãi thành công.
    /// - 400 BadRequest: Dữ liệu gửi lên không hợp lệ (mã rỗng, trùng lặp, thời gian kết thúc trước ngày bắt đầu, % giảm quá 100).
    /// </returns>
    [HttpPost]
    [ProducesResponseType<PromotionDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PromotionDto>> Create(
        [FromBody] CreatePromotionCommand command,
        CancellationToken cancellationToken)
    {
        var result = await createUseCase.ExecuteAsync(command, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    /// <summary>
    /// POST /api/v1/promotions/apply – Kiểm tra tính hợp lệ và áp dụng mã khuyến mãi tại bước Checkout (US-073 AC3).
    /// </summary>
    /// <param name="query">{ApplyPromotionQuery} - FE truyền qua Request Body - Mã code, mã bãi xe và tổng tiền gốc của đơn đặt chỗ.</param>
    /// <param name="cancellationToken">{CancellationToken} - Hệ thống tự động truyền - Token hủy tác vụ bất đồng bộ khi client ngắt kết nối.</param>
    /// <returns>
    /// Đối tượng AppliedPromotionDto chứa số tiền giảm giá, số tiền thực trả và bên tài trợ.
    /// Mã HTTP trả về:
    /// - 200 OK: Áp dụng mã thành công, hiển thị số tiền được giảm.
    /// - 400 BadRequest: Mã không hợp lệ (hết hạn, hết lượt dùng, sai bãi xe, chưa đạt giá trị đơn tối thiểu).
    /// - 404 NotFound: Mã khuyến mãi không tồn tại trong hệ thống.
    /// </returns>
    [HttpPost("apply")]
    [ProducesResponseType<AppliedPromotionDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AppliedPromotionDto>> Apply(
        [FromBody] ApplyPromotionQuery query,
        CancellationToken cancellationToken)
    {
        var result = await applyUseCase.ExecuteAsync(query, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// GET /api/v1/promotions – Tra cứu danh sách mã khuyến mãi theo bãi xe hoặc toàn sàn (US-073).
    /// </summary>
    /// <param name="parkingLotId">{int?} - FE truyền qua Query - Mã định danh của bãi đỗ xe (để trống nếu muốn lấy toàn sàn).</param>
    /// <param name="isActiveOnly">{bool?} - FE truyền qua Query - Lọc chỉ lấy các mã đang hoạt động hay lấy toàn bộ.</param>
    /// <param name="cancellationToken">{CancellationToken} - Hệ thống tự động truyền - Token hủy tác vụ bất đồng bộ khi client ngắt kết nối.</param>
    /// <returns>
    /// Danh sách các đối tượng PromotionDto thỏa mãn điều kiện lọc.
    /// Mã HTTP trả về:
    /// - 200 OK: Lấy danh sách thành công.
    /// - 400 BadRequest: ParkingLotId không hợp lệ (nhỏ hơn hoặc bằng 0).
    /// </returns>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<PromotionDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<PromotionDto>>> List(
        [FromQuery] int? parkingLotId,
        [FromQuery] bool? isActiveOnly,
        CancellationToken cancellationToken)
    {
        var result = await getUseCase.ListAsync(parkingLotId, isActiveOnly, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// GET /api/v1/promotions/{id} – Xem thông tin chi tiết một mã khuyến mãi theo ID (US-073).
    /// </summary>
    /// <param name="id">{int} - FE truyền qua Route - Mã định danh duy nhất của khuyến mãi.</param>
    /// <param name="cancellationToken">{CancellationToken} - Hệ thống tự động truyền - Token hủy tác vụ bất đồng bộ khi client ngắt kết nối.</param>
    /// <returns>
    /// Đối tượng PromotionDto chứa cấu hình chi tiết của khuyến mãi.
    /// Mã HTTP trả về:
    /// - 200 OK: Tìm thấy thông tin khuyến mãi.
    /// - 404 NotFound: Không tìm thấy khuyến mãi với ID tương ứng.
    /// </returns>
    [HttpGet("{id:int}")]
    [ProducesResponseType<PromotionDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PromotionDto>> GetById(
        [FromRoute] int id,
        CancellationToken cancellationToken)
    {
        var result = await getUseCase.GetByIdAsync(id, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// PUT /api/v1/promotions/{id} – Cập nhật cấu hình hoặc bật/tắt trạng thái hoạt động của mã khuyến mãi (US-073).
    /// </summary>
    /// <param name="id">{int} - FE truyền qua Route - Mã định danh duy nhất của khuyến mãi cần sửa.</param>
    /// <param name="command">{UpdatePromotionCommand} - FE truyền qua Request Body - Dữ liệu cập nhật tên, thời hạn, giới hạn lượt dùng và cờ IsActive.</param>
    /// <param name="cancellationToken">{CancellationToken} - Hệ thống tự động truyền - Token hủy tác vụ bất đồng bộ khi client ngắt kết nối.</param>
    /// <returns>
    /// Đối tượng PromotionDto sau khi đã cập nhật thành công.
    /// Mã HTTP trả về:
    /// - 200 OK: Cập nhật thành công.
    /// - 400 BadRequest: Dữ liệu cập nhật không hợp lệ (thời gian kết thúc trước ngày bắt đầu).
    /// - 404 NotFound: Không tìm thấy khuyến mãi tương ứng.
    /// </returns>
    [HttpPut("{id:int}")]
    [ProducesResponseType<PromotionDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PromotionDto>> Update(
        [FromRoute] int id,
        [FromBody] UpdatePromotionCommand command,
        CancellationToken cancellationToken)
    {
        // Đồng bộ ID từ route vào command nếu có sự sai lệch
        if (id != command.Id)
        {
            command = command with { Id = id };
        }

        var result = await updateUseCase.ExecuteAsync(command, cancellationToken);
        return Ok(result);
    }
}

