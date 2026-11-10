using System.Globalization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PaymentService.Application.Features.Settlements;
using PaymentService.Infrastructure.Persistence.Repositories;
using PaymentService.Infrastructure.Settlements;

namespace PaymentService.Infrastructure;

/// <summary>
/// US-080: đăng ký quyết toán định kỳ trong một chỗ (Program.cs gọi 1 dòng, sau AddPaymentCommission – dùng lại CommissionPolicy và
/// kết nối AdminService của US-079). Cấu hình:
/// <code>
/// "Settlements": { "BiweeklyAnchor": "2026-01-05", "MaxCatchUpPeriods": 60, "SourceTimeoutSeconds": 10,
///                  "Generation": { "Enabled": true, "IntervalMinutes": 60 } },
/// "Services": { "GatewayBaseUrl": "http://localhost:5000", "AdminService": { "BaseUrl": "…", "ApiKey": "" } }   // ApiKey chỉ qua biến môi trường
/// </code>
/// </summary>
public static class SettlementServiceCollectionExtensions
{
    public const string SectionName = "Settlements";

    public static IServiceCollection AddPaymentSettlements(this IServiceCollection services, IConfiguration configuration)
    {
        var section = configuration.GetSection(SectionName);
        services.AddSingleton(ReadOptions(section));
        services.AddSingleton(ReadGateway(configuration, section));
        var generation = section.GetSection("Generation");
        services.AddSingleton(new SettlementJobSchedule(
            generation.GetValue("Enabled", true),
            TimeSpan.FromMinutes(Math.Max(SettlementJobSchedule.MinIntervalMinutes,
                generation.GetValue("IntervalMinutes", SettlementJobSchedule.DefaultIntervalMinutes)))));

        // HttpClient.Timeout chỉ là chốt chặn cuối – timeout thật đặt mỗi lần gọi.
        services.AddHttpClient<ISettlementCycleClient, AdminSettlementCycleClient>(client => client.Timeout = TimeSpan.FromSeconds(60));
        services.AddHttpClient<IBookingStatusClient, BookingStatusClient>(client => client.Timeout = TimeSpan.FromSeconds(60));
        services.AddHttpClient<IPayoutHoldClient, PayoutHoldClient>(client => client.Timeout = TimeSpan.FromSeconds(60));

        services.AddScoped<ISettlementGenerationRepository, SettlementGenerationRepository>();
        services.AddScoped<SettlementBuilder>();
        services.AddScoped<IRunSettlementGenerationUseCase, RunSettlementGenerationUseCase>();
        services.AddScoped<IGenerateSettlementsUseCase, GenerateSettlementsUseCase>();
        services.AddScoped<IRegenerateSettlementUseCase, RegenerateSettlementUseCase>();
        services.AddHostedService<SettlementGenerationJob>();
        return services;
    }

    /// <summary>BiweeklyAnchor phải là Thứ Hai (yyyy-MM-dd); sai → lỗi khi khởi động thay vì chia kỳ lệch ngầm.</summary>
    public static SettlementOptions ReadOptions(IConfiguration section)
    {
        var raw = section["BiweeklyAnchor"];
        var anchor = SettlementOptions.DefaultBiweeklyAnchor;
        if (!string.IsNullOrWhiteSpace(raw)
            && !DateOnly.TryParseExact(raw.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out anchor))
            throw new InvalidOperationException($"{SectionName}:BiweeklyAnchor '{raw}' phải có dạng yyyy-MM-dd.");
        if (anchor.DayOfWeek != DayOfWeek.Monday)
            throw new InvalidOperationException($"{SectionName}:BiweeklyAnchor {anchor:yyyy-MM-dd} phải là Thứ Hai.");
        var max = Math.Clamp(section.GetValue("MaxCatchUpPeriods", SettlementOptions.DefaultMaxCatchUpPeriods), 1, 500);
        return new SettlementOptions(anchor, max);
    }

    public static SettlementGatewayConnection ReadGateway(IConfiguration configuration, IConfiguration section)
    {
        var raw = configuration[SettlementGatewayConnection.BaseUrlKey];
        var value = string.IsNullOrWhiteSpace(raw) ? SettlementGatewayConnection.DefaultBaseUrl : raw.Trim().TrimEnd('/') + "/";
        if (!Uri.TryCreate(value, UriKind.Absolute, out var baseUrl) || baseUrl.Scheme is not ("http" or "https"))
            throw new InvalidOperationException($"{SettlementGatewayConnection.BaseUrlKey} không phải URL http(s) hợp lệ.");
        var seconds = section.GetValue<double?>("SourceTimeoutSeconds");
        return new SettlementGatewayConnection(baseUrl, seconds is > 0 ? TimeSpan.FromSeconds(seconds.Value) : SettlementGatewayConnection.DefaultTimeout);
    }
}
