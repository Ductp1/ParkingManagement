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
        services.AddServiceDbContext<UserDbContext>(configuration);
        services.AddScoped<IUserQueries, UserQueries>();
        services.AddScoped<IDataSeeder<UserDbContext>, UserDataSeeder>();

        // Đăng ký Auth services
        services.AddScoped<UserService.Application.Interfaces.IJwtTokenGenerator, UserService.Infrastructure.Authentication.JwtTokenGenerator>();
        services.AddScoped<UserService.Application.Interfaces.IPasswordHasher, UserService.Infrastructure.Authentication.PasswordHasher>();
        services.AddScoped<UserService.Application.Features.Auth.IAuthRepository, UserService.Infrastructure.Persistence.Repositories.AuthRepository>();
        services.AddScoped<UserService.Application.Features.Auth.ISessionRepository, UserService.Infrastructure.Persistence.Repositories.SessionRepository>();
        services.AddScoped<UserService.Application.Features.Identity.IIdentityStore, UserService.Infrastructure.Persistence.Repositories.IdentityStore>();
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
