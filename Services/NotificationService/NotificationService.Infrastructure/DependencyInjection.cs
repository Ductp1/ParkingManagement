using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NotificationService.Application.Features;
using NotificationService.Application.Features.Dispatcher;
using NotificationService.Infrastructure.Persistence;
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

        // Queries
        services.AddScoped<INotificationQueries, NotificationQueries>();

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
        services.AddScoped<ISmsNotificationSender, SmsNotificationSender>();
        services.AddScoped<IFcmNotificationSender, FcmNotificationSender>();
        services.AddScoped<IInAppNotificationSender, InAppNotificationSender>();

        // Dispatcher orchestrator
        services.AddScoped<INotificationDispatcher, NotificationDispatcher>();

        return services;
    }
}
