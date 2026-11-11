using NotificationService.Application.Features.Ingestion;
using ParkingManagement.SharedKernel.Enums;

namespace NotificationService.Test;

/// <summary>US-086: tổng hợp trạng thái gửi đợt khẩn cấp — pure function.</summary>
public class EmergencyDeliveryAggregatorTests
{
    private static EmergencyDeliveryRow Row(int userId, NotificationChannel channel, NotificationStatus status) =>
        new(userId, channel, status, RetryCount: 0, LastError: null, SentAtUtc: null,
            CreatedAtUtc: new DateTime(2026, 11, 5, 2, 0, 0, DateTimeKind.Utc));

    [Fact]
    public void All_sent_reports_overall_sent_with_per_channel_counts()
    {
        var report = EmergencyDeliveryAggregator.Summarize("evt:1", [
            Row(5, NotificationChannel.Push, NotificationStatus.Sent),
            Row(5, NotificationChannel.Email, NotificationStatus.Sent),
            Row(6, NotificationChannel.Email, NotificationStatus.Sent),
        ]);

        Assert.Equal("Sent", report.OverallStatus);
        Assert.Equal(3, report.TotalMessages);
        var email = Assert.Single(report.Channels, c => c.Channel == "Email");
        Assert.Equal((2, 2, 0, 0).Item1, email.Total);
        Assert.Equal(2, email.Sent);
        Assert.Equal(0, email.Failed);
    }

    [Fact]
    public void Mixed_sent_and_failed_reports_partial()
    {
        var report = EmergencyDeliveryAggregator.Summarize("evt:1", [
            Row(5, NotificationChannel.Push, NotificationStatus.Sent),
            Row(6, NotificationChannel.Push, NotificationStatus.Failed),
        ]);

        Assert.Equal("Partial", report.OverallStatus);
        var push = Assert.Single(report.Channels);
        Assert.Equal(1, push.Sent);
        Assert.Equal(1, push.Failed);
    }

    [Fact]
    public void Any_pending_reports_in_progress()
    {
        var report = EmergencyDeliveryAggregator.Summarize("evt:1", [
            Row(5, NotificationChannel.Push, NotificationStatus.Sent),
            Row(6, NotificationChannel.Sms, NotificationStatus.Pending),
        ]);

        Assert.Equal("InProgress", report.OverallStatus);
    }

    [Fact]
    public void All_failed_reports_failed_per_channel_counts()
    {
        var report = EmergencyDeliveryAggregator.Summarize("evt:1", [
            Row(5, NotificationChannel.Push, NotificationStatus.Failed),
            Row(5, NotificationChannel.Sms, NotificationStatus.Failed),
        ]);

        Assert.Equal("Failed", report.OverallStatus);
        Assert.Equal(2, report.Channels.Count(c => c.Failed == 1 && c.Total == 1));
    }

    [Fact]
    public void Empty_rows_report_no_recipients()
    {
        var report = EmergencyDeliveryAggregator.Summarize("evt:1", []);
        Assert.Equal("NoRecipients", report.OverallStatus);
        Assert.Empty(report.Channels);
    }
}
