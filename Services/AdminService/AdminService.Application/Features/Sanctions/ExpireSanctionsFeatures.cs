using System.Text.Json;
using AdminService.Domain.Entities;
using Microsoft.Extensions.Logging;
using ParkingManagement.SharedKernel.Exceptions;

namespace AdminService.Application.Features.Sanctions;

public interface IExpireSanctionsUseCase
{
    /// <summary>Đóng tối đa <paramref name="batchSize"/> chế tài tới hạn; trả số chế tài đã đóng.</summary>
    Task<int> ExecuteAsync(int batchSize, CancellationToken cancellationToken = default);
}

/// <summary>
/// US-097: SanctionExpiryJob gọi định kỳ. Chế tài Active quá EndsAtUtc → Expired + audit "Sanction.Expired" (1 transaction, chỉ khi DB
/// còn Active – job chạy trùng không đóng 2 lần) → báo chủ bãi hết hạn.
///  - ParkingService: không cần gọi, hiệu lực tự hết theo thời gian.
///  - Khóa chủ bãi: job không có JWT Admin để gọi UserService mở khóa → UserServiceSyncStatus = Pending, Admin bấm "đồng bộ lại".
/// </summary>
public sealed class ExpireSanctionsUseCase(
    ISanctionRepository repository,
    IParkingSanctionClient parking,
    INotificationClient notifications,
    TimeProvider timeProvider,
    ILogger<ExpireSanctionsUseCase> logger) : IExpireSanctionsUseCase
{
    private readonly SanctionSyncCore sync = new(parking, notifications, timeProvider, logger);

    public async Task<int> ExecuteAsync(int batchSize, CancellationToken cancellationToken = default)
    {
        if (batchSize <= 0) throw new ValidationException("batchSize phải là số nguyên dương.");

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var due = await repository.FindDueForExpiryTrackedAsync(now, batchSize, cancellationToken);
        var expired = 0;
        foreach (var sanction in due)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!await SanctionExpiry.TryExpireAsync(repository, sanction, performedByUserId: null, cancellationToken))
            {
                logger.LogInformation("Chế tài #{SanctionId} đã được đóng bởi tiến trình khác, bỏ qua.", sanction.Id);
                continue;
            }

            expired++;
            await sync.NotifyAsync(sanction, cancellationToken);
            await repository.SaveAsync(cancellationToken);
        }

        if (expired > 0) logger.LogInformation("Đã đóng {Count} chế tài SLA tới hạn.", expired);
        return expired;
    }
}

/// <summary>Đóng chế tài tới hạn – dùng chung cho job và "đồng bộ lại" của Admin.</summary>
internal static class SanctionExpiry
{
    private static readonly JsonSerializerOptions AuditJson = new(JsonSerializerDefaults.Web);

    public static Task<bool> TryExpireAsync(ISanctionRepository repository, Sanction sanction, int? performedByUserId,
        CancellationToken cancellationToken)
    {
        var auditLog = new AuditLog
        {
            SourceService = "AdminService",
            UserId = performedByUserId,
            OwnerProfileId = sanction.OwnerProfileId,
            Action = "Sanction.Expired",
            EntityName = "Sanction",
            EntityId = sanction.Id.ToString(),
            OldValuesJson = JsonSerializer.Serialize(new { status = sanction.Status.ToString(), endsAtUtc = sanction.EndsAtUtc }, AuditJson),
            NewValuesJson = JsonSerializer.Serialize(new { status = "Expired" }, AuditJson),
            Reason = $"Chế tài #{sanction.Id} hết hạn.",
        };
        sanction.ExpireOnSchedule();
        return repository.TryExpireAsync(sanction, auditLog, cancellationToken);
    }
}
