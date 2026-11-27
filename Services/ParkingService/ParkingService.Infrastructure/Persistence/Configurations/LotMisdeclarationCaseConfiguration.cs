using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ParkingService.Domain.Entities;
using ParkingService.Domain.Enums;

namespace ParkingService.Infrastructure.Persistence.Configurations;

internal sealed class LotMisdeclarationCaseConfiguration : IEntityTypeConfiguration<LotMisdeclarationCase>
{
    public void Configure(EntityTypeBuilder<LotMisdeclarationCase> e)
    {
        e.ToTable("LotMisdeclarationCases");
        e.Property(x => x.Finding).HasMaxLength(LotMisdeclarationCase.TextMaxLength);
        e.Property(x => x.EscalationNote).HasMaxLength(LotMisdeclarationCase.TextMaxLength);
        e.Property(x => x.ResolutionNote).HasMaxLength(LotMisdeclarationCase.TextMaxLength);
        e.Property(x => x.RowVersion).IsConcurrencyToken();   // hai Admin thao tác cùng case → người sau 409
        e.HasOne(x => x.ParkingLot).WithMany().HasForeignKey(x => x.ParkingLotId);
        e.HasIndex(x => new { x.ParkingLotId, x.OpenedAtUtc });
        e.HasIndex(x => x.OwnerProfileId);
        // Mỗi bãi tối đa 1 case đang mở: hai Admin mở cùng lúc → request sau bị index chặn → 409.
        e.HasIndex(x => x.ParkingLotId)
            .IsUnique()
            .HasFilter($"\"Status\" IN ('{nameof(MisdeclarationCaseStatus.Warned)}', '{nameof(MisdeclarationCaseStatus.Suspended)}')")
            .HasDatabaseName("UX_LotMisdeclarationCases_OpenPerLot");
    }
}
