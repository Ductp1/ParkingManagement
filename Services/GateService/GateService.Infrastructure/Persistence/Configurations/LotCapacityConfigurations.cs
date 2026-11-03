using GateService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GateService.Infrastructure.Persistence.Configurations;

/// <summary>US-066: cấu hình mapping cho sức chứa, bộ đếm runtime và audit log (1-1 với ParkingLot, không FK liên service).</summary>
public class LotCapacityConfigConfiguration : IEntityTypeConfiguration<LotCapacityConfig>
{
    public void Configure(EntityTypeBuilder<LotCapacityConfig> builder)
    {
        builder.HasIndex(x => x.ParkingLotId).IsUnique();
        builder.Property(x => x.Version).IsConcurrencyToken();
    }
}

public class LotAdmissionCounterConfiguration : IEntityTypeConfiguration<LotAdmissionCounter>
{
    public void Configure(EntityTypeBuilder<LotAdmissionCounter> builder)
        => builder.HasIndex(x => x.ParkingLotId).IsUnique();
}

public class LotCapacityAuditLogConfiguration : IEntityTypeConfiguration<LotCapacityAuditLog>
{
    public void Configure(EntityTypeBuilder<LotCapacityAuditLog> builder)
        => builder.HasIndex(x => new { x.ParkingLotId, x.ChangedAtUtc });
}