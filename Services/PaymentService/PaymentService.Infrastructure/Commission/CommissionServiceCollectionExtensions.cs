using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ParkingManagement.SharedKernel.Enums;
using PaymentService.Application.Features.Commission;
using PaymentService.Infrastructure.Commission;
using PaymentService.Infrastructure.Persistence.Queries;
using PaymentService.Infrastructure.Persistence.Repositories;

namespace PaymentService.Infrastructure;

/// <summary>
/// US-079: đăng ký toàn bộ phần hoa hồng nền tảng trong một chỗ (Program.cs gọi 1 dòng) để không phải sửa DependencyInjection.cs dùng chung.
/// Cấu hình:
/// <code>
/// "Commission": { "Enabled": true, "IntervalSeconds": 60, "BatchSize": 200, "AdminTimeoutSeconds": 5, "ExcludedPurposes": [] },
/// "Services": { "AdminService": { "BaseUrl": "http://localhost:5108", "ApiKey": "" } }   // ApiKey chỉ qua biến môi trường
/// </code>
/// </summary>
public static class CommissionServiceCollectionExtensions
{
    public const string SectionName = "Commission";

    public static IServiceCollection AddPaymentCommission(this IServiceCollection services, IConfiguration configuration)
    {
        var section = configuration.GetSection(SectionName);
        services.AddSingleton(ReadPolicy(section));
        services.AddSingleton(new CommissionJobSchedule(
            section.GetValue("Enabled", true),
            TimeSpan.FromSeconds(Math.Max(CommissionJobSchedule.MinIntervalSeconds,
                section.GetValue("IntervalSeconds", CommissionJobSchedule.DefaultIntervalSeconds)))));
        var connection = ReadAdminService(configuration);
        services.AddSingleton(connection);

        // AdminService: route /internal không qua Gateway. HttpClient.Timeout chỉ là chốt chặn cuối – timeout thật đặt mỗi lần gọi.
        services.AddHttpClient<ICommissionRateClient, AdminCommissionRateClient>(client => client.Timeout = TimeSpan.FromSeconds(30));

        services.AddScoped<ICommissionLedgerRepository, CommissionLedgerRepository>();
        services.AddScoped<IRunCommissionAccrualUseCase, RunCommissionAccrualUseCase>();
        services.AddHostedService<CommissionAccrualJob>();

        // Báo cáo hoa hồng toàn sàn cho Admin.
        services.AddScoped<IAdminCommissionQueries, AdminCommissionQueries>();
        services.AddScoped<IGetAdminCommissionSummaryUseCase, GetAdminCommissionSummaryUseCase>();
        return services;
    }

    /// <summary>ExcludedPurposes: tên PaymentPurpose (không phân biệt hoa thường); tên sai → lỗi khi khởi động thay vì bỏ qua ngầm.</summary>
    public static CommissionPolicy ReadPolicy(IConfiguration section)
    {
        var excluded = new List<PaymentPurpose>();
        foreach (var value in section.GetSection("ExcludedPurposes").GetChildren().Select(c => c.Value).Where(v => !string.IsNullOrWhiteSpace(v)))
        {
            var purpose = Enum.GetValues<PaymentPurpose>().Cast<PaymentPurpose?>()
                              .FirstOrDefault(p => string.Equals(p.ToString(), value!.Trim(), StringComparison.OrdinalIgnoreCase))
                          ?? throw new InvalidOperationException(
                              $"{SectionName}:ExcludedPurposes chứa '{value}' không phải PaymentPurpose ({string.Join(", ", Enum.GetNames<PaymentPurpose>())}).");
            if (!excluded.Contains(purpose)) excluded.Add(purpose);
        }
        var batch = Math.Clamp(section.GetValue("BatchSize", CommissionPolicy.DefaultBatchSize), 1, CommissionPolicy.MaxBatchSize);
        return new CommissionPolicy(excluded, batch);
    }

    public static AdminServiceConnection ReadAdminService(IConfiguration configuration)
    {
        var raw = configuration[AdminServiceConnection.BaseUrlKey];
        Uri? baseUrl = null;
        if (!string.IsNullOrWhiteSpace(raw))
        {
            if (!Uri.TryCreate(raw.TrimEnd('/') + "/", UriKind.Absolute, out baseUrl) || baseUrl.Scheme is not ("http" or "https"))
                throw new InvalidOperationException($"{AdminServiceConnection.BaseUrlKey} không phải URL http(s) hợp lệ.");
        }
        var seconds = configuration.GetValue<double?>(AdminServiceConnection.TimeoutKey);
        var timeout = seconds is > 0 ? TimeSpan.FromSeconds(seconds.Value) : AdminServiceConnection.DefaultTimeout;
        var apiKey = configuration[AdminServiceConnection.ApiKeyKey];
        return new AdminServiceConnection(baseUrl, string.IsNullOrWhiteSpace(apiKey) ? null : apiKey.Trim(), timeout);
    }
}
