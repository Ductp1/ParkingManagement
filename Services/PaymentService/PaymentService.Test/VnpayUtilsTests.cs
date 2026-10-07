using PaymentService.Infrastructure.Vnpay;

namespace PaymentService.Test;

/// <summary>
/// Bộ kiểm thử tự động cho lớp tiện ích VnpayUtils (Task T-401).
/// </summary>
public class VnpayUtilsTests
{
    private const string SecretKey = "XXPMWWQHGMCWVPKGQAHMVOZJWEKMLFDM";

    /// <summary>
    /// Kiểm tra hàm CreateRequestUrl có tạo đúng URL chứa vnp_SecureHash hay không.
    /// </summary>
    [Fact]
    public void CreateRequestUrl_Should_Build_Correct_Url_And_Signature()
    {
        var vnpay = new VnpayUtils();
        vnpay.AddRequestData("vnp_Version", "2.1.0");
        vnpay.AddRequestData("vnp_Command", "pay");
        vnpay.AddRequestData("vnp_TmnCode", "QYPLY49G");
        vnpay.AddRequestData("vnp_Amount", "10000000");
        vnpay.AddRequestData("vnp_TxnRef", "BOOK123456");

        var baseUrl = "https://sandbox.vnpayment.vn/paymentv2/vpcpay.html";
        var url = vnpay.CreateRequestUrl(baseUrl, SecretKey);

        Assert.NotNull(url);
        Assert.Contains("vnp_SecureHash=", url);
        Assert.StartsWith(baseUrl, url);
    }

    /// <summary>
    /// Kiểm tra hàm ValidateSignature trả về true khi chữ ký HMAC-SHA512 khớp hoàn toàn.
    /// </summary>
    [Fact]
    public void ValidateSignature_Should_Return_True_For_Valid_Hash()
    {
        var testUtils = new VnpayUtils();
        testUtils.AddRequestData("vnp_Amount", "10000000");
        testUtils.AddRequestData("vnp_BankCode", "NCB");
        testUtils.AddRequestData("vnp_ResponseCode", "00");
        testUtils.AddRequestData("vnp_TxnRef", "BOOK123456");

        var generatedUrl = testUtils.CreateRequestUrl("http://localhost", SecretKey);
        var secureHash = generatedUrl.Split("vnp_SecureHash=")[1];

        var vnpay = new VnpayUtils();
        vnpay.AddResponseData("vnp_Amount", "10000000");
        vnpay.AddResponseData("vnp_BankCode", "NCB");
        vnpay.AddResponseData("vnp_ResponseCode", "00");
        vnpay.AddResponseData("vnp_TxnRef", "BOOK123456");

        var isValid = vnpay.ValidateSignature(secureHash, SecretKey);
        Assert.True(isValid);
    }

    /// <summary>
    /// Kiểm tra hàm ValidateSignature trả về false khi số tiền hoặc chữ ký bị giả mạo.
    /// </summary>
    [Fact]
    public void ValidateSignature_Should_Return_False_When_Data_Is_Tampered()
    {
        var vnpay = new VnpayUtils();
        vnpay.AddResponseData("vnp_Amount", "100000"); // Dữ liệu bị giả mạo
        vnpay.AddResponseData("vnp_BankCode", "NCB");
        vnpay.AddResponseData("vnp_ResponseCode", "00");
        vnpay.AddResponseData("vnp_TxnRef", "BOOK123456");

        var fakeHash = "invalid_hash_value_1234567890";

        var isValid = vnpay.ValidateSignature(fakeHash, SecretKey);
        Assert.False(isValid);
    }
}
