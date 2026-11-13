using ParkingManagement.SharedKernel.Enums;

namespace NotificationService.Application.Features.Ingestion;

/// <summary>
/// US-086: tổng hợp trạng thái gửi 1 đợt khẩn cấp — pure function (không DB) để unit test.
/// Trạng thái tổng: Sent khi mọi row Sent; Failed khi mọi row Failed; Partial khi lẫn; Pending còn chờ.
/// </summary>
public static class EmergencyDeliveryAggregator
{
    public sealed record ChannelSummary(string Channel, int Total, int Sent, int Pending, int Failed);
    public sealed record DeliveryStatusReport(
        string BatchKey, string OverallStatus, int TotalMessages,
        IReadOnlyList<ChannelSummary> Channels, IReadOnlyList<EmergencyDeliveryRow> Rows);

    public static DeliveryStatusReport Summarize(string batchKey, IReadOnlyList<EmergencyDeliveryRow> rows)
    {
        var channels = rows
            .GroupBy(r => r.Channel)
            .OrderBy(g => g.Key.ToString())
            .Select(g => new ChannelSummary(
                g.Key.ToString(), g.Count(),
                g.Count(r => r.Status == NotificationStatus.Sent),
                g.Count(r => r.Status == NotificationStatus.Pending),
                g.Count(r => r.Status == NotificationStatus.Failed)))
            .ToList();

        var overall = rows.Count == 0
            ? "NoRecipients"
            : rows.All(r => r.Status == NotificationStatus.Sent) ? "Sent"
            : rows.All(r => r.Status == NotificationStatus.Failed) ? "Failed"
            : rows.Any(r => r.Status == NotificationStatus.Pending) ? "InProgress"
            : "Partial";

        return new DeliveryStatusReport(batchKey, overall, rows.Count, channels, rows);
    }
}
