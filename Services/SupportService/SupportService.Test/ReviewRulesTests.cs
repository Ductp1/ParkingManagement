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
    public void Complaint_must_be_sent_within_7_days()
    {
        var ended = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);
        Assert.True(ReviewRules.IsWithinComplaintWindow(ended, ended.AddDays(7)));
        Assert.False(ReviewRules.IsWithinComplaintWindow(ended, ended.AddDays(7).AddMinutes(1)));
    }

    [Fact]
    public void Owner_silent_for_48_hours_escalates_to_admin()
    {
        var due = new DateTime(2026, 10, 3, 0, 0, 0, DateTimeKind.Utc);
        Assert.True(ReviewRules.ShouldEscalate(ComplaintStatus.AwaitingOwner, due, due.AddMinutes(1)));
        Assert.False(ReviewRules.ShouldEscalate(ComplaintStatus.OwnerResponded, due, due.AddDays(1)));
    }
}
