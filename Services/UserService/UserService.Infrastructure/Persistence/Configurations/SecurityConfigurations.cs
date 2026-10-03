using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UserService.Domain.Entities;

namespace UserService.Infrastructure.Persistence.Configurations;

internal sealed class SecurityEventConfiguration : IEntityTypeConfiguration<SecurityEvent>
{
    public void Configure(EntityTypeBuilder<SecurityEvent> e)
    {
        e.ToTable("SecurityEvents");
        e.Property(x => x.Identifier).HasMaxLength(256);
        e.Property(x => x.IpAddress).HasMaxLength(45);
        e.Property(x => x.UserAgent).HasMaxLength(512);
        e.Property(x => x.Detail).HasMaxLength(1000);
        e.HasOne(x => x.User).WithMany(u => u.SecurityEvents).HasForeignKey(x => x.UserId);
        e.HasIndex(x => new { x.UserId, x.CreatedAtUtc });
        e.HasIndex(x => new { x.Identifier, x.EventType, x.CreatedAtUtc });   // đếm số lần sai trong 15 phút
    }
}

internal sealed class DataSubjectRequestConfiguration : IEntityTypeConfiguration<DataSubjectRequest>
{
    public void Configure(EntityTypeBuilder<DataSubjectRequest> e)
    {
        e.ToTable("DataSubjectRequests");
        e.Property(x => x.Reason).HasMaxLength(1000);
        e.Property(x => x.ResultNote).HasMaxLength(1000);
        e.Property(x => x.ExportFileUrl).HasMaxLength(512);
        e.HasOne(x => x.User).WithMany(u => u.DataRequests).HasForeignKey(x => x.UserId);
        e.HasIndex(x => new { x.Status, x.DueAtUtc });
        e.HasIndex(x => x.UserId);
    }
}
