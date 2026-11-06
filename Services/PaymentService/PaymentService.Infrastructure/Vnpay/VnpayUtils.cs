using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;

namespace PaymentService.Infrastructure.Vnpay;

/// <summary>
/// Lớp tiện ích xử lý mã hóa HMAC-SHA512, tạo URL thanh toán và kiểm tra chữ ký bảo mật từ VNPAY.
/// </summary>
public class VnpayUtils
{
    private readonly SortedDictionary<string, string> _requestData = new(new VnpayCompare());
    private readonly SortedDictionary<string, string> _responseData = new(new VnpayCompare());

    /// <summary>
    /// Thêm tham số gửi đi lên VNPAY. Dữ liệu sẽ tự động được sắp xếp theo bảng chữ cái A-Z.
    /// </summary>
    /// <param name="key">string - Tên tham số VNPAY (ví dụ vnp_Amount).</param>
    /// <param name="value">string - Giá trị tham số.</param>
    public void AddRequestData(string key, string value)
    {
        if (!string.IsNullOrEmpty(value))
        {
            _requestData[key] = value;
        }
    }

    /// <summary>
    /// Thêm tham số VNPAY trả về để phục vụ kiểm tra chữ ký.
    /// </summary>
    /// <param name="key">string - Tên tham số VNPAY trả về.</param>
    /// <param name="value">string - Giá trị tương ứng.</param>
    public void AddResponseData(string key, string value)
    {
        if (!string.IsNullOrEmpty(value))
        {
            _responseData[key] = value;
        }
    }

    /// <summary>
    /// Lấy giá trị tham số trả về theo key.
    /// </summary>
    /// <param name="key">string - Tên tham số cần lấy.</param>
    /// <returns>string - Giá trị hoặc chuỗi rỗng nếu không tìm thấy.</returns>
    public string GetResponseData(string key)
    {
        return _responseData.TryGetValue(key, out var retValue) ? retValue : string.Empty;
    }

    /// <summary>
    /// Tạo URL thanh toán hoàn chỉnh chứa chuỗi mã hóa vnp_SecureHash.
    /// </summary>
    /// <param name="baseUrl">string - URL cổng VNPAY Sandbox.</param>
    /// <param name="vnpHashSecret">string - Chuỗi bí mật mã hóa (HashSecret).</param>
    /// <returns>string - Link thanh toán gửi cho khách hàng.</returns>
    public string CreateRequestUrl(string baseUrl, string vnpHashSecret)
    {
        var data = new StringBuilder();

        foreach (var (key, value) in _requestData)
        {
            if (!string.IsNullOrEmpty(value))
            {
                data.Append(WebUtility.UrlEncode(key) + "=" + WebUtility.UrlEncode(value) + "&");
            }
        }

        var queryString = data.ToString();
        var rawData = queryString.TrimEnd('&');

        var vnpSecureHash = HmacSha512(vnpHashSecret, rawData);
        var paymentUrl = $"{baseUrl}?{queryString}vnp_SecureHash={vnpSecureHash}";

        return paymentUrl;
    }

    /// <summary>
    /// Kiểm tra chữ ký bảo mật (Checksum) do VNPAY trả về xem có bị sửa đổi dữ liệu hay không.
    /// </summary>
    /// <param name="inputHash">string - Chuỗi vnp_SecureHash từ VNPAY trả về.</param>
    /// <param name="secretKey">string - Chuỗi bí mật vnp_HashSecret của hệ thống.</param>
    /// <returns>bool - True nếu hợp lệ, False nếu dữ liệu bị giả mạo.</returns>
    public bool ValidateSignature(string inputHash, string secretKey)
    {
        var data = new StringBuilder();

        foreach (var (key, value) in _responseData)
        {
            if (!string.IsNullOrEmpty(value) && key.StartsWith("vnp_") && key != "vnp_SecureHash" && key != "vnp_SecureHashType")
            {
                data.Append(WebUtility.UrlEncode(key) + "=" + WebUtility.UrlEncode(value) + "&");
            }
        }

        var rawData = data.ToString().TrimEnd('&');
        var myChecksum = HmacSha512(secretKey, rawData);

        return myChecksum.Equals(inputHash, StringComparison.InvariantCultureIgnoreCase);
    }

    /// <summary>
    /// Thuật toán băm mã hóa HMAC-SHA512 theo đúng quy chuẩn VNPAY.
    /// </summary>
    private static string HmacSha512(string key, string inputData)
    {
        var hash = new StringBuilder();
        var keyBytes = Encoding.UTF8.GetBytes(key);
        var inputBytes = Encoding.UTF8.GetBytes(inputData);

        using var hmac = new HMACSHA512(keyBytes);
        var hashValue = hmac.ComputeHash(inputBytes);

        foreach (var b in hashValue)
        {
            hash.Append(b.ToString("x2"));
        }

        return hash.ToString();
    }
}

/// <summary>
/// Lớp hỗ trợ so sánh chuỗi theo bảng mã Ordinal chuẩn VNPAY (A-Z).
/// </summary>
public class VnpayCompare : IComparer<string>
{
    public int Compare(string? x, string? y)
    {
        if (x == y) return 0;
        if (x == null) return -1;
        if (y == null) return 1;

        var vnpCompare = CultureInfo.InvariantCulture.CompareInfo;
        return vnpCompare.Compare(x, y, CompareOptions.Ordinal);
    }
}
