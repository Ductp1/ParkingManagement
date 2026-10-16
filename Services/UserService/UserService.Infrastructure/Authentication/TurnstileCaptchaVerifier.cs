using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using UserService.Application.Interfaces;

namespace UserService.Infrastructure.Authentication;

public sealed class TurnstileCaptchaVerifier(HttpClient client, IOptions<CaptchaOptions> options) : ICaptchaVerifier
{
    public async Task VerifyAsync(string? token, CancellationToken cancellationToken = default)
    {
        var settings = options.Value;
        if (!settings.Enabled) return;
        if (string.IsNullOrWhiteSpace(token) || token.Length > 2048)
            throw new CaptchaException(false);
        TurnstileResponse? result;
        try
        {
            using var body = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["secret"] = settings.SecretKey,
                ["response"] = token
            });
            using var response = await client.PostAsync(
                "https://challenges.cloudflare.com/turnstile/v0/siteverify", body, cancellationToken);
            if (!response.IsSuccessStatusCode) throw new CaptchaException(true);
            result = await response.Content.ReadFromJsonAsync<TurnstileResponse>(cancellationToken);
        }
        catch (HttpRequestException) { throw new CaptchaException(true); }
        catch (JsonException) { throw new CaptchaException(true); }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        { throw new CaptchaException(true); }

        if (result is null) throw new CaptchaException(true);
        if (result.Errors?.Any(e => e is "internal-error" or "missing-input-secret" or "invalid-input-secret") == true)
            throw new CaptchaException(true);
        if (!result.Success || result.Action != settings.ExpectedAction ||
            !settings.AllowedHostnames.Contains(result.Hostname, StringComparer.OrdinalIgnoreCase))
            throw new CaptchaException(false);
    }

    private sealed record TurnstileResponse(
        bool Success, string? Hostname, string? Action,
        [property: JsonPropertyName("error-codes")] string[]? Errors);
}
