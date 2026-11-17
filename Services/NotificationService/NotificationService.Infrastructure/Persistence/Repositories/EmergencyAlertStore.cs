using Microsoft.EntityFrameworkCore;
using NotificationService.Application.Features.Ingestion;
using NotificationService.Domain.Entities;
using NotificationService.Infrastructure.Persistence;
using ParkingManagement.SharedKernel.Enums;

namespace NotificationService.Infrastructure.Persistence.Repositories;

/// <summary>US-086: metadata đợt khẩn cấp + rows trạng thái gửi (người nhận × kênh).</summary>
public sealed class EmergencyAlertStore(NotificationDbContext db) : IEmergencyAlertStore, IEmergencyDeliveryQueries
{
    public async Task AddAsync(EmergencyAlert alert, CancellationToken cancellationToken = default)
    {
        db.EmergencyAlerts.Add(alert);
        await db.SaveChangesAsync(cancellationToken);
    }

    public Task<EmergencyAlert?> GetByBatchKeyAsync(string batchKey, CancellationToken cancellationToken = default)
        => db.EmergencyAlerts.FirstOrDefaultAsync(a => a.BatchKey == batchKey, cancellationToken);

    public Task<List<EmergencyAlert>> ListByLotAsync(int parkingLotId, int take, CancellationToken cancellationToken = default)
        => db.EmergencyAlerts.Where(a => a.ParkingLotId == parkingLotId)
            .OrderByDescending(a => a.Id).Take(take).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<EmergencyDeliveryRow>> GetRowsByBatchKeyAsync(string batchKey, CancellationToken cancellationToken = default)
    {
        var rows = await db.Notifications.AsNoTracking()
            .Where(n => n.BatchKey == batchKey)
            .OrderBy(n => n.UserId).ThenBy(n => n.Channel)
            .Select(n => new EmergencyDeliveryRow(
                n.UserId, n.Channel, n.Status, n.RetryCount, n.LastError, n.SentAtUtc, n.CreatedAtUtc))
            .ToListAsync(cancellationToken);
        return rows;
    }
}
