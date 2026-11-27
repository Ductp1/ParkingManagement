using GateService.Application.Features.LotCapacity;

namespace GateService.Test;

/// <summary>
/// US-066 (luật 1) – matrix validator theo hợp đồng lỗi mới:
/// REQUIRED (thiếu) → INVALID (0/âm) → EXCEEDS (gắn ô) → TOTAL_EXCEEDS (tổng hợp, không gắn field).
/// Mỗi ô chỉ giữ một lỗi; thông điệp khớp nguyên văn đặc tả.
/// </summary>
public class LotCapacityValidationTests
{
    private static (Dictionary<string, string[]> Codes, string? SummaryCode, string? SummaryMessage) Validate(int? c, int? b, int? q)
    {
        var outcome = LotCapacityValidator.Validate(c, b, q);
        var codes = outcome.Errors.ToDictionary(
            pair => pair.Key,
            pair => pair.Value.Select(e => e.Code).ToArray());
        return (codes, outcome.Summary?.Code, outcome.Summary?.Message);
    }

    // ===== Hợp lệ & biên =====
    [Theory]
    [InlineData(50, 10, 10)]  // Q + B < C
    [InlineData(50, 0, 0)]    // biên: B = 0, Q = 0
    [InlineData(50, 50, 0)]   // biên: B = C
    [InlineData(50, 0, 50)]   // biên: Q = C
    [InlineData(50, 25, 25)]  // biên: Q + B = C
    public void Valid_configs_produce_no_errors(int c, int b, int q)
    {
        var (codes, summaryCode, _) = Validate(c, b, q);
        Assert.Empty(codes);
        Assert.Null(summaryCode);
    }

    // ===== Thiếu (trống) =====
    [Fact]
    public void Empty_capacity_returns_required_with_exact_message()
    {
        var outcome = LotCapacityValidator.Validate(null, 0, 0);
        Assert.Equal("CAPACITY_REQUIRED", outcome.Errors["capacity"][0].Code);
        Assert.Equal("Hãy nhập tổng số chỗ của bãi (ví dụ: 100).", outcome.Errors["capacity"][0].Message);
    }

    [Fact]
    public void Empty_transient_returns_required_with_exact_message()
    {
        var outcome = LotCapacityValidator.Validate(50, null, 0);
        Assert.Equal("TRANSIENT_REQUIRED", outcome.Errors["transientBuffer"][0].Code);
        Assert.Equal("Hãy nhập số chỗ cho xe vãng lai. Nhập 0 nếu không nhận.", outcome.Errors["transientBuffer"][0].Message);
    }

    [Fact]
    public void Empty_quota_returns_required_with_exact_message()
    {
        var outcome = LotCapacityValidator.Validate(50, 0, null);
        Assert.Equal("SUBSCRIBER_REQUIRED", outcome.Errors["subscriberQuota"][0].Code);
        Assert.Equal("Hãy nhập số chỗ cho khách đăng ký. Nhập 0 nếu không nhận.", outcome.Errors["subscriberQuota"][0].Message);
    }

    // ===== Sai giá trị (0 / âm) =====
    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Capacity_zero_or_negative_returns_invalid(int c)
    {
        var outcome = LotCapacityValidator.Validate(c, 0, 0);
        Assert.Equal("CAPACITY_INVALID", outcome.Errors["capacity"][0].Code);
        Assert.Equal("Tổng số chỗ phải là số nguyên từ 1 trở lên.", outcome.Errors["capacity"][0].Message);
    }

    [Fact]
    public void Negative_buffer_returns_invalid_with_exact_message()
    {
        var outcome = LotCapacityValidator.Validate(50, -1, 0);
        Assert.Equal("TRANSIENT_INVALID", outcome.Errors["transientBuffer"][0].Code);
        Assert.Equal("Số chỗ không được âm hoặc có dấu phẩy. Nhập 0 nếu không nhận.", outcome.Errors["transientBuffer"][0].Message);
    }

    [Fact]
    public void Negative_quota_returns_invalid_with_exact_message()
    {
        var outcome = LotCapacityValidator.Validate(50, 0, -2);
        Assert.Equal("SUBSCRIBER_INVALID", outcome.Errors["subscriberQuota"][0].Code);
        Assert.Equal("Số chỗ không được âm hoặc có dấu phẩy. Nhập 0 nếu không nhận.", outcome.Errors["subscriberQuota"][0].Message);
    }

    // ===== Vượt tổng (gắn vào ô) =====
    [Fact]
    public void Buffer_exceeding_capacity_returns_exceeds_with_exact_message()
    {
        var outcome = LotCapacityValidator.Validate(50, 60, 0);
        var error = outcome.Errors["transientBuffer"][0];
        Assert.Equal("TRANSIENT_EXCEEDS_CAPACITY", error.Code);
        Assert.Equal("Chỗ cho xe vãng lai (60) nhiều hơn tổng số chỗ (50). Hãy giảm xuống tối đa 50.", error.Message);
    }

    [Fact]
    public void Quota_exceeding_capacity_returns_exceeds_with_exact_message()
    {
        var outcome = LotCapacityValidator.Validate(50, 0, 60);
        var error = outcome.Errors["subscriberQuota"][0];
        Assert.Equal("SUBSCRIBER_EXCEEDS_CAPACITY", error.Code);
        Assert.Equal("Chỗ cho khách đăng ký (60) nhiều hơn tổng số chỗ (50). Hãy giảm xuống tối đa 50.", error.Message);
    }

    // ===== Lỗi tổng hợp (không gắn field) =====
    [Fact]
    public void Buffer_plus_quota_over_capacity_returns_summary_without_field_errors()
    {
        var outcome = LotCapacityValidator.Validate(50, 30, 30);
        Assert.Empty(outcome.Errors);
        Assert.NotNull(outcome.Summary);
        Assert.Equal("TOTAL_EXCEEDS_CAPACITY", outcome.Summary!.Code);
        Assert.Equal("Vãng lai (30) + đăng ký (30) = 60, vượt tổng số chỗ 50. Hãy giảm bớt ít nhất 10 chỗ.", outcome.Summary.Message);
    }

    [Fact]
    public void Buffer_plus_quota_equal_capacity_is_valid_boundary()
    {
        var (codes, summaryCode, _) = Validate(50, 25, 25);
        Assert.Empty(codes);
        Assert.Null(summaryCode);
    }

    [Fact]
    public void Buffer_plus_quota_one_over_capacity_returns_summary_deficit_one()
    {
        var outcome = LotCapacityValidator.Validate(50, 25, 26);
        Assert.Empty(outcome.Errors);
        Assert.Equal("Vãng lai (25) + đăng ký (26) = 51, vượt tổng số chỗ 50. Hãy giảm bớt ít nhất 1 chỗ.",
            outcome.Summary!.Message);
    }

    // ===== Ưu tiên: lỗi từng ô đè lỗi tổng hợp =====
    [Fact]
    public void Field_error_takes_priority_over_summary()
    {
        var outcome = LotCapacityValidator.Validate(null, 40, 40);
        Assert.Equal("CAPACITY_REQUIRED", outcome.Errors["capacity"][0].Code);
        Assert.Null(outcome.Summary);
    }
}