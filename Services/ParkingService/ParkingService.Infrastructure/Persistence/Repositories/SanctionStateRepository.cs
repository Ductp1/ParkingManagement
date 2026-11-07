using Microsoft.EntityFrameworkCore;
using Npgsql;
using ParkingManagement.SharedKernel.Exceptions;
using ParkingService.Application.Abstractions;
using ParkingService.Domain.Entities;

namespace ParkingService.Infrastructure.Persistence.Repositories;

/// <summary>US-097: ghi hiệu lực chế tài đẩy từ AdminService.</summary>
public sealed class SanctionStateRepository(ParkingDbContext db) : ISanctionStateRepository
{
    public Task<SanctionState?> FindBySanctionIdAsync(int sanctionId, CancellationToken cancellationToken = default)
        => db.Set<SanctionState>().FirstOrDefaultAsync(s => s.SanctionId == sanctionId, cancellationToken);

    public void Add(SanctionState state) => db.Set<SanctionState>().Add(state);

    public async Task SaveAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            throw new ConflictException("Chế tài vừa được đồng bộ bởi một request khác, vui lòng thử lại.");
        }
    }
}
