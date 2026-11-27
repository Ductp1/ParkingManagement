using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using UserService.Application.Features.Identity;
using UserService.Infrastructure.Persistence;

namespace UserService.Infrastructure.Storage;

// Chỉ dọn token đã hết hạn lâu; giữ đủ chuỗi để phát hiện replay khi phiên còn hoạt động.
public sealed class IdentityRetentionWorker(IServiceScopeFactory scopes,TimeProvider clock,
    ILogger<IdentityRetentionWorker> logger):BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while(!ct.IsCancellationRequested)
        {
            try
            {
                await using var scope=scopes.CreateAsyncScope();var db=scope.ServiceProvider.GetRequiredService<UserDbContext>();
                var now=clock.GetUtcNow().UtcDateTime;
                await db.RefreshTokens.Where(t=>t.ExpiresAtUtc<now.AddDays(-7)).ExecuteDeleteAsync(ct);
                await db.OtpCodes.Where(o=>o.ExpiresAtUtc<now.AddDays(-1)).ExecuteDeleteAsync(ct);
                await db.OutboxMessages.Where(m=>m.EventType=="OtpDeliveryRequested"&&m.ProcessedAtUtc<now.AddDays(-1)).ExecuteDeleteAsync(ct);
                await db.SaveChangesAsync(ct);
            }
            catch(OperationCanceledException)when(ct.IsCancellationRequested){break;}
            catch(Exception ex){logger.LogError(ex,"Không hoàn tất dọn dữ liệu xác thực hết hạn.");}
            try{await Task.Delay(TimeSpan.FromHours(24),ct);}catch(OperationCanceledException){break;}
        }
    }
}
