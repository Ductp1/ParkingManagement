using System.Security.Cryptography;
using System.Text;

namespace GateService.Application.Features.GateConsole;

/// <summary>
/// T-704: Các trường đã xác thực (chữ ký + hạn dùng + bãi xe) của QR token do BookingService cấp lúc tạo booking.
/// BookingCode là mã booking gốc ("BK-..."), PlateNumber là biển số trên booking (chưa chuẩn hoá theo cổng).
/// </summary>
public sealed record BookingQrTokenClaims(string BookingCode, string PlateNumber, int ParkingLotId, DateTime ExpiresAtUtc);

/// <summary>
/// Port kiểm tra QR token check-in (T-704). GateService CHỈ xác thực – việc sinh token thuộc BookingService
/// (IQrTokenService). Hai bên phải dùng cùng định dạng, thuật toán và secret – đây là hợp đồng bảo mật TV3 ↔ TV7.
/// </summary>
public interface IBookingQrTokenValidator
{
    /// <summary>
    /// Xác thực QR token: đúng định dạng, chữ ký HMAC-SHA256 khớp (so sánh constant-time), đủ trường dữ liệu,
    /// chưa hết hạn và đúng bãi xe. Trả false cho mọi token hỏng/sai/hết hạn/sai bãi – không ném exception.
    /// </summary>
    bool TryValidate(string? token, int expectedParkingLotId, out BookingQrTokenClaims? claims);
}

/// <summary>
/// T-704: Cài đặt xác thực QR token của BookingService. Format phải khớp TỪNG BYTE với
/// BookingService.QrTokenService: payload "code|plate|parkingLotId|expiryTicks", token
/// "Base64(payload).Base64(HMACSHA256(payload))", hạn dùng hợp lệ khi expiry &gt; now.
/// Secret KHÔNG hardcode trong code: đọc từ biến môi trường <see cref="SecretEnvVar"/> lúc khởi tạo
/// (singleton) và PHẢI cùng giá trị với secret BookingService dùng để ký token. Thiếu secret → fail fast.
/// Chỉ thêm lớp kiểm tra của cổng (đủ trường, đúng bãi); KHÔNG đổi format token.
/// </summary>
public sealed class BookingQrTokenValidator : IBookingQrTokenValidator
{
    /// <summary>Tên biến môi trường chứa secret HMAC-SHA256 dùng chung với BookingService.</summary>
    public const string SecretEnvVar = "QRTOKEN_SECRET";

    private readonly string _sharedSecret;
    private readonly TimeProvider? _timeProvider;

    public BookingQrTokenValidator(TimeProvider? timeProvider = null, string? sharedSecret = null)
    {
        // Ưu tiên secret tiêm trực tiếp (unit test); production đọc từ biến môi trường.
        var secret = sharedSecret ?? Environment.GetEnvironmentVariable(SecretEnvVar);
        if (string.IsNullOrWhiteSpace(secret))
            throw new InvalidOperationException($"Thiếu biến môi trường {SecretEnvVar} – secret dùng chung với BookingService để xác thực QR token check-in.");
        _sharedSecret = secret;
        _timeProvider = timeProvider;
    }

    public bool TryValidate(string? token, int expectedParkingLotId, out BookingQrTokenClaims? claims)
    {
        claims = null;
        if (string.IsNullOrWhiteSpace(token)) return false;

        var parts = token.Split('.');
        if (parts.Length != 2) return false;

        try
        {
            var payload = Encoding.UTF8.GetString(Convert.FromBase64String(parts[0]));
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_sharedSecret));
            var expectedSignature = Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(payload)));
            // Constant-time: không lộ thông tin qua thời gian so sánh chữ ký.
            if (!CryptographicOperations.FixedTimeEquals(
                    Encoding.UTF8.GetBytes(parts[1]), Encoding.UTF8.GetBytes(expectedSignature)))
                return false;

            var fields = payload.Split('|');
            if (fields.Length != 4) return false;
            if (string.IsNullOrWhiteSpace(fields[0]) || string.IsNullOrWhiteSpace(fields[1])) return false;
            if (!int.TryParse(fields[2], out var parkingLotId) || parkingLotId <= 0) return false;
            var expiry = new DateTime(long.Parse(fields[3]), DateTimeKind.Utc);

            var now = (_timeProvider ?? TimeProvider.System).GetUtcNow().UtcDateTime;
            if (expiry <= now) return false;                       // Giữ nguyên semantics của BookingService: expiry > now.
            if (parkingLotId != expectedParkingLotId) return false; // Token của bãi khác – từ chối.

            claims = new BookingQrTokenClaims(fields[0], fields[1], parkingLotId, expiry);
            return true;
        }
        catch (FormatException)
        {
            return false;   // Base64 hoặc số không hợp lệ.
        }
        catch (ArgumentException)
        {
            return false;   // Payload decode ra byte không hợp lệ.
        }
    }
}
