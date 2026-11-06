using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NotificationService.Application.Features;
using NotificationService.Application.Features.DeviceTokens;
using NotificationService.Application.Features.Dispatcher;
using NotificationService.Application.Features.Notifications;
using NotificationService.Application.Features.Preferences;
using NotificationService.Infrastructure.Persistence;
using NotificationService.Infrastructure.Services.UserDirectory;
using NotificationService.Infrastructure.Persistence.Queries;
using NotificationService.Infrastructure.Persistence.Repositories;
using NotificationService.Infrastructure.Persistence.Seeding;
using NotificationService.Infrastructure.Services.Dispatcher;
using NotificationService.Infrastructure.Services.EmailSender;
using NotificationService.Infrastructure.Services.FcmSender;
using NotificationService.Infrastructure.Services.InAppSender;
using NotificationService.Infrastructure.Services.SmsSender;
using ParkingManagement.ServiceDefaults;
using ParkingManagement.ServiceDefaults.Persistence;

namespace NotificationService.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddNotificationInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // Database
        services.AddServiceDbContext<NotificationDbContext>(configuration);

        // Repositories
        services.AddScoped<INotificationRepository, NotificationRepository>();
        services.AddScoped<IDeviceTokenRepository, DeviceTokenRepository>();
        services.AddScoped<INotificationPreferenceRepository, NotificationPreferenceRepository>();
        services.AddScoped<INotificationCommands, NotificationCommands>();

        // Queries
        services.AddScoped<INotificationQueries, NotificationQueries>();

        // User directory – tra cứu email/phone của user qua UserService API
        var userServiceBaseUrl = configuration["UserService:BaseUrl"] ?? "http://localhost:5101";
        services.AddHttpClient<IUserDirectory, UserServiceDirectory>(c => c.BaseAddress = new Uri(userServiceBaseUrl));

        // Seeding
        services.AddScoped<IDataSeeder<NotificationDbContext>, NotificationDataSeeder>();

        // ===== S1-T602: Notification Dispatcher & Channel Senders =====

        // Load configurations from appsettings
        var smtpConfig = configuration.GetSection("SmtpConfiguration").Get<SmtpConfiguration>() ?? new SmtpConfiguration();
        var smsConfig = configuration.GetSection("SmsConfiguration").Get<SmsConfiguration>() ?? new SmsConfiguration();
        var fcmConfig = configuration.GetSection("FcmConfiguration").Get<FcmConfiguration>() ?? new FcmConfiguration();

        services.AddSingleton(smtpConfig);
        services.AddSingleton(smsConfig);
        services.AddSingleton(fcmConfig);

        // Channel-specific senders
        services.AddScoped<IEmailNotificationSender, EmailNotificationSender>();
        services.AddHttpClient<ISmsNotificationSender, SmsNotificationSender>();      // Twilio REST API
        services.AddHttpClient<IFcmNotificationSender, FcmNotificationSender>();      // FCM HTTP v1 + OAuth2
        services.AddScoped<IInAppNotificationSender, InAppNotificationSender>();

        // Dispatcher orchestrator
        services.AddScoped<INotificationDispatcher, NotificationDispatcher>();

        return services;
    }
}
