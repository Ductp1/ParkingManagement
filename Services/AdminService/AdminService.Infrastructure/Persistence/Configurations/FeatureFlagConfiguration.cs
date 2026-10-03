using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ParkingManagement.ServiceDefaults.Persistence;
using ParkingManagement.SharedKernel.Enums;
using AdminService.Domain.Entities;

namespace AdminService.Infrastructure.Persistence.Configurations;

internal sealed class FeatureFlagConfiguration : IEntityTypeConfiguration<FeatureFlag>
{
    public void Configure(EntityTypeBuilder<FeatureFlag> e)
    {
        e.ToTable("FeatureFlags");
        e.Property(x => x.Key).HasMaxLength(100).IsRequired();
        e.HasIndex(x => x.Key).IsUnique();

        var at = SeedClock.SeededAtUtc;
        var id = 0;
        object F(string key, bool enabled, string description) => new
        {
            Id = ++id, Key = key, IsEnabled = enabled, Description = description, CreatedAtUtc = at
        };

        e.HasData(
            F("FEATURE_AI_LPR", true, "Nhận diện biển số OCR tại cổng (có trong demo MVP)"),
            F("FEATURE_3D_MAP", false, "Sơ đồ 2.5D/3D – stretch"),
            F("FEATURE_AI_VOICE_SEARCH", false, "Tìm bãi bằng giọng nói – stretch"),
            F("FEATURE_AI_CHATBOT", false, "Chatbot FAQ – stretch"),
            F("FEATURE_SPONSORED_LISTING", false, "Bãi quảng cáo trên kết quả tìm kiếm – Phase 2"),
            F("FEATURE_MONTHLY_PASS", false, "Vé tháng – Phase 2"),
            F("FEATURE_VNPAY", true, "Thanh toán online qua VNPAY sandbox"),
            F("FEATURE_VIETQR_AT_GATE", true, "Thanh toán VietQR động tại cổng ra"));
    }
}
