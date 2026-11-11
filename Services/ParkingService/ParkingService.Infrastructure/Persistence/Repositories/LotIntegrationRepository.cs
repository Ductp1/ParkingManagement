using Microsoft.EntityFrameworkCore;
using ParkingService.Application.Abstractions;
using ParkingService.Domain.Entities;

namespace ParkingService.Infrastructure.Persistence.Repositories;

/// <summary>ADAPTER cài ILotIntegrationRepository bằng EF Core (US-052).</summary>
public sealed class LotIntegrationRepository(ParkingDbContext db) : ILotIntegrationRepository
{
    public Task<LotIntegration?> GetByApiKeyPrefixAsync(string apiKeyPrefix, CancellationToken cancellationToken = default)
        => db.LotIntegrations.FirstOrDefaultAsync(i => i.ApiKeyPrefix == apiKeyPrefix, cancellationToken);

    public Task<LotIntegration?> GetByLotIdAsync(int parkingLotId, CancellationToken cancellationToken = default)
        => db.LotIntegrations.FirstOrDefaultAsync(i => i.ParkingLotId == parkingLotId, cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) => db.SaveChangesAsync(cancellationToken);
}
