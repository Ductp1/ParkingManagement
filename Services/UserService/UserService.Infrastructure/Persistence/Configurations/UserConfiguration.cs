using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ParkingManagement.ServiceDefaults.Persistence;
using ParkingManagement.SharedKernel.Enums;
using UserService.Domain.Entities;

namespace UserService.Infrastructure.Persistence.Configurations;

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> e)
    {
        e.ToTable("Users");
        e.Property(x => x.FullName).HasMaxLength(150).IsRequired();
        e.Property(x => x.Email).HasMaxLength(256);
        e.Property(x => x.PhoneNumber).HasMaxLength(20);
        e.Property(x => x.PasswordHash).HasMaxLength(200).IsRequired();
        e.Property(x => x.KycNumberEncrypted).HasMaxLength(512);
        e.Property(x => x.KycFrontImageUrl).HasMaxLength(512);
        e.Property(x => x.KycBackImageUrl).HasMaxLength(512);
        e.Property(x => x.AvatarUrl).HasMaxLength(512);

        // Đăng ký bằng SĐT hoặc Email → mỗi giá trị là duy nhất nếu có.
        e.HasIndex(x => x.Email).IsUnique().HasFilter("[Email] IS NOT NULL");
        e.HasIndex(x => x.PhoneNumber).IsUnique().HasFilter("[PhoneNumber] IS NOT NULL");
        e.HasIndex(x => x.Status);
        e.ToTable(t => t.HasCheckConstraint("CK_Users_EmailOrPhone", "[Email] IS NOT NULL OR [PhoneNumber] IS NOT NULL"));
    }
}
