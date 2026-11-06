using Microsoft.EntityFrameworkCore;
using ParkingManagement.ServiceDefaults.Persistence;

namespace ParkingManagement.ServiceDefaults.Messaging;

public sealed class InboxReceipt
{
    public Guid EventId { get; set; }
    public DateTime ProcessedAtUtc { get; set; }
}

public static class TransactionalInbox
{
    public static void ConfigureInbox(this ModelBuilder model)
    {
        model.Entity<InboxReceipt>().ToTable("InboxReceipts").HasKey(x => x.EventId);
    }
    public static async Task<bool> ApplyOnceAsync(this ServiceDbContext db, Guid eventId,
        Func<CancellationToken, Task> apply, CancellationToken ct)
    {
        if (eventId == Guid.Empty) throw new ArgumentException("EventId không được rỗng.", nameof(eventId));
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var key = BitConverter.ToInt64(eventId.ToByteArray(), 0);
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({key})", ct);
        if (await db.Set<InboxReceipt>().AnyAsync(r => r.EventId == eventId, ct)) { await tx.CommitAsync(ct); return false; }
        await apply(ct);
        db.Add(new InboxReceipt { EventId = eventId, ProcessedAtUtc = DateTime.UtcNow });
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return true;
    }
}
