using AdminService.Application.Features.AuditLogs;
using AdminService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using ParkingManagement.ServiceDefaults.Messaging;

namespace AdminService.Infrastructure.Persistence.Repositories;

/// <summary>
/// T-802: cài đặt port IAuditEventInboxRepository bằng AdminDbContext. InboxReceipt + AuditLog đi chung 1 lần SaveChanges
/// (1 transaction): hoặc cả hai được ghi, hoặc không gì cả. Hai request cùng EventId đến đồng thời: request sau bị khóa chính
/// PK_InboxReceipts chặn → coi như đã xử lý (Duplicate), không ghi thêm dòng AuditLogs nào.
/// </summary>
public sealed class AuditEventInboxRepository(AdminDbContext db) : IAuditEventInboxRepository
{
    public const string InboxPrimaryKey = "PK_InboxReceipts";

    public Task<bool> HasReceivedAsync(Guid eventId, CancellationToken cancellationToken)
        => db.Set<InboxReceipt>().AsNoTracking().AnyAsync(r => r.EventId == eventId, cancellationToken);

    public async Task<bool> TryRecordAsync(Guid eventId, AuditLog auditLog, DateTime receivedAtUtc, CancellationToken cancellationToken)
    {
        var receipt = new InboxReceipt { EventId = eventId, ProcessedAtUtc = receivedAtUtc };
        db.Set<InboxReceipt>().Add(receipt);
        db.AuditLogs.Add(auditLog);
        try
        {
            await db.SaveChangesAsync(cancellationToken);       // receipt + audit log: cùng 1 lần SaveChanges
            return true;
        }
        catch (DbUpdateException ex) when (IsDuplicateReceipt(ex))
        {
            // Bỏ theo dõi 2 entity chưa ghi được để DbContext của request không còn thay đổi treo.
            db.Entry(receipt).State = EntityState.Detached;
            db.Entry(auditLog).State = EntityState.Detached;
            return false;
        }
    }

    /// <summary>Chỉ unique violation trên khóa chính InboxReceipts là "đã nhận"; lỗi khác (mất kết nối...) để nổi lên → 5xx, dispatcher retry.</summary>
    public static bool IsDuplicateReceipt(DbUpdateException exception)
        => exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: InboxPrimaryKey };
}
