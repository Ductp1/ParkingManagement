using AdminService.Application.Features;
using AdminService.Application.Features.Users;
using AdminService.Infrastructure.Integrations;
using AdminService.Infrastructure.Persistence;
using AdminService.Infrastructure.Persistence.Queries;
using AdminService.Infrastructure.Persistence.Seeding;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ParkingManagement.ServiceDefaults;
using ParkingManagement.ServiceDefaults.Persistence;

namespace AdminService.Infrastructure;

public static class DependencyInjection
{
    private const string GatewayBaseUrlKey = "Services:GatewayBaseUrl";

    public static IServiceCollection AddAdminInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddServiceDbContext<AdminDbContext>(configuration);
        services.AddScoped<ISettingsQueries, SettingsQueries>();
        services.AddScoped<IDataSeeder<AdminDbContext>, AdminDataSeeder>();

        // US-096: dữ liệu người dùng lấy từ UserService qua Gateway (không truy cập pm_user).
        if (!Uri.TryCreate(configuration[GatewayBaseUrlKey], UriKind.Absolute, out var gatewayUri)
            || gatewayUri.Scheme is not ("http" or "https"))
            throw new InvalidOperationException($"Thiếu hoặc sai {GatewayBaseUrlKey} trong appsettings.json.");

        services.AddHttpContextAccessor();
        services.AddTransient<BearerTokenForwardingHandler>();
        services.AddHttpClient<IUserServiceClient, UserServiceClient>(client =>
            {
                client.BaseAddress = new Uri(gatewayUri.AbsoluteUri.TrimEnd('/') + "/");
                client.Timeout = TimeSpan.FromSeconds(10);
            })
            .AddHttpMessageHandler<BearerTokenForwardingHandler>();
        return services;
    }
}
