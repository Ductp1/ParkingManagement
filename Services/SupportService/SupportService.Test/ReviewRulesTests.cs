using ParkingManagement.SharedKernel.Enums;
using SupportService.Domain.Rules;

namespace SupportService.Test;

public class ReviewRulesTests
{
    [Theory]
    [InlineData(BookingStatus.Completed, 5, true)]
    [InlineData(BookingStatus.Completed, 0, false)]
    [InlineData(BookingStatus.Completed, 6, false)]
    [InlineData(BookingStatus.Confirmed, 5, false)]   // chưa đỗ xong thì không được đánh giá
    public void Review_only_after_completed_booking_with_1_to_5_stars(BookingStatus status, int rating, bool allowed)
        => Assert.Equal(allowed, ReviewRules.CanReview(status, rating));

    [Fact]
    public void Complaint_must_be_sent_within_7_working_days()
    {
        // Sự cố thứ Sáu 09/10 15:00 giờ VN → ngày làm việc 12,13,14,15,16,19,20/10 → hạn hết thứ Ba 20/10 (giờ VN).
        var incident = new DateTime(2026, 10, 9, 8, 0, 0, DateTimeKind.Utc);
        var deadline = new DateTime(2026, 10, 20, 17, 0, 0, DateTimeKind.Utc);   // 00:00 thứ Tư 21/10 giờ VN

        Assert.Equal(deadline, ReviewRules.ComplaintDeadlineUtc(incident));
        Assert.True(ReviewRules.IsWithinComplaintWindow(incident, deadline.AddMinutes(-1)));
        Assert.False(ReviewRules.IsWithinComplaintWindow(incident, deadline));
    }

    [Fact]
    public void Weekend_incident_starts_counting_from_monday()
    {
        var saturday = new DateTime(2026, 10, 10, 3, 0, 0, DateTimeKind.Utc);
        Assert.Equal(new DateTime(2026, 10, 20, 17, 0, 0, DateTimeKind.Utc), ReviewRules.ComplaintDeadlineUtc(saturday));
    }

    [Theory]
    // 01/10 16:30 UTC = 23:30 thứ Năm 01/10 giờ VN → hạn hết thứ Hai 12/10
    [InlineData("2026-10-01T16:30:00Z", "2026-10-12T17:00:00Z")]
    // 01/10 17:30 UTC = 00:30 thứ Sáu 02/10 giờ VN → hạn hết thứ Ba 13/10
    [InlineData("2026-10-01T17:30:00Z", "2026-10-13T17:00:00Z")]
    public void Working_days_are_counted_in_vietnam_time(string incidentUtc, string expectedDeadlineUtc)
    {
        var incident = DateTime.Parse(incidentUtc, null, System.Globalization.DateTimeStyles.AdjustToUniversal);
        var expected = DateTime.Parse(expectedDeadlineUtc, null, System.Globalization.DateTimeStyles.AdjustToUniversal);
        Assert.Equal(expected, ReviewRules.ComplaintDeadlineUtc(incident));
    }

    [Fact]
    public void Owner_silent_for_48_hours_escalates_to_admin()
    {
        var due = new DateTime(2026, 10, 3, 0, 0, 0, DateTimeKind.Utc);
        Assert.True(ReviewRules.ShouldEscalate(ComplaintStatus.AwaitingOwner, due, due.AddMinutes(1)));
        Assert.False(ReviewRules.ShouldEscalate(ComplaintStatus.OwnerResponded, due, due.AddDays(1)));
    }
}
