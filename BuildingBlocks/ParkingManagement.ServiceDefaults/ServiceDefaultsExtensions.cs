using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ParkingManagement.ServiceDefaults.Middleware;
using ParkingManagement.ServiceDefaults.Persistence;

namespace ParkingManagement.ServiceDefaults;

/// <summary>
/// Cấu hình mặc định cho mọi service – Program.cs của mỗi service chỉ cần vài dòng:
/// <code>
/// builder.AddServiceDefaults("ParkingService");
/// builder.Services.AddServiceDbContext&lt;ParkingDbContext&gt;(builder.Configuration);
/// ...
/// var app = builder.Build();
/// await app.InitializeDatabaseAsync&lt;ParkingDbContext&gt;();
/// app.UseServiceDefaults();
/// app.Run();
/// </code>
/// </summary>
public static class ServiceDefaultsExtensions
{
    public const string ConnectionStringName = "ServiceDb";
    public const string FrontendCorsPolicy = "Frontend";

    public static WebApplicationBuilder AddServiceDefaults(this WebApplicationBuilder builder, string serviceName)
    {
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddControllers()
            .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
        builder.Services.AddProblemDetails();
        builder.Services.AddOpenApi();                         // http://localhost:<port>/openapi/v1.json
        builder.Services.AddHealthChecks();                    // /health – Gateway & run-all dùng để kiểm tra service sống
        builder.Services.AddCors(o => o.AddPolicy(FrontendCorsPolicy, p => p
            .WithOrigins(builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? ["http://localhost:5173"])
            .AllowAnyHeader().AllowAnyMethod().AllowCredentials()));

        builder.Services.AddSingleton(new ServiceInfo(serviceName));
        return builder;
    }

    /// <summary>Đăng ký DbContext của service với connection string "ConnectionStrings:ServiceDb".</summary>
    public static IServiceCollection AddServiceDbContext<TContext>(this IServiceCollection services, IConfiguration configuration)
        where TContext : DbContext
    {
        var connectionString = configuration.GetConnectionString(ConnectionStringName)
            ?? throw new InvalidOperationException($"Thiếu ConnectionStrings:{ConnectionStringName} trong appsettings.json.");

        // Không bật EnableRetryOnFailure: nó chặn transaction tự mở (BeginTransaction),
        // trong khi Booking/Gate cần transaction để chống đặt trùng slot.
        services.AddDbContext<TContext>(o => o
            .UseSqlServer(connectionString)
            // Bảng soft delete (Users, ParkingLots...) là đầu "bắt buộc" của quan hệ trong service – thiết kế có chủ đích.
            .ConfigureWarnings(w => w.Ignore(CoreEventId.PossibleIncorrectRequiredNavigationWithQueryFilterInteractionWarning)));
        return services;
    }

    public static WebApplication UseServiceDefaults(this WebApplication app)
    {
        var info = app.Services.GetRequiredService<ServiceInfo>();

        app.UseMiddleware<ExceptionHandlingMiddleware>();
        app.UseCors(FrontendCorsPolicy);
        app.MapOpenApi();
        app.MapHealthChecks("/health");
        app.MapControllers();
        app.MapGet("/", () => Results.Ok(new { service = info.Name, status = "running" }));
        return app;
    }

    /// <summary>
    /// Khi khởi động: tạo/cập nhật database của service theo migration và nạp dữ liệu demo (nếu bật).
    /// appsettings.json → "Database": { "ApplyMigrationsOnStartup": true, "SeedDemoData": true }
    /// </summary>
    public static async Task InitializeDatabaseAsync<TContext>(this WebApplication app) where TContext : DbContext
    {
        static bool IsOn(string? v) => bool.TryParse(v, out var b) && b;

        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DatabaseInitializer");

        if (IsOn(app.Configuration["Database:ApplyMigrationsOnStartup"]))
        {
            logger.LogInformation("Migrate {Database}...", db.Database.GetDbConnection().Database);
            await db.Database.MigrateAsync();
        }

        if (IsOn(app.Configuration["Database:SeedDemoData"]))
        {
            var seeder = scope.ServiceProvider.GetService<IDataSeeder<TContext>>();
            if (seeder is not null)
                await seeder.SeedAsync(db, CancellationToken.None);
        }
    }
}

public sealed record ServiceInfo(string Name);
