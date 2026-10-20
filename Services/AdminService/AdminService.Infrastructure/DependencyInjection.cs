using AdminService.Application.Features;
using AdminService.Application.Features.Owners;
using AdminService.Application.Features.PartnerOnboarding;
using AdminService.Application.Features.Settings;
using AdminService.Application.Features.Users;
using AdminService.Infrastructure.Documents;
using AdminService.Infrastructure.Integrations;
using AdminService.Infrastructure.Persistence;
using AdminService.Infrastructure.Persistence.Queries;
using AdminService.Infrastructure.Persistence.Repositories;
using AdminService.Infrastructure.Persistence.Seeding;
using AdminService.Infrastructure.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ParkingManagement.ServiceDefaults;
using ParkingManagement.ServiceDefaults.Persistence;

namespace AdminService.Infrastructure;

public static class DependencyInjection
{
    private const string GatewayBaseUrlKey = "Services:GatewayBaseUrl";
    private const string StorageRootKey = "Storage:RootPath";

    public static IServiceCollection AddAdminInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddServiceDbContext<AdminDbContext>(configuration);
        services.AddScoped<ISettingsQueries, SettingsQueries>();
        services.AddScoped<ISystemConfigChangeQueries, SystemConfigChangeQueries>();
        services.AddScoped<ISanctionQueries, SanctionQueries>();
        services.AddScoped<IPartnerApplicationQueries, PartnerApplicationQueries>();   // US-116
        services.AddScoped<IOwnerLockRepository, OwnerLockRepository>();
        services.AddScoped<IPartnerApplicationRepository, PartnerApplicationRepository>();   // US-116
        services.AddScoped<ISystemConfigRepository, SystemConfigRepository>();
        services.AddScoped<IDataSeeder<AdminDbContext>, AdminDataSeeder>();

        // US-116: hợp đồng tự sinh dạng HTML + lưu file local (đường dẫn tương đối tính từ thư mục gốc của API).
        services.AddSingleton<IContractRenderer, HtmlContractRenderer>();
        services.AddSingleton<IFileStorage>(sp => new LocalFileStorage(Path.GetFullPath(
            configuration[StorageRootKey] ?? "App_Data/files", sp.GetRequiredService<IHostEnvironment>().ContentRootPath)));

        // US-096: dữ liệu người dùng lấy từ UserService qua Gateway (không truy cập pm_user).
        if (!Uri.TryCreate(configuration[GatewayBaseUrlKey], UriKind.Absolute, out var gatewayUri)
            || gatewayUri.Scheme is not ("http" or "https"))
            throw new InvalidOperationException($"Thiếu hoặc sai {GatewayBaseUrlKey} trong appsettings.json.");

        services.AddHttpContextAccessor();
        services.AddTransient<BearerTokenForwardingHandler>();
        services.AddHttpClient<UserServiceClient>(client =>
            {
                client.BaseAddress = new Uri(gatewayUri.AbsoluteUri.TrimEnd('/') + "/");
                client.Timeout = TimeSpan.FromSeconds(10);
            })
            .AddHttpMessageHandler<BearerTokenForwardingHandler>();
        services.AddTransient<IUserServiceClient>(sp => sp.GetRequiredService<UserServiceClient>());
        // US-116: tạo / kích hoạt tài khoản chủ bãi (U2 / U3) dùng chung client UserService.
        services.AddTransient<IOwnerAccountClient>(sp => sp.GetRequiredService<UserServiceClient>());
        return services;
    }
}
