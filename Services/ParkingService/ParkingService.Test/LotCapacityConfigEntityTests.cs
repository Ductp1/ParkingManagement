using ParkingService.Domain.Entities;

namespace ParkingService.Test;

/// <summary>
/// US-066 – Test phần HIỆN CÓ (theo quyết định của QA review): entity LotCapacityConfig với
/// ngữ nghĩa "vùng đệm vãng lai" = WalkInBufferPercent là % CHỖ GIỮ cho khách vãng lai
/// (không phải trần tối đa). Logic enforce (chặn vãng lai khi hết buffer, chặn khi bãi đầy,
/// validate sức chứa khi lưu...) CHƯA được cài đặt ở service nào → phần đó ghi vào báo cáo
/// "chưa test được". KHÔNG viết test theo spec cho code chưa tồn tại.
/// </summary>
public class LotCapacityConfigEntityTests
{
    [Fact]
    public void Defaults_follow_the_documented_walk_in_buffer_policy()
    {
        var config = new LotCapacityConfig();

        // Giữ 30% cho khách vãng lai (bãi Mức 0: 30–50% theo đặc tả) + khóa kênh online khi còn ≤ 5 chỗ.
        Assert.Equal(30, config.WalkInBufferPercent);
        Assert.Equal(5, config.OnlineLockThreshold);
        Assert.False(config.InstantBookingPaused);
        Assert.False(config.IsEmergencyStopped);
        Assert.False(config.IsStale);
        Assert.Equal(0, config.ConsecutiveHeartbeatFailures);
    }

    [Fact]
    public void Config_values_round_trip()
    {
        var config = new LotCapacityConfig
        {
            ParkingLotId = 3,
            OnlineQuota = 50,
            WalkInBufferPercent = 45,
            OnlineLockThreshold = 8,
            InstantBookingPaused = true,
        };

        Assert.Equal(3, config.ParkingLotId);
        Assert.Equal(50, config.OnlineQuota);
        Assert.Equal(45, config.WalkInBufferPercent);
        Assert.Equal(8, config.OnlineLockThreshold);
        Assert.True(config.InstantBookingPaused);
    }

    [Fact]
    public void Emergency_stop_carries_reason()
    {
        var config = new LotCapacityConfig { IsEmergencyStopped = true, EmergencyReason = "Cháy hệ thống điện" };
        Assert.True(config.IsEmergencyStopped);
        Assert.Equal("Cháy hệ thống điện", config.EmergencyReason);
    }
}