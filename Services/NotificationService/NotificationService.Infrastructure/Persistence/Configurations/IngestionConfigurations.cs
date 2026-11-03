using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NotificationService.Domain.Entities;

namespace NotificationService.Infrastructure.Persistence.Configurations;

/// <summary>US-083 foundation: settings ngôn ngữ 1 row/user + DedupeKey chống gửi trùng.</summary>
internal sealed class UserNotificationSettingsConfiguration : IEntityTypeConfiguration<UserNotificationSettings>
{
    public void Configure(EntityTypeBuilder<UserNotificationSettings> e)
    {
        e.ToTable("UserNotificationSettings");
        e.Property(x => x.Language).HasMaxLength(5).IsRequired();
        e.HasIndex(x => x.UserId).IsUnique();
    }
}

internal sealed class NotificationDedupeConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> e)
    {
        // Unique khi có giá trị (NULL cho phép trùng): replay cùng EventId → insert thứ 2 bị chặn ở DB.
        e.HasIndex(x => x.DedupeKey).IsUnique().HasFilter("\"DedupeKey\" IS NOT NULL");
    }
}
