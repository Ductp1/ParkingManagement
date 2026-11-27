namespace UserService.Infrastructure.Authentication;

public sealed class CaptchaOptions
{
    public bool Enabled { get; set; } = true;
    public string SecretKey { get; set; } = "";
    public string[] AllowedHostnames { get; set; } = [];
    public string ExpectedAction { get; set; } = "login";
    public int TimeoutSeconds { get; set; } = 5;
}
