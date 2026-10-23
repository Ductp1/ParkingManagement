using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ParkingManagement.ServiceDefaults;
using ParkingManagement.ServiceDefaults.Persistence;
using UserService.Application.Features.Users;
using UserService.Infrastructure.Persistence;
using UserService.Infrastructure.Persistence.Queries;
using UserService.Infrastructure.Persistence.Seeding;

namespace UserService.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddUserInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<UserService.Infrastructure.Authentication.CaptchaOptions>()
            .Bind(configuration.GetSection("Captcha"))
            .Validate(o => o.TimeoutSeconds is >= 1 and <= 15, "Captcha timeout must be 1-15 seconds.")
            .Validate(o => !o.Enabled || (!string.IsNullOrWhiteSpace(o.SecretKey)
                && o.AllowedHostnames.Length > 0 && o.AllowedHostnames.All(h => !string.IsNullOrWhiteSpace(h))
                && !string.IsNullOrWhiteSpace(o.ExpectedAction)), "Captcha requires SecretKey, AllowedHostnames and ExpectedAction.")
            .ValidateOnStart();
        services.AddSingleton<Microsoft.Extensions.Options.IValidateOptions<UserService.Infrastructure.Authentication.CaptchaOptions>,
            UserService.Infrastructure.Authentication.CaptchaEnvironmentValidator>();
        services.AddHttpClient<UserService.Application.Interfaces.ICaptchaVerifier,
            UserService.Infrastructure.Authentication.TurnstileCaptchaVerifier>((provider, client) =>
            client.Timeout = TimeSpan.FromSeconds(provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<
                UserService.Infrastructure.Authentication.CaptchaOptions>>().Value.TimeoutSeconds));
        services.AddServiceDbContext<UserDbContext>(configuration);
        services.AddScoped<IUserQueries, UserQueries>();
        services.AddScoped<IDataSeeder<UserDbContext>, UserDataSeeder>();

        // Đăng ký Auth services
        services.AddScoped<UserService.Application.Interfaces.IJwtTokenGenerator, UserService.Infrastructure.Authentication.JwtTokenGenerator>();
        services.AddScoped<UserService.Application.Interfaces.IPasswordHasher, UserService.Infrastructure.Authentication.PasswordHasher>();
        services.AddScoped<UserService.Application.Features.Auth.IAuthRepository, UserService.Infrastructure.Persistence.Repositories.AuthRepository>();
        services.AddScoped<UserService.Application.Features.Auth.ISessionRepository, UserService.Infrastructure.Persistence.Repositories.SessionRepository>();
        services.AddScoped<UserService.Application.Features.Identity.IIdentityStore, UserService.Infrastructure.Persistence.Repositories.IdentityStore>();
        services.AddScoped<UserService.Application.Features.Users.IOwnerProfileRepository, UserService.Infrastructure.Persistence.Repositories.OwnerProfileRepository>();
        services.AddScoped<UserService.Application.Features.Identity.ISecretCipher, UserService.Infrastructure.Authentication.AesSecretCipher>();
        services.AddScoped<UserService.Application.Features.Identity.IOtpDeliveryConfiguration, UserService.Infrastructure.Integrations.OtpDeliveryConfiguration>();
        services.AddScoped<ParkingManagement.ServiceDefaults.ITokenPrincipalValidator, UserService.Infrastructure.Authentication.UserPrincipalValidator>();
        services.AddScoped<ParkingManagement.ServiceDefaults.Messaging.IOutboxTransport, UserService.Infrastructure.Integrations.IdentityOutboxTransport>();
        services.AddHostedService<ParkingManagement.ServiceDefaults.Messaging.OutboxDispatcher<UserDbContext>>();
        services.AddHostedService<UserService.Infrastructure.Storage.IdentityRetentionWorker>();
        services.AddHttpClient("IdentityIntegrations", client => client.Timeout = TimeSpan.FromSeconds(10));
        return services;
    }
}
