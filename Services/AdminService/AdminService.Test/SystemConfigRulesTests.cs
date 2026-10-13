using AdminService.Application.Features.Settings;
using AdminService.Domain.Entities;
using AdminService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using ParkingManagement.SharedKernel.Exceptions;

namespace AdminService.Test;

// US-098: luật kiểm tra giá trị tham số theo kiểu dữ liệu và theo từng khóa (hàm thuần).
public class SystemConfigRulesTests
{
    [Theory]
    [InlineData("BOOKING_HOLD_MINUTES", "int", "20", "20")]
    [InlineData("BOOKING_HOLD_MINUTES", "int", "  020 ", "20")]                 // trim + bỏ số 0 đầu
    [InlineData("BOOKING_HOLD_MINUTES", "int", "1", "1")]                       // đúng biên dưới
    [InlineData("BOOKING_HOLD_MINUTES", "int", "120", "120")]                   // đúng biên trên
    [InlineData("CHECKIN_GRACE_PERIOD_MINUTES", "int", "0", "0")]
    [InlineData("DEFAULT_COMMISSION_RATE", "decimal", "0.15", "0.15")]
    [InlineData("DEFAULT_COMMISSION_RATE", "decimal", "0", "0")]
    [InlineData("DEFAULT_COMMISSION_RATE", "decimal", "1", "1")]
    [InlineData("PRICE_CEILING_PER_HOUR_VND", "decimal", "250000", "250000")]
    [InlineData("SETTLEMENT_CYCLE", "string", " monthly ", "MONTHLY")]          // không phân biệt hoa thường, lưu dạng chuẩn
    [InlineData("KEY_WITHOUT_RULE", "string", " bất kỳ ", "bất kỳ")]
    [InlineData("KEY_WITHOUT_RULE", "int", "-5", "-5")]                         // khóa chưa có luật riêng: chỉ kiểm tra kiểu
    [InlineData("KEY_WITHOUT_RULE", "bool", "TRUE", "true")]
    [InlineData("KEY_WITHOUT_RULE", "json", "{\"a\":1}", "{\"a\":1}")]
    public void Valid_value_is_normalized(string key, string dataType, string raw, string expected)
        => Assert.Equal(expected, SystemConfigRules.Normalize(key, dataType, raw));

    [Theory]
    [InlineData("BOOKING_HOLD_MINUTES", "int", null, "Giá trị tham số không được để trống.")]
    [InlineData("BOOKING_HOLD_MINUTES", "int", "   ", "Giá trị tham số không được để trống.")]
    [InlineData("BOOKING_HOLD_MINUTES", "int", "abc", "Giá trị của BOOKING_HOLD_MINUTES phải là số nguyên.")]
    [InlineData("BOOKING_HOLD_MINUTES", "int", "15.5", "Giá trị của BOOKING_HOLD_MINUTES phải là số nguyên.")]
    [InlineData("BOOKING_HOLD_MINUTES", "int", "1,000", "Giá trị của BOOKING_HOLD_MINUTES phải là số nguyên.")]
    [InlineData("BOOKING_HOLD_MINUTES", "int", "0", "Giá trị của BOOKING_HOLD_MINUTES phải trong khoảng 1–120.")]
    [InlineData("BOOKING_HOLD_MINUTES", "int", "121", "Giá trị của BOOKING_HOLD_MINUTES phải trong khoảng 1–120.")]
    [InlineData("CHECKIN_GRACE_PERIOD_MINUTES", "int", "-1", "Giá trị của CHECKIN_GRACE_PERIOD_MINUTES phải trong khoảng 0–120.")]
    [InlineData("DEFAULT_COMMISSION_RATE", "decimal", "0,15", "Giá trị của DEFAULT_COMMISSION_RATE phải là số thập phân, dùng dấu chấm (VD 0.15).")]
    [InlineData("DEFAULT_COMMISSION_RATE", "decimal", "10%", "Giá trị của DEFAULT_COMMISSION_RATE phải là số thập phân, dùng dấu chấm (VD 0.15).")]
    [InlineData("DEFAULT_COMMISSION_RATE", "decimal", "1.5", "Giá trị của DEFAULT_COMMISSION_RATE phải trong khoảng 0–1.")]
    [InlineData("DEFAULT_COMMISSION_RATE", "decimal", "-0.1", "Giá trị của DEFAULT_COMMISSION_RATE phải trong khoảng 0–1.")]
    [InlineData("OCR_AUTO_OPEN_MIN_CONFIDENCE", "decimal", "0", "Giá trị của OCR_AUTO_OPEN_MIN_CONFIDENCE phải trong khoảng 0.01–1.")]
    [InlineData("PRICE_CEILING_PER_HOUR_VND", "decimal", "0", "Giá trị của PRICE_CEILING_PER_HOUR_VND phải trong khoảng 1–10000000.")]
    [InlineData("SETTLEMENT_CYCLE", "string", "DAILY", "Giá trị của SETTLEMENT_CYCLE phải là một trong: WEEKLY, BIWEEKLY, MONTHLY.")]
    [InlineData("KEY_WITHOUT_RULE", "bool", "yes", "Giá trị của KEY_WITHOUT_RULE phải là true hoặc false.")]
    [InlineData("KEY_WITHOUT_RULE", "json", "{a:1", "Giá trị của KEY_WITHOUT_RULE phải là JSON hợp lệ.")]
    public void Invalid_value_is_rejected_with_message(string key, string dataType, string? raw, string expectedMessage)
    {
        var ex = Assert.Throws<ValidationException>(() => SystemConfigRules.Normalize(key, dataType, raw));

        Assert.Equal(expectedMessage, ex.Message);
    }

    [Fact]
    public void Value_longer_than_1000_characters_is_rejected()
    {
        Assert.Equal(new string('a', 1000), SystemConfigRules.Normalize("KEY_WITHOUT_RULE", "string", new string('a', 1000)));

        var ex = Assert.Throws<ValidationException>(() => SystemConfigRules.Normalize("KEY_WITHOUT_RULE", "string", new string('a', 1001)));
        Assert.Equal("Giá trị tham số tối đa 1000 ký tự.", ex.Message);
    }

    [Theory]
    [InlineData("decimal", "0.1", "0.10", true)]
    [InlineData("decimal", "0.1", "0.11", false)]
    [InlineData("int", "15", "15", true)]
    [InlineData("int", "15", "16", false)]
    [InlineData("string", "WEEKLY", "WEEKLY", true)]
    [InlineData("string", "WEEKLY", "weekly", false)]
    public void Same_value_is_compared_by_data_type(string dataType, string left, string right, bool expected)
        => Assert.Equal(expected, SystemConfigRules.AreSameValue(dataType, left, right));

    [Fact]
    public void Every_seeded_config_has_its_own_rule_and_its_seed_value_passes_it()
    {
        // Đọc dữ liệu seed (HasData) từ model của AdminDbContext – không mở kết nối database.
        var options = new DbContextOptionsBuilder<AdminDbContext>().UseNpgsql("Host=unused").Options;
        using var db = new AdminDbContext(options);
        var seeds = db.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(SystemConfig))!.GetSeedData().ToList();

        Assert.Equal(30, seeds.Count);
        foreach (var seed in seeds)
        {
            var key = (string)seed[nameof(SystemConfig.Key)]!;
            var value = (string)seed[nameof(SystemConfig.Value)]!;
            var dataType = (string)seed[nameof(SystemConfig.DataType)]!;

            Assert.True(SystemConfigRules.HasRuleFor(key), $"{key} chưa có luật riêng.");
            Assert.True(key.Length <= 50, $"{key} dài hơn 50 ký tự (AuditLog.EntityId).");
            // Chuẩn hóa không làm đổi giá trị về mặt số học / chuỗi.
            Assert.True(SystemConfigRules.AreSameValue(dataType, value, SystemConfigRules.Normalize(key, dataType, value)), key);
        }
    }

    [Fact]
    public void Order_rules_cover_price_bounds_and_no_show_versus_checkin_grace()
    {
        Assert.Contains(SystemConfigRules.OrderRules, r => r is { LowerKey: "PRICE_FLOOR_PER_HOUR_VND", UpperKey: "PRICE_CEILING_PER_HOUR_VND" });
        Assert.Contains(SystemConfigRules.OrderRules, r => r is { LowerKey: "CHECKIN_GRACE_PERIOD_MINUTES", UpperKey: "NO_SHOW_CANCEL_AFTER_MINUTES" });
    }
}
