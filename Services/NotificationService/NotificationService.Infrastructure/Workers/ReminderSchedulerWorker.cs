using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NotificationService.Application.Features.Reminders;
using NotificationService.Infrastructure.Persistence.Repositories;

namespace NotificationService.Infrastructure.Workers;

/// <summary>
/// US-084: worker quét job nhắc lịch đến hạn (pattern như HoldExpiryWorker của BookingService).
/// Poll mỗi 15 giây (config Reminders:PollSeconds), mỗi vòng xử lý tối đa BatchSize job.
/// </summary>
public sealed class ReminderSchedulerWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<ReminderOptions> options,
    ILogger<ReminderSchedulerWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("ReminderSchedulerWorker đã khởi động (poll {PollSeconds}s).", options.Value.PollSeconds);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var scheduler = scope.ServiceProvider.GetRequiredService<IBookingReminderScheduler>();
                var sent = await scheduler.ProcessDueAsync(DateTime.UtcNow, stoppingToken);
                if (sent > 0)
                    logger.LogInformation("Đã gửi {Count} nhắc lịch booking.", sent);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Lỗi vòng quét nhắc lịch.");
            }

            await Task.Delay(TimeSpan.FromSeconds(Math.Clamp(options.Value.PollSeconds, 5, 300)), stoppingToken);
        }
    }
}
