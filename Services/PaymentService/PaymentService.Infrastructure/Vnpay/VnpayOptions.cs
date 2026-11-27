namespace PaymentService.Infrastructure.Vnpay;

/// <summary>
/// Lớp đại diện cho các thông số cấu hình VNPAY Sandbox được đọc từ file appsettings.json.
/// </summary>
public class VnpayOptions
{
    /// <summary>
    /// Tên section trong file cấu hình JSON.
    /// </summary>
    public const string SectionName = "Vnpay";

    /// <summary>Mã Website do VNPAY cấp (vnp_TmnCode).</summary>
    public string TmnCode { get; set; } = string.Empty;

    /// <summary>Chuỗi secret key dùng để ký HMAC-SHA512 (vnp_HashSecret).</summary>
    public string HashSecret { get; set; } = string.Empty;

    /// <summary>URL cổng thanh toán VNPAY TEST.</summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>Phiên bản API của VNPAY (Mặc định 2.1.0).</summary>
    public string Version { get; set; } = "2.1.0";

    /// <summary>Lệnh giao dịch (Mặc định pay).</summary>
    public string Command { get; set; } = "pay";

    /// <summary>Đơn vị tiền tệ (Mặc định VND).</summary>
    public string CurrCode { get; set; } = "VND";

    /// <summary>Ngôn ngữ giao diện (Mặc định vn).</summary>
    public string Locale { get; set; } = "vn";

    /// <summary>Địa chỉ URL mà VNPAY sẽ chuyển hướng browser về sau khi thanh toán thành công hoặc thất bại.</summary>
    public string ReturnUrl { get; set; } = string.Empty;
}
