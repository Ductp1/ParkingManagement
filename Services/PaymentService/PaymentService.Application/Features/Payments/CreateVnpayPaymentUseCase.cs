using ParkingManagement.SharedKernel.Enums;
using ParkingManagement.SharedKernel.Exceptions;
using PaymentService.Application.Interfaces;
using PaymentService.Domain.Entities;

namespace PaymentService.Application.Features.Payments;

/// <summary>
/// Lệnh yêu cầu khởi tạo giao dịch thanh toán và sinh URL cổng VNPAY Sandbox (US-032).
/// </summary>
/// <param name="BookingId">Mã định danh duy nhất của lượt đặt chỗ trong hệ thống.</param>
/// <param name="BookingCode">Mã hiển thị của lượt đặt chỗ (ví dụ: BK-0002).</param>
/// <param name="ParkingLotId">Mã định danh của bãi đỗ xe diễn ra lượt gửi.</param>
/// <param name="OwnerProfileId">Mã định danh chủ bãi xe thụ hưởng số tiền này.</param>
/// <param name="UserId">Mã định danh tài xế đặt chỗ (null nếu là khách vãng lai).</param>
/// <param name="Amount">Số tiền thực tế cần thanh toán (VNĐ).</param>
/// <param name="IpAddress">Địa chỉ IP của thiết bị khách hàng thực hiện giao dịch.</param>
public sealed record CreateVnpayPaymentCommand(
    int BookingId,
    string BookingCode,
    int ParkingLotId,
    int OwnerProfileId,
    int? UserId,
    decimal Amount,
    string IpAddress);

/// <summary>
/// Dữ liệu trả về cho Client sau khi khởi tạo giao dịch thanh toán VNPAY thành công (US-032).
/// </summary>
/// <param name="PaymentCode">Mã tham chiếu giao dịch nội bộ duy nhất được tạo trong hệ thống (vnp_TxnRef).</param>
/// <param name="PaymentUrl">Đường dẫn chuyển hướng thanh toán VNPAY chứa đầy đủ chữ ký số HMAC-SHA512.</param>
/// <param name="ExpiresAtUtc">Thời điểm hết hạn hiệu lực của phiên thanh toán (chuẩn UTC, 15 phút).</param>
public sealed record VnpayPaymentUrlDto(
    string PaymentCode,
    string PaymentUrl,
    DateTime ExpiresAtUtc);

/// <summary>
/// Giao diện Use Case: Khởi tạo giao dịch thanh toán online qua VNPAY Sandbox (US-032).
/// </summary>
public interface ICreateVnpayPaymentUseCase
{
    /// <summary>
    /// Thực thi quy trình kiểm tra dữ liệu, lưu bản ghi Pending và sinh URL VNPAY.
    /// </summary>
    /// <param name="command">Lệnh khởi tạo thanh toán chứa thông tin đơn và số tiền.</param>
    /// <param name="cancellationToken">Token hủy tác vụ bất đồng bộ.</param>
    /// <returns>Thông tin URL thanh toán và mã giao dịch duy nhất.</returns>
    Task<VnpayPaymentUrlDto> ExecuteAsync(CreateVnpayPaymentCommand command, CancellationToken cancellationToken = default);
}

/// <summary>
/// Lớp thực thi Use Case khởi tạo giao dịch thanh toán online qua VNPAY Sandbox (US-032).
/// </summary>
public sealed class CreateVnpayPaymentUseCase(
    IPaymentRepository paymentRepository,
    IVnpayGateway vnpayGateway) : ICreateVnpayPaymentUseCase
{
    /// <summary>
    /// Thực hiện quy trình nghiệp vụ tạo thanh toán VNPAY.
    /// </summary>
    public async Task<VnpayPaymentUrlDto> ExecuteAsync(CreateVnpayPaymentCommand command, CancellationToken cancellationToken = default)
    {
        // Chặn sớm dữ liệu âm hoặc bằng 0 trước khi chạm vào cơ sở dữ liệu
        if (command.Amount <= 0)
        {
            throw new ValidationException("Số tiền thanh toán phải lớn hơn 0 đồng.");
        }

        // Đảm bảo thanh toán luôn gắn liền với một lượt đặt chỗ cụ thể
        if (command.BookingId <= 0)
        {
            throw new ValidationException("Mã đặt chỗ (bookingId) không hợp lệ.");
        }

        // Tạo mã định danh duy nhất trong ngày theo chuẩn độ dài của VNPAY
        var paymentCode = $"PAY_{DateTime.UtcNow:yyyyMMddHHmmss}_{Guid.NewGuid().ToString("N")[..6]}";

        var createdAtUtc = DateTime.UtcNow;
        // Đồng bộ thời gian hết hạn của phiên thanh toán với thời gian giữ chỗ 15 phút của hệ thống
        var expiresAtUtc = createdAtUtc.AddMinutes(15);

        // Khởi tạo thực thể Payment với trạng thái ban đầu là Pending
        var payment = new Payment
        {
            Code = paymentCode,
            BookingId = command.BookingId,
            BookingCode = command.BookingCode,
            ParkingLotId = command.ParkingLotId,
            OwnerProfileId = command.OwnerProfileId,
            UserId = command.UserId,
            Purpose = PaymentPurpose.Booking,
            Method = PaymentMethod.Vnpay,
            Status = PaymentStatus.Pending,
            Amount = command.Amount,
            RefundedAmount = 0,
            Currency = "VND",
            // Khóa chống xử lý trùng lặp khi callback gọi nhiều lần
            IdempotencyKey = Guid.NewGuid().ToString("N"),
            ExpiresAtUtc = expiresAtUtc,
            RetryCount = 0
        };

        // Lưu đơn hàng vào cơ sở dữ liệu trước để có dữ liệu đối soát khi VNPAY gọi IPN báo kết quả ở US-033
        await paymentRepository.AddAsync(payment, cancellationToken);

        var orderDescription = $"Thanh toan don dat cho {command.BookingCode}";

        // Tách biệt việc băm mã hóa và lắp ráp URL cho tầng hạ tầng phụ trách
        var paymentUrl = vnpayGateway.GeneratePaymentUrl(
            paymentCode,
            command.Amount,
            orderDescription,
            command.IpAddress,
            createdAtUtc,
            expiresAtUtc);

        return new VnpayPaymentUrlDto(paymentCode, paymentUrl, expiresAtUtc);
    }
}
