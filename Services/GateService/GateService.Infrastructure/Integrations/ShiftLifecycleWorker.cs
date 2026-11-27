using GateService.Application.Features.Shifts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace GateService.Infrastructure.Integrations;

/// <summary>US-076: mỗi phút đánh dấu ca vắng, tự đóng ca quên End và bù tổng tiền mặt còn thiếu.</summary>
public sealed class ShiftLifecycleWorker(IServiceScopeFactory scopes, ILogger<ShiftLifecycleWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    await using var scope = scopes.CreateAsyncScope();
                    await scope.ServiceProvider.GetRequiredService<ShiftLifecycleUseCases>().RunDueAsync(stoppingToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogError(ex, "US-076: lượt quét ca trực thất bại, thử lại ở lượt sau.");
                }
            }
        }
        catch (OperationCanceledException) { }
    }
}
