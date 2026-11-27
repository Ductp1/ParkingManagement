using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace UserService.Infrastructure.Authentication;

public sealed class CaptchaEnvironmentValidator(IHostEnvironment environment) : IValidateOptions<CaptchaOptions>
{
    public ValidateOptionsResult Validate(string? name, CaptchaOptions options)
    {
        if (environment.IsDevelopment()) return ValidateOptionsResult.Success;
        if (options.ExpectedAction != "login")
            return ValidateOptionsResult.Fail("Captcha action must be login outside Development.");
        if (!options.Enabled)
            return ValidateOptionsResult.Fail("Captcha cannot be disabled outside Development.");
        if (options.SecretKey is "1x0000000000000000000000000000000AA" or
            "2x0000000000000000000000000000000AA" or "3x0000000000000000000000000000000AA")
            return ValidateOptionsResult.Fail("Turnstile test keys cannot be used outside Development.");
        return ValidateOptionsResult.Success;
    }
}
