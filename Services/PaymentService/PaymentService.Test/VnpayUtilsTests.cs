using PaymentService.Infrastructure.Vnpay;

namespace PaymentService.Test;

/// <summary>
/// Bộ kiểm thử tự động toàn diện cho lớp tiện ích VnpayUtils (Task T-401).
/// Bổ sung đầy đủ kịch bản theo góp ý của Reviewer: Valid hash, Invalid hash, Missing hash, Parameter order, Empty values.
/// </summary>
public class VnpayUtilsTests
{
    private const string SecretKey = "XXPMWWQHGMCWVPKGQAHMVOZJWEKMLFDM";

    /// <summary>
    /// 1. Valid Hash: Kiểm tra ValidateSignature trả về true khi chữ ký khớp 100%.
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
    /// 2. Invalid Hash: Kiểm tra ValidateSignature trả về false khi chữ ký bị sai hoặc dữ liệu bị sửa đổi.
    /// </summary>
    [Fact]
    public void ValidateSignature_Should_Return_False_For_Invalid_Hash()
    {
        var vnpay = new VnpayUtils();
        vnpay.AddResponseData("vnp_Amount", "100000"); // Dữ liệu bị giả mạo
        vnpay.AddResponseData("vnp_BankCode", "NCB");
        vnpay.AddResponseData("vnp_ResponseCode", "00");

        var fakeHash = "INVALID_HASH_1234567890ABCDEF";
        var isValid = vnpay.ValidateSignature(fakeHash, SecretKey);

        Assert.False(isValid);
    }

    /// <summary>
    /// 3. Missing Hash: Kiểm tra ValidateSignature trả về false khi chuỗi chữ ký bị null hoặc rỗng.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("   ")]
    public void ValidateSignature_Should_Return_False_When_Hash_Is_Missing_Or_Empty(string? missingHash)
    {
        var vnpay = new VnpayUtils();
        vnpay.AddResponseData("vnp_Amount", "10000000");

        var isValid = vnpay.ValidateSignature(missingHash!, SecretKey);

        Assert.False(isValid);
    }

    /// <summary>
    /// 4. Parameter Order: Kiểm tra CreateRequestUrl tự động sắp xếp tham số theo đúng thứ tự A-Z bất kể thứ tự Add.
    /// </summary>
    [Fact]
    public void CreateRequestUrl_Should_Sort_Parameters_Alphabetically()
    {
        var vnpay = new VnpayUtils();
        // Thêm tham số không theo thứ tự bảng chữ cái (Z -> C -> A)
        vnpay.AddRequestData("vnp_ZParam", "ValueZ");
        vnpay.AddRequestData("vnp_Command", "pay");
        vnpay.AddRequestData("vnp_Amount", "10000000");

        var url = vnpay.CreateRequestUrl("https://sandbox.vnpayment.vn/paymentv2/vpcpay.html", SecretKey);

        // Kiểm tra thứ tự xuất hiện trong chuỗi URL: vnp_Amount phải đứng trước vnp_Command và vnp_ZParam
        var amountIndex = url.IndexOf("vnp_Amount=");
        var commandIndex = url.IndexOf("vnp_Command=");
        var zParamIndex = url.IndexOf("vnp_ZParam=");

        Assert.True(amountIndex < commandIndex);
        Assert.True(commandIndex < zParamIndex);
    }

    /// <summary>
    /// 5. Empty Values: Kiểm tra CreateRequestUrl và AddRequestData loại bỏ tham số có giá trị rỗng hoặc null.
    /// </summary>
    [Fact]
    public void CreateRequestUrl_Should_Ignore_Empty_Or_Null_Values()
    {
        var vnpay = new VnpayUtils();
        vnpay.AddRequestData("vnp_Amount", "10000000");
        vnpay.AddRequestData("vnp_EmptyParam", ""); // Tham số rỗng
        vnpay.AddRequestData("vnp_NullParam", null!); // Tham số null

        var url = vnpay.CreateRequestUrl("https://sandbox.vnpayment.vn/paymentv2/vpcpay.html", SecretKey);

        Assert.Contains("vnp_Amount=10000000", url);
        Assert.DoesNotContain("vnp_EmptyParam", url);
        Assert.DoesNotContain("vnp_NullParam", url);
    }
}
