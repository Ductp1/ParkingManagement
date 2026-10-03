using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NotificationService.Domain.Entities;

namespace NotificationService.Infrastructure.Persistence.Configurations;

internal sealed class NotificationPreferenceConfiguration : IEntityTypeConfiguration<NotificationPreference>
{
    public void Configure(EntityTypeBuilder<NotificationPreference> e)
    {
        e.ToTable("NotificationPreferences");
        e.Property(x => x.TemplateKey).HasMaxLength(50).IsRequired();
        e.HasIndex(x => new { x.UserId, x.TemplateKey, x.Channel }).IsUnique();
    }
}

internal sealed class DeviceTokenConfiguration : IEntityTypeConfiguration<DeviceToken>
{
    public void Configure(EntityTypeBuilder<DeviceToken> e)
    {
        e.ToTable("DeviceTokens");
        e.Property(x => x.Token).HasMaxLength(512).IsRequired();
        e.Property(x => x.DeviceName).HasMaxLength(100);
        e.HasIndex(x => x.Token).IsUnique();
        e.HasIndex(x => new { x.UserId, x.IsRevoked });
    }
}
