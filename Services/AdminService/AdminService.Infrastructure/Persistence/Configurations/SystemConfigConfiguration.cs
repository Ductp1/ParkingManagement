using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ParkingManagement.ServiceDefaults.Persistence;
using ParkingManagement.SharedKernel.Enums;
using AdminService.Domain.Entities;

namespace AdminService.Infrastructure.Persistence.Configurations;

internal sealed class SystemConfigConfiguration : IEntityTypeConfiguration<SystemConfig>
{
    public void Configure(EntityTypeBuilder<SystemConfig> e)
    {
        e.ToTable("SystemConfigs");
        e.Property(x => x.Key).HasMaxLength(100).IsRequired();
        e.Property(x => x.Value).HasMaxLength(1000).IsRequired();
        e.Property(x => x.DataType).HasMaxLength(20).IsRequired();
        e.HasIndex(x => x.Key).IsUnique();

        // Giá trị mặc định theo sheet "Vấn đề cần chốt" + Final Business Decisions v3.
        var at = SeedClock.SeededAtUtc;
        var id = 0;
        object C(string key, string value, string type, string description) => new
        {
            Id = ++id, Key = key, Value = value, DataType = type, Description = description, CreatedAtUtc = at
        };

        e.HasData(
            C("BOOKING_HOLD_MINUTES", "15", "int", "Thời gian giữ chỗ chờ thanh toán"),
            C("CHECKIN_GRACE_PERIOD_MINUTES", "15", "int", "Ân hạn chờ tài xế đến trễ"),
            C("PARKING_BILLING_GRACE_PERIOD_MINUTES", "15", "int", "Ân hạn miễn phí tính cước mỗi lượt"),
            C("CANCELLATION_WINDOW_MINUTES", "60", "int", "Hủy trước ≥ 60 phút hoàn 100%, sau đó hoàn 0%"),
            C("NO_SHOW_CANCEL_AFTER_MINUTES", "30", "int", "Staff được hủy no-show sau mốc này"),
            C("MANUAL_LOT_APPROVAL_MINUTES", "10", "int", "Bãi Mức 0 phải duyệt booking trong thời gian này"),
            C("DEFAULT_COMMISSION_RATE", "0.10", "decimal", "Hoa hồng mặc định trên giao dịch hoàn tất"),
            C("SETTLEMENT_CYCLE", "WEEKLY", "string", "Chu kỳ quyết toán cho chủ bãi"),
            C("OTP_EXPIRY_SECONDS", "300", "int", "Hiệu lực mã OTP"),
            C("OTP_MAX_ATTEMPTS", "3", "int", "Số lần nhập sai OTP trước khi khóa"),
            C("OTP_LOCK_MINUTES", "15", "int", "Thời gian khóa sau khi sai OTP"),
            C("JWT_ACCESS_TOKEN_HOURS", "24", "int", "Hiệu lực access token"),
            C("JWT_REFRESH_TOKEN_DAYS", "7", "int", "Hiệu lực refresh token"),
            C("MAX_VEHICLES_PER_USER", "10", "int", "Số xe tối đa trong Garage cá nhân"),
            C("SEARCH_RADIUS_KM", "5", "decimal", "Bán kính tìm bãi mặc định"),
            C("HEIGHT_CLEARANCE_MARGIN_CM", "10", "int", "Biên an toàn chiều cao xe so với trần"),
            C("HEARTBEAT_INTERVAL_SECONDS", "60", "int", "Chu kỳ kiểm tra kết nối bãi"),
            C("HEARTBEAT_MAX_FAILURES", "3", "int", "Số lần mất kết nối trước khi đánh dấu Stale"),
            C("OCR_AUTO_OPEN_MIN_CONFIDENCE", "0.90", "decimal", "Ngưỡng tự mở barie theo kết quả OCR"),
            C("OWNER_DISPUTE_RESPONSE_HOURS", "48", "int", "Hạn chủ bãi phản hồi tranh chấp"),
            C("COMPLAINT_WINDOW_DAYS", "7", "int", "Hạn gửi khiếu nại sau sự việc"),
            C("LAYOUT_UPDATE_DEADLINE_HOURS", "48", "int", "Hạn cập nhật sơ đồ sau thay đổi thực tế"),
            C("EV_IDLE_FEE_PER_15_MINUTES", "20000", "decimal", "Phí chiếm dụng trụ sạc sau 30 phút sạc đầy"),
            C("API_RATE_LIMIT_PER_MINUTE", "100", "int", "Giới hạn request/phút cho mỗi client"));
    }
}
