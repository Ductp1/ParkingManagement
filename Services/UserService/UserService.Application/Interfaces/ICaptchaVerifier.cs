namespace UserService.Application.Interfaces;

public interface ICaptchaVerifier
{
    Task VerifyAsync(string? token, CancellationToken cancellationToken = default);
}

// Handled by the login endpoint; never includes provider tokens or secrets.
public sealed class CaptchaException(bool unavailable) : Exception(unavailable
    ? "Dịch vụ CAPTCHA tạm thời không khả dụng. Vui lòng thử lại."
    : "Vui lòng xác thực CAPTCHA lại.")
{
    public bool Unavailable { get; } = unavailable;
    public string Code => Unavailable ? "CAPTCHA_UNAVAILABLE" : "CAPTCHA_INVALID";
}
