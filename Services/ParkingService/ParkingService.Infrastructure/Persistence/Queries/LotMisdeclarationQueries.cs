using Microsoft.EntityFrameworkCore;
using ParkingService.Application.Abstractions;

namespace ParkingService.Infrastructure.Persistence.Queries;

/// <summary>ADAPTER cổng đọc xử lý khai báo sai (US-056): AsNoTracking + Select, đọc cả bãi Suspended.</summary>
public sealed class LotMisdeclarationQueries(ParkingDbContext db) : ILotMisdeclarationQueries
{
    public Task<MisdeclarationLotRow?> GetLotAsync(int parkingLotId, CancellationToken cancellationToken = default)
        => db.ParkingLots.AsNoTracking()
            .Where(l => l.Id == parkingLotId)
            .Select(l => new MisdeclarationLotRow(l.Id, l.Name, l.Address, l.OwnerProfileId, l.Status))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<MisdeclarationCaseRow>> ListCasesAsync(int parkingLotId,
        CancellationToken cancellationToken = default)
        => await db.LotMisdeclarationCases.AsNoTracking()
            .Where(c => c.ParkingLotId == parkingLotId)
            .OrderByDescending(c => c.OpenedAtUtc)
            .ThenByDescending(c => c.Id)
            .Select(c => new MisdeclarationCaseRow(c.Id, c.Severity, c.Category, c.Status, c.Finding, c.DueAtUtc, c.OpenedByUserId,
                c.OpenedAtUtc, c.EscalationNote, c.EscalatedAtUtc, c.ClosedByUserId, c.ClosedAtUtc, c.ResolutionNote))
            .ToListAsync(cancellationToken);
}
