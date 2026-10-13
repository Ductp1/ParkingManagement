using System.Security.Cryptography;
using System.Text;
using GateService.Application.Features.GateConsole;

namespace GateService.Test;

// T-704: Xác thực QR token do BookingService cấp (unit test, thuần bộ nhớ).
public class BookingQrTokenValidatorTests
{
    // "Bây giờ" cố định để test hạn dùng một cách xác định.
    private static readonly DateTimeOffset Now = new(2026, 10, 9, 8, 0, 0, TimeSpan.Zero);
    private static readonly FixedClock Clock = new(Now);

    private readonly BookingQrTokenValidator _validator = new(Clock, TestQrTokenFactory.BookingServiceSecret);

    [Fact]
    public void Valid_token_is_accepted_and_exposes_verified_claims()
    {
        var expires = Now.AddHours(2).UtcDateTime;
        var token = TestQrTokenFactory.Create("BK-20261002-0001", "51F-123.45", 2, expires);

        var ok = _validator.TryValidate(token, expectedParkingLotId: 2, out var claims);

        Assert.True(ok);
        Assert.NotNull(claims);
        Assert.Equal("BK-20261002-0001", claims!.BookingCode);
        Assert.Equal("51F-123.45", claims.PlateNumber);
        Assert.Equal(2, claims.ParkingLotId);
        Assert.Equal(expires, claims.ExpiresAtUtc);
    }

    [Fact]
    public void Token_signed_with_wrong_secret_is_rejected()
    {
        var token = TestQrTokenFactory.Create("BK-20261002-0001", "51F-123.45", 2, Now.AddHours(2).UtcDateTime,
            secret: "totally-different-secret");

        Assert.False(_validator.TryValidate(token, 2, out var claims));
        Assert.Null(claims);
    }

    [Fact]
    public void Tampered_payload_is_rejected()
    {
        var token = TestQrTokenFactory.Create("BK-20261002-0001", "51F-123.45", 2, Now.AddHours(2).UtcDateTime);
        // Đổi mã booking trong payload nhưng GIỮ nguyên chữ ký → chữ ký không còn khớp.
        var parts = token.Split('.');
        var fields = Encoding.UTF8.GetString(Convert.FromBase64String(parts[0])).Split('|');
        var forgedPayload = $"BK-20261002-9999|{fields[1]}|{fields[2]}|{fields[3]}";
        var tampered = $"{TestQrTokenFactory.EncodePayload(forgedPayload)}.{parts[1]}";

        Assert.False(_validator.TryValidate(tampered, 2, out _));
    }

    [Fact]
    public void Tampered_signature_is_rejected()
    {
        var token = TestQrTokenFactory.Create("BK-20261002-0001", "51F-123.45", 2, Now.AddHours(2).UtcDateTime);
        var parts = token.Split('.');
        var flippedSignature = parts[1][..^1] + (parts[1][^1] == 'A' ? "B" : "A"); // hỏng chữ ký cuối
        var tampered = $"{parts[0]}.{flippedSignature}";

        Assert.False(_validator.TryValidate(tampered, 2, out _));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Empty_token_is_rejected(string? token)
        => Assert.False(_validator.TryValidate(token, 2, out _));

    [Theory]
    [InlineData("not-a-token")]                  // không có dấu '.'
    [InlineData("a.b.c")]                        // quá nhiều dấu '.'
    [InlineData("!!!.!!!")]                      // base64 hỏng
    public void Malformed_token_is_rejected(string token)
        => Assert.False(_validator.TryValidate(token, 2, out _));

    [Fact]
    public void Payload_with_wrong_number_of_fields_is_rejected()
    {
        var payload = "BK-20261002-0001|51F-123.45|2"; // thiếu expiry
        var token = $"{TestQrTokenFactory.EncodePayload(payload)}.{TestQrTokenFactory.Sign(payload, TestQrTokenFactory.BookingServiceSecret)}";
        Assert.False(_validator.TryValidate(token, 2, out _));
    }

    [Fact]
    public void Payload_with_empty_booking_code_or_plate_is_rejected()
    {
        var expires = Now.AddHours(2).Ticks;
        var noCodePayload = $"|51F-123.45|2|{expires}";
        var noCode = $"{TestQrTokenFactory.EncodePayload(noCodePayload)}.{TestQrTokenFactory.Sign(noCodePayload, TestQrTokenFactory.BookingServiceSecret)}";
        Assert.False(_validator.TryValidate(noCode, 2, out _));

        var noPlatePayload = $"BK-20261002-0001||2|{expires}";
        var noPlate = $"{TestQrTokenFactory.EncodePayload(noPlatePayload)}.{TestQrTokenFactory.Sign(noPlatePayload, TestQrTokenFactory.BookingServiceSecret)}";
        Assert.False(_validator.TryValidate(noPlate, 2, out _));
    }

    [Fact]
    public void Payload_with_non_numeric_lot_or_expiry_is_rejected()
    {
        var badLotPayload = $"BK-20261002-0001|51F-123.45|abc|{Now.AddHours(2).Ticks}";
        var badLot = $"{TestQrTokenFactory.EncodePayload(badLotPayload)}.{TestQrTokenFactory.Sign(badLotPayload, TestQrTokenFactory.BookingServiceSecret)}";
        Assert.False(_validator.TryValidate(badLot, 2, out _));

        var badTicksPayload = "BK-20261002-0001|51F-123.45|2|not-ticks";
        var badTicks = $"{TestQrTokenFactory.EncodePayload(badTicksPayload)}.{TestQrTokenFactory.Sign(badTicksPayload, TestQrTokenFactory.BookingServiceSecret)}";
        Assert.False(_validator.TryValidate(badTicks, 2, out _));
    }

    [Fact]
    public void Expired_token_is_rejected()
    {
        var justExpired = TestQrTokenFactory.Create("BK-20261002-0001", "51F-123.45", 2, Now.AddSeconds(-1).UtcDateTime);
        Assert.False(_validator.TryValidate(justExpired, 2, out _));

        // Đúng semantics của BookingService: expiry == now đã coi là hết hạn (expiry > now mới hợp lệ).
        var exactlyNow = TestQrTokenFactory.Create("BK-20261002-0001", "51F-123.45", 2, Now.UtcDateTime);
        Assert.False(_validator.TryValidate(exactlyNow, 2, out _));
    }

    [Fact]
    public void Wrong_parking_lot_token_is_rejected()
    {
        var token = TestQrTokenFactory.Create("BK-20261002-0001", "51F-123.45", 99, Now.AddHours(2).UtcDateTime);

        Assert.False(_validator.TryValidate(token, expectedParkingLotId: 2, out _));
    }

    [Fact]
    public void Secret_is_read_from_environment_variable_when_not_injected()
    {
        // TestEnvironment (ModuleInitializer) đã đặt QRTOKEN_SECRET cho tiến trình test
        // → validator KHÔNG tiêm secret vẫn phải đọc được từ biến môi trường (như production).
        var fromEnv = new BookingQrTokenValidator(Clock);
        var token = TestQrTokenFactory.Create("BK-20261002-0001", "51F-123.45", 2, Now.AddHours(2).UtcDateTime);

        Assert.True(fromEnv.TryValidate(token, 2, out var claims));
        Assert.Equal("BK-20261002-0001", claims!.BookingCode);
    }

    [Fact]
    public void Empty_environment_secret_fails_fast()
    {
        // Secret rỗng/khoảng trắng phải bị chặn ngay lúc khởi tạo (không âm thầm dùng giá trị rỗng).
        Assert.Throws<InvalidOperationException>(() => new BookingQrTokenValidator(Clock, sharedSecret: "   "));
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}

/// <summary>
/// Sinh QR token đúng format BookingService.QrTokenService để test GateService
/// (GateService.Test KHÔNG tham chiếu BookingService – phải tái tạo hợp đồng ở đây).
/// </summary>
internal static class TestQrTokenFactory
{
    // PHẢI trùng với SecretKey trong BookingService.Application.QrTokenService.
    public const string BookingServiceSecret = "SmartParking_BookingService_HMACSHA256_SecretKey_2026";

    public static string Create(string code, string plate, int parkingLotId, DateTime expiresAtUtc, string? secret = null)
    {
        var payload = $"{code}|{plate}|{parkingLotId}|{expiresAtUtc.Ticks}";
        return $"{EncodePayload(payload)}.{Sign(payload, secret ?? BookingServiceSecret)}";
    }

    public static string EncodePayload(string payload) => Convert.ToBase64String(Encoding.UTF8.GetBytes(payload));

    public static string Sign(string payload, string secret)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        return Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(payload)));
    }
}
