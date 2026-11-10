using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NotificationService.Application.Features;
using NotificationService.Application.Features.DeviceTokens;
using NotificationService.Application.Features.Dispatcher;
using NotificationService.Application.Features.Ingestion;
using NotificationService.Application.Features.Notifications;
using NotificationService.Application.Features.Preferences;
using NotificationService.Application.Features.Reminders;
using NotificationService.Infrastructure.Integrations;
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
using ParkingManagement.ServiceDefaults.Messaging;
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

        // ===== US-083→086 foundation: renderer i18n + settings ngôn ngữ + consumer integration events =====
        services.AddScoped<ITemplateRenderer, DbTemplateRenderer>();
        services.AddScoped<IUserNotificationSettingsPort, UserNotificationSettingsRepository>();
        services.AddScoped<ITransactionalNotificationSender, DispatcherTransactionalNotificationSender>();
        services.AddScoped<IBookingConfirmationNotifications, BookingConfirmationNotifications>();
        services.AddScoped<IOwnerBookingNotifications, OwnerBookingNotifications>();
        // ===== US-086: cảnh báo khẩn cấp đa kênh + tracking trạng thái gửi =====
        services.AddScoped<ILotEmergencyAlertNotifications, LotEmergencyAlertNotifications>();
        services.AddScoped<Application.Features.Tracking.IDeliveryStatusReadStore, NotificationService.Infrastructure.Persistence.Queries.NotificationDeliveryStatusStore>();
        services.AddScoped<Application.Features.Tracking.IGetDeliveryStatusUseCase, Application.Features.Tracking.GetDeliveryStatusUseCase>();

        // ===== US-084: nhắc lịch booking (đặt/hủy/reschedule job + worker quét due) =====
        services.Configure<ReminderOptions>(configuration.GetSection(ReminderOptions.SectionName));
        services.AddScoped<Application.Features.Reminders.IReminderJobRepository, ReminderJobRepository>();
        services.AddScoped<Application.Features.Reminders.IBookingReminderScheduler, Application.Features.Reminders.BookingReminderScheduler>();
        services.AddHostedService<Workers.ReminderSchedulerWorker>();

        // MassTransit + RabbitMQ: queue "notification.service" nhận event booking/payment/lot theo routing key
        // (= EventType). TransactionalInbox chặn xử lý trùng theo EventId (idempotency US-083).
        services.AddRabbitMqInboxConsumer<NotificationDbContext, NotificationIntegrationEventHandler>(configuration);

        return services;
    }
}
