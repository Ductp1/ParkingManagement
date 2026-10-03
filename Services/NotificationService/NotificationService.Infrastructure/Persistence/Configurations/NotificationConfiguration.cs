using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ParkingManagement.ServiceDefaults.Persistence;
using ParkingManagement.SharedKernel.Enums;
using NotificationService.Domain.Entities;

namespace NotificationService.Infrastructure.Persistence.Configurations;

internal sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> e)
    {
        e.ToTable("Notifications");
        e.Property(x => x.TemplateKey).HasMaxLength(50).IsRequired();
        e.Property(x => x.Title).HasMaxLength(200).IsRequired();
        e.Property(x => x.Body).HasMaxLength(2000).IsRequired();
        e.Property(x => x.DataJson).IsMaxText();
        e.Property(x => x.LastError).HasMaxLength(1000);
        e.HasIndex(x => new { x.UserId, x.IsRead, x.CreatedAtUtc });   // chuông thông báo
        e.HasIndex(x => new { x.Status, x.RetryCount });                // worker gửi lại
    }
}
