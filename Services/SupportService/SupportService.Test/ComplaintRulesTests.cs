using ParkingManagement.SharedKernel.Enums;
using SupportService.Domain.Rules;

namespace SupportService.Test;

public class ComplaintRulesTests
{
    private static readonly DateTime Now = new(2026, 10, 9, 8, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime End = Now.AddDays(3);
    private static readonly DateTime Changed = Now.AddDays(-2);

    [Theory]
    [InlineData(BookingStatus.Confirmed)]
    [InlineData(BookingStatus.CheckedIn)]
    [InlineData(BookingStatus.Parking)]
    [InlineData(BookingStatus.PendingOwnerApproval)]
    public void Ongoing_booking_incident_is_now(BookingStatus status)
        => Assert.Equal(Now, ComplaintRules.ResolveIncidentAtUtc(status, End, Changed, Now));

    [Fact]
    public void Ongoing_booking_past_its_end_time_is_anchored_at_end_time()
    {
        // "Confirmed" đã quá giờ kết thúc mà chưa chuyển no-show → không được coi là đang diễn ra mãi.
        var endedYesterday = Now.AddDays(-1);
        Assert.Equal(endedYesterday, ComplaintRules.ResolveIncidentAtUtc(BookingStatus.Confirmed, endedYesterday, Changed, Now));
    }

    [Theory]
    [InlineData(BookingStatus.Completed)]
    [InlineData(BookingStatus.CheckedOut)]
    [InlineData(BookingStatus.Cancelled)]
    [InlineData(BookingStatus.CancelledNoRefund)]
    [InlineData(BookingStatus.Rejected)]
    [InlineData(BookingStatus.Expired)]
    [InlineData(BookingStatus.NoShow)]
    public void Ended_booking_incident_is_last_status_change(BookingStatus status)
        => Assert.Equal(Changed, ComplaintRules.ResolveIncidentAtUtc(status, End, Changed, Now));

    [Fact]
    public void Ended_booking_without_history_falls_back_to_planned_end()
        => Assert.Equal(End, ComplaintRules.ResolveIncidentAtUtc(BookingStatus.Completed, End, null, Now));
}
