using AdminService.Application.Features;
using AdminService.Application.Features.AuditLogs;
using AdminService.Application.Features.Commission;
using AdminService.Application.Features.ContractTemplates;
using AdminService.Application.Features.Dashboard;
using AdminService.Application.Features.LotContracts;
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
        services.AddScoped<IDashboardQueries, DashboardQueries>();                           // US-081: số liệu dashboard
        services.AddScoped<ISystemConfigChangeQueries, SystemConfigChangeQueries>();
        services.AddScoped<ISanctionQueries, SanctionQueries>();
        services.AddScoped<IPartnerApplicationQueries, PartnerApplicationQueries>();   // US-116
        services.AddScoped<IOwnerLockRepository, OwnerLockRepository>();
        services.AddScoped<IAuditEventInboxRepository, AuditEventInboxRepository>();         // T-802: nhận AuditRecorded
        services.AddScoped<IPartnerApplicationRepository, PartnerApplicationRepository>();   // US-116
        services.AddScoped<ILotContractQueries, LotContractQueries>();                       // US-116
        services.AddScoped<ICommissionRateQueries, CommissionRateQueries>();                 // US-079: tỷ lệ hoa hồng theo hợp đồng
        services.AddScoped<ILotContractRepository, LotContractRepository>();
        services.AddScoped<IContractTemplateQueries, ContractTemplateQueries>();             // US-116: mẫu hợp đồng bãi
        services.AddScoped<IContractTemplateRepository, ContractTemplateRepository>();
        services.AddScoped<IAuditLogQueries, AuditLogQueries>();                             // US-100: tra cứu audit log
        services.AddScoped<ISystemConfigRepository, SystemConfigRepository>();
        services.AddScoped<IFeatureFlagRepository, FeatureFlagRepository>();   // US-099
        services.AddScoped<IOwnerPayoutAccountQueries, OwnerPayoutAccountQueries>();         // US-080: tài khoản nhận tiền quyết toán
        services.AddScoped<IDataSeeder<AdminDbContext>, AdminDataSeeder>();

        // US-116: hợp đồng tự sinh dạng HTML + lưu file local (đường dẫn tương đối tính từ thư mục gốc của API).
        services.AddSingleton<IContractRenderer, HtmlContractRenderer>();
        // US-116 v11: đọc / điền file DOCX (không package Office) + AI đọc hợp đồng nhập từ file (Claude, tùy chọn).
        services.AddSingleton<IDocxDocumentService, DocxDocumentService>();
        services.AddSingleton<IContractDocumentAnalyzer>(sp => new ClaudeContractDocumentAnalyzer(configuration,
            sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<ClaudeContractDocumentAnalyzer>>()));
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
        // US-116: đọc hồ sơ KYB + kích hoạt bãi bên ParkingService qua Gateway (route parking-lots), chuyển tiếp JWT Admin.
        services.AddHttpClient<IParkingServiceClient, ParkingServiceClient>(client =>
            {
                client.BaseAddress = new Uri(gatewayUri.AbsoluteUri.TrimEnd('/') + "/");
                client.Timeout = TimeSpan.FromSeconds(10);
            })
            .AddHttpMessageHandler<BearerTokenForwardingHandler>();

        // US-081: nguồn chỉ-đọc của Admin dashboard qua Gateway (JWT Admin chuyển tiếp). Timeout từng nguồn đặt ở use case
        // ("Dashboard:SourceTimeoutSeconds:{Service}", mặc định 3 giây); HttpClient.Timeout chỉ là chốt chặn cuối.
        AddDashboardSource<IGateServiceClient, GateServiceClient>(services, gatewayUri);
        AddDashboardSource<ISupportServiceClient, SupportServiceClient>(services, gatewayUri);
        AddDashboardSource<IParkingDashboardClient, ParkingDashboardClient>(services, gatewayUri);
        AddDashboardSource<IPaymentServiceClient, PaymentServiceClient>(services, gatewayUri);
        AddDashboardSource<IBookingServiceClient, BookingServiceClient>(services, gatewayUri);
        AddDashboardSource<IUserDataRequestsClient, UserDataRequestsClient>(services, gatewayUri);
        services.AddSingleton(DashboardTimeouts(configuration));
        return services;
    }

    private static void AddDashboardSource<TPort, TClient>(IServiceCollection services, Uri gatewayUri)
        where TPort : class where TClient : class, TPort
        => services.AddHttpClient<TPort, TClient>(client =>
            {
                client.BaseAddress = new Uri(gatewayUri.AbsoluteUri.TrimEnd('/') + "/");
                client.Timeout = TimeSpan.FromSeconds(10);
            })
            .AddHttpMessageHandler<BearerTokenForwardingHandler>();

    /// <summary>"Dashboard:SourceTimeoutSeconds": { "GateService": 3, … } – giá trị thiếu / ≤ 0 / sai định dạng dùng mặc định 3 giây.</summary>
    private static DashboardSourceTimeouts DashboardTimeouts(IConfiguration configuration)
    {
        var perService = configuration.GetSection("Dashboard:SourceTimeoutSeconds").GetChildren()
            .Where(s => double.TryParse(s.Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture,
                out var seconds) && seconds > 0)
            .ToDictionary(s => s.Key, s => TimeSpan.FromSeconds(double.Parse(s.Value!, System.Globalization.CultureInfo.InvariantCulture)));
        return new DashboardSourceTimeouts(perService);
    }
}
