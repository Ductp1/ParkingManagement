namespace NotificationService.Infrastructure.Services.EmailSender;

/// <summary>
/// Configuration cho SMTP mock/real. Module: TV6 (S1-T602).
/// </summary>
public sealed class SmtpConfiguration
{
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 587;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string FromEmail { get; set; } = "noreply@parkingmanagement.local";
    public string FromName { get; set; } = "Parking Management";
    public bool EnableSsl { get; set; } = false;
    public bool UseMock { get; set; } = true; // TRUE = mock mode, FALSE = real SMTP
    public int TimeoutMs { get; set; } = 10000;
}
