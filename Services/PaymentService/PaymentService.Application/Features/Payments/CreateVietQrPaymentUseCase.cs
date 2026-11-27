using ParkingManagement.SharedKernel.Enums;
using ParkingManagement.SharedKernel.Exceptions;
using PaymentService.Application.Interfaces;
using PaymentService.Domain.Entities;

namespace PaymentService.Application.Features.Payments;

/// <summary>
/// Bộ công cụ sinh đường dẫn hình ảnh mã QR động theo chuẩn VietQR NAPAS247 (Task T-402).
/// </summary>
public static class VietQrGenerator
{
    /// <summary>
    /// Tạo đường link hình ảnh mã QR động theo chuẩn VietQR QuickLink để hiển thị trực tiếp lên trình duyệt hoặc màn hình Kiosk cổng.
    /// </summary>
    /// <param name="bankBin">Mã định danh ngân hàng (BIN 6 số do Ngân hàng Nhà nước cấp, ví dụ: 970422 là MBBank, 970436 là Vietcombank).</param>
    /// <param name="accountNumber">Số tài khoản ngân hàng của chủ bãi xe nhận tiền.</param>
    /// <param name="amount">Số tiền thanh toán cần điền sẵn vào mã QR.</param>
    /// <param name="transferContent">Nội dung chuyển khoản hiển thị trên ứng dụng ngân hàng (mã lượt gửi xe).</param>
    /// <param name="accountName">Tên chủ tài khoản ngân hàng viết hoa không dấu.</param>
    /// <returns>Đường dẫn URL chứa hình ảnh mã QR chất lượng cao.</returns>
    public static string GenerateQuickLink(
        string bankBin,
        string accountNumber,
        decimal amount,
        string transferContent,
        string accountName)
    {
        // Loại bỏ mọi ký tự khoảng trắng thừa trong số tài khoản và mã ngân hàng
        var cleanBin = bankBin.Trim();
        var cleanAccountNo = accountNumber.Trim();

        // Mã hóa nội dung chuyển khoản và tên chủ tài khoản để tránh lỗi ký tự đặc biệt trên URL
        var encodedContent = Uri.EscapeDataString(transferContent.Trim());
        var encodedName = Uri.EscapeDataString(accountName.Trim().ToUpperInvariant());

        // Định dạng số tiền thành chuỗi số nguyên (không có phần thập phân vì VNĐ không dùng số lẻ)
        var amountInt = Convert.ToInt64(Math.Round(amount, MidpointRounding.AwayFromZero));

        // Trả về đường link ảnh QR theo chuẩn giao diện gọn compact2 của dịch vụ VietQR quốc gia
        return $"https://img.vietqr.io/image/{cleanBin}-{cleanAccountNo}-compact2.png?amount={amountInt}&addInfo={encodedContent}&accountName={encodedName}";
    }
}

/// <summary>
/// Dữ liệu đầu vào yêu cầu tạo mã thanh toán VietQR động (Task T-402).
/// </summary>
/// <param name="BookingId">Mã định danh lượt đặt chỗ (nếu là khách đặt trước).</param>
/// <param name="BookingCode">Mã hiển thị lượt đặt chỗ (ví dụ: BK-0002).</param>
/// <param name="ParkingSessionId">Mã định danh phiên đỗ xe thực tế tại cổng (nếu là khách vãng lai hoặc check-out tại cổng).</param>
/// <param name="ParkingLotId">Mã bãi đỗ xe.</param>
/// <param name="OwnerProfileId">Mã chủ bãi xe thụ hưởng tiền.</param>
/// <param name="UserId">Mã tài xế (null nếu là khách vãng lai không có tài khoản).</param>
/// <param name="Amount">Số tiền thực tế cần thanh toán (VNĐ).</param>
/// <param name="BankBin">Mã BIN ngân hàng nhận tiền (6 chữ số chuẩn NAPAS).</param>
/// <param name="BankAccountNumber">Số tài khoản ngân hàng của chủ bãi xe.</param>
/// <param name="BankAccountName">Tên chủ tài khoản ngân hàng.</param>
public sealed record CreateVietQrPaymentCommand(
    int? BookingId,
    string? BookingCode,
    int? ParkingSessionId,
    int ParkingLotId,
    int OwnerProfileId,
    int? UserId,
    decimal Amount,
    string BankBin,
    string BankAccountNumber,
    string BankAccountName);

/// <summary>
/// Kết quả trả về sau khi khởi tạo thành công mã thanh toán VietQR động (Task T-402).
/// </summary>
/// <param name="PaymentCode">Mã giao dịch nội bộ duy nhất được tạo trong hệ thống.</param>
/// <param name="QrImageUrl">Đường dẫn hình ảnh mã QR động chuẩn QuickLink để hiển thị lên Kiosk hoặc Web.</param>
/// <param name="BankBin">Mã BIN ngân hàng nhận tiền.</param>
/// <param name="BankAccountNumber">Số tài khoản ngân hàng của chủ bãi xe.</param>
/// <param name="BankAccountName">Tên chủ tài khoản ngân hàng.</param>
/// <param name="Amount">Số tiền cần thanh toán (VNĐ).</param>
/// <param name="TransferContent">Nội dung chuyển khoản tự động điền sẵn trên ứng dụng ngân hàng.</param>
/// <param name="ExpiresAtUtc">Thời điểm hết hạn của phiên thanh toán (sau 15 phút).</param>
public sealed record VietQrPaymentDto(
    string PaymentCode,
    string QrImageUrl,
    string BankBin,
    string BankAccountNumber,
    string BankAccountName,
    decimal Amount,
    string TransferContent,
    DateTime ExpiresAtUtc);

/// <summary>
/// Hợp đồng giao diện thực thi nghiệp vụ tạo thanh toán VietQR động (Task T-402).
/// </summary>
public interface ICreateVietQrPaymentUseCase
{
    /// <summary>
    /// Thực hiện quy trình nghiệp vụ tạo mã thanh toán VietQR động.
    /// </summary>
    Task<VietQrPaymentDto> ExecuteAsync(CreateVietQrPaymentCommand command, CancellationToken cancellationToken = default);
}

/// <summary>
/// Lớp cài đặt quy trình nghiệp vụ tạo mã thanh toán VietQR động chuẩn NAPAS247 (Task T-402).
/// </summary>
public sealed class CreateVietQrPaymentUseCase(IPaymentRepository paymentRepository) : ICreateVietQrPaymentUseCase
{
    public async Task<VietQrPaymentDto> ExecuteAsync(CreateVietQrPaymentCommand command, CancellationToken cancellationToken = default)
    {
        // Chặn sớm dữ liệu số tiền không hợp lệ (số tiền phải lớn hơn 0 đồng)
        if (command.Amount <= 0)
        {
            throw new ValidationException("Số tiền thanh toán phải lớn hơn 0 đồng.");
        }

        // Chặn thiếu mã ngân hàng (BankBin) của chủ bãi xe
        if (string.IsNullOrWhiteSpace(command.BankBin))
        {
            throw new ValidationException("Mã ngân hàng (BankBin) của chủ bãi không được để trống.");
        }

        // Chặn thiếu số tài khoản ngân hàng nhận tiền
        if (string.IsNullOrWhiteSpace(command.BankAccountNumber))
        {
            throw new ValidationException("Số tài khoản ngân hàng của chủ bãi không được để trống.");
        }

        // Chặn thiếu tên chủ tài khoản nhận tiền
        if (string.IsNullOrWhiteSpace(command.BankAccountName))
        {
            throw new ValidationException("Tên chủ tài khoản ngân hàng không được để trống.");
        }

        // Đảm bảo giao dịch phải gắn liền với một lượt đặt chỗ trước (BookingId) hoặc một lượt đỗ xe tại cổng (ParkingSessionId)
        if ((!command.BookingId.HasValue || command.BookingId.Value <= 0) &&
            (!command.ParkingSessionId.HasValue || command.ParkingSessionId.Value <= 0))
        {
            throw new ValidationException("Giao dịch VietQR phải gắn với BookingId hoặc ParkingSessionId.");
        }

        // Tạo mã định danh duy nhất trong ngày theo định dạng PAY_yyyyMMddHHmmss_{guid6}
        var paymentCode = $"PAY_{DateTime.UtcNow:yyyyMMddHHmmss}_{Guid.NewGuid().ToString("N")[..6]}";

        // Xác định nội dung chuyển khoản: ưu tiên dùng mã đặt chỗ BookingCode, nếu không có thì dùng mã lượt đỗ SES_{id}
        var transferContent = !string.IsNullOrWhiteSpace(command.BookingCode)
            ? command.BookingCode
            : $"SES_{command.ParkingSessionId}";

        // Tính toán đường dẫn ảnh QR chuẩn VietQR NAPAS247 QuickLink
        var qrImageUrl = VietQrGenerator.GenerateQuickLink(
            command.BankBin,
            command.BankAccountNumber,
            command.Amount,
            transferContent,
            command.BankAccountName);

        // Thiết lập thời hạn thanh toán là 15 phút (khớp với thời hạn giữ chỗ của hệ thống)
        var expiresAtUtc = DateTime.UtcNow.AddMinutes(15);

        // Khởi tạo thực thể Payment để lưu trữ vào Cơ sở dữ liệu
        var payment = new Payment
        {
            Code = paymentCode,
            BookingId = command.BookingId,
            BookingCode = command.BookingCode,
            ParkingSessionId = command.ParkingSessionId,
            ParkingLotId = command.ParkingLotId,
            OwnerProfileId = command.OwnerProfileId,
            UserId = command.UserId,
            Purpose = command.ParkingSessionId.HasValue ? PaymentPurpose.WalkIn : PaymentPurpose.Booking,
            Method = PaymentMethod.VietQr,
            Status = PaymentStatus.Pending,
            Amount = command.Amount,
            Currency = "VND",
            IdempotencyKey = $"VIETQR_{paymentCode}",
            TransferContent = transferContent,
            ExpiresAtUtc = expiresAtUtc,
            RetryCount = 0
        };

        // Lưu vào cơ sở dữ liệu thông qua Repository
        await paymentRepository.AddAsync(payment, cancellationToken);

        // Trả về DTO kết quả hoàn chỉnh cho Controller
        return new VietQrPaymentDto(
            PaymentCode: paymentCode,
            QrImageUrl: qrImageUrl,
            BankBin: command.BankBin.Trim(),
            BankAccountNumber: command.BankAccountNumber.Trim(),
            BankAccountName: command.BankAccountName.Trim().ToUpperInvariant(),
            Amount: command.Amount,
            TransferContent: transferContent,
            ExpiresAtUtc: expiresAtUtc);
    }
}
