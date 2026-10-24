using Microsoft.Extensions.Options;
using PaymentService.Application.Interfaces;

namespace PaymentService.Infrastructure.Vnpay;

/// <summary>
/// Lớp triển khai cổng kết nối VNPAY thực tế, lắp ráp tham số và gọi thuật toán băm HMAC-SHA512.
/// </summary>
public sealed class VnpayGateway(IOptions<VnpayOptions> options) : IVnpayGateway
{
    private readonly VnpayOptions _vnpOptions = options.Value;

    /// <summary>
    /// Lắp ráp các tham số theo quy chuẩn VNPAY và tạo link thanh toán có gắn chữ ký số.
    /// </summary>
    public string GeneratePaymentUrl(
        string paymentCode,
        decimal amount,
        string orderInfo,
        string ipAddress,
        DateTime createdAtUtc,
        DateTime expiresAtUtc)
    {
        var vnpay = new VnpayUtils();

        // Nạp thông tin định danh Merchant được cấp trong file cấu hình
        vnpay.AddRequestData("vnp_Version", _vnpOptions.Version);
        vnpay.AddRequestData("vnp_Command", _vnpOptions.Command);
        vnpay.AddRequestData("vnp_TmnCode", _vnpOptions.TmnCode);

        // Quy ước bắt buộc của VNPAY: nhân 100 để loại bỏ dấu thập phân
        var vnpAmount = ((long)(amount * 100)).ToString();
        vnpay.AddRequestData("vnp_Amount", vnpAmount);

        // Cổng VNPAY yêu cầu định dạng ngày giờ yyyyMMddHHmmss theo múi giờ Việt Nam UTC+7
        var vnCreateTime = createdAtUtc.AddHours(7).ToString("yyyyMMddHHmmss");
        vnpay.AddRequestData("vnp_CreateDate", vnCreateTime);

        var vnExpireTime = expiresAtUtc.AddHours(7).ToString("yyyyMMddHHmmss");
        vnpay.AddRequestData("vnp_ExpireDate", vnExpireTime);

        vnpay.AddRequestData("vnp_CurrCode", _vnpOptions.CurrCode);
        var clientIp = string.IsNullOrWhiteSpace(ipAddress) || ipAddress == "::1" || ipAddress == "0.0.0.1" 
            ? "127.0.0.1" 
            : ipAddress;
        vnpay.AddRequestData("vnp_IpAddr", clientIp);
        vnpay.AddRequestData("vnp_Locale", _vnpOptions.Locale);

        // Nội dung hiển thị tại giao diện thanh toán VNPAY
        vnpay.AddRequestData("vnp_OrderInfo", orderInfo);
        vnpay.AddRequestData("vnp_OrderType", "other");

        // Địa chỉ trình duyệt sẽ được VNPAY chuyển hướng về sau khi thanh toán xong
        vnpay.AddRequestData("vnp_ReturnUrl", _vnpOptions.ReturnUrl);

        // Mã tham chiếu giao dịch nội bộ dùng để đối soát ở US-033
        vnpay.AddRequestData("vnp_TxnRef", paymentCode);

        // Thực hiện sắp xếp tham số A-Z và tính mã băm chữ ký bảo mật vnp_SecureHash
        return vnpay.CreateRequestUrl(_vnpOptions.BaseUrl, _vnpOptions.HashSecret);
    }
}
