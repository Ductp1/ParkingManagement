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

/// <summary>US-084: job nhắc lịch — poll theo (Status, DueAtUtc); 1 job Pending duy nhất mỗi (BookingId, Kind).</summary>
internal sealed class ReminderJobConfiguration : IEntityTypeConfiguration<ReminderJob>
{
    public void Configure(EntityTypeBuilder<ReminderJob> e)
    {
        e.ToTable("ReminderJobs");
        e.Property(x => x.BookingCode).HasMaxLength(50).IsRequired();
        e.Property(x => x.LotName).HasMaxLength(200).IsRequired();
        e.HasIndex(x => new { x.Status, x.DueAtUtc });
        e.HasIndex(x => new { x.BookingId, x.Kind, x.Status })
            .IsUnique()
            .HasFilter("\"Status\" = 'Pending'");
    }
}
