using GateService.Application.Features.PmsIntegration;
using GateService.Infrastructure.Persistence;
using ParkingManagement.ServiceDefaults.Messaging;
using ParkingManagement.ServiceDefaults.Persistence;

namespace GateService.Infrastructure.Persistence;

/// <summary>Port IPmsInbox cài bằng TransactionalInbox (bảng InboxReceipts + advisory lock) – chống xử lý trùng.</summary>
public sealed class PmsInbox(GateDbContext db) : IPmsInbox
{
    public Task<bool> ApplyOnceAsync(Guid eventId, Func<CancellationToken, Task> apply, CancellationToken cancellationToken)
        => TransactionalInbox.ApplyOnceAsync(db, eventId, apply, cancellationToken);
}