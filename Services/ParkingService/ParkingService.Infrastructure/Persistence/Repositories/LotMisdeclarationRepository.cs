using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using ParkingManagement.SharedKernel.Contracts;
using ParkingManagement.SharedKernel.Domain;
using ParkingManagement.SharedKernel.Exceptions;
using ParkingService.Application.Abstractions;
using ParkingService.Domain.Entities;
using ParkingService.Domain.Enums;

namespace ParkingService.Infrastructure.Persistence.Repositories;

/// <summary>ADAPTER cổng ghi xử lý khai báo sai (US-056): case, bãi và outbox lưu trong một SaveChanges.</summary>
public sealed class LotMisdeclarationRepository(ParkingDbContext db) : ILotMisdeclarationRepository
{
    public Task<ParkingLot?> GetLotAsync(int parkingLotId, CancellationToken cancellationToken = default)
        => db.ParkingLots.FirstOrDefaultAsync(l => l.Id == parkingLotId, cancellationToken);

    public Task<LotMisdeclarationCase?> GetCaseAsync(int parkingLotId, int caseId, CancellationToken cancellationToken = default)
        => db.LotMisdeclarationCases
            .Include(c => c.ParkingLot)
            .FirstOrDefaultAsync(c => c.Id == caseId && c.ParkingLotId == parkingLotId, cancellationToken);

    public Task<bool> HasOpenCaseAsync(int parkingLotId, CancellationToken cancellationToken = default)
        => db.LotMisdeclarationCases.AnyAsync(c => c.ParkingLotId == parkingLotId
            && (c.Status == MisdeclarationCaseStatus.Warned || c.Status == MisdeclarationCaseStatus.Suspended), cancellationToken);

    public void Add(LotMisdeclarationCase misdeclarationCase) => db.LotMisdeclarationCases.Add(misdeclarationCase);

    public async Task SaveWithEventsAsync(IReadOnlyList<IntegrationEvent> events, CancellationToken cancellationToken = default)
    {
        foreach (var ev in events)
        {
            db.OutboxMessages.Add(new OutboxMessage
            {
                Id = ev.EventId,
                EventType = ev.GetType().Name,
                OccurredAtUtc = ev.OccurredAtUtc,
                PayloadJson = JsonSerializer.Serialize(ev, ev.GetType()),
            });
        }

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConflictException("Case khai báo sai vừa được Admin khác cập nhật, vui lòng tải lại và thử lại.");
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            throw new ConflictException("Bãi đã có case khai báo sai đang mở (Admin khác vừa tạo), vui lòng tải lại.");
        }
    }
}
