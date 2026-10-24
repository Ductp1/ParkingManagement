namespace PaymentService.Application.Interfaces;

/// <summary>
/// Cổng kết nối với dịch vụ thanh toán bên ngoài VNPAY.
/// </summary>
public interface IVnpayGateway
{
    /// <summary>
    /// Sinh đường dẫn thanh toán VNPAY Sandbox có gắn chữ ký số bảo mật HMAC-SHA512.
    /// </summary>
    /// <param name="paymentCode">Mã giao dịch nội bộ duy nhất (vnp_TxnRef).</param>
    /// <param name="amount">Số tiền cần thanh toán theo đơn vị VNĐ.</param>
    /// <param name="orderInfo">Nội dung mô tả đơn hàng thanh toán hiển thị trên VNPAY.</param>
    /// <param name="ipAddress">Địa chỉ IP mạng của thiết bị khách hàng.</param>
    /// <param name="createdAtUtc">Thời điểm khởi tạo giao dịch.</param>
    /// <param name="expiresAtUtc">Thời điểm hết hạn giao dịch (15 phút).</param>
    /// <returns>Chuỗi URL thanh toán hoàn chỉnh chuyển hướng sang VNPAY.</returns>
    string GeneratePaymentUrl(
        string paymentCode,
        decimal amount,
        string orderInfo,
        string ipAddress,
        DateTime createdAtUtc,
        DateTime expiresAtUtc);
}
