namespace NotificationService.Application.Features.Tracking;

/// <summary>US-086: 1 dòng trạng thái gửi (1 notification = 1 user × 1 kênh).</summary>
public sealed record DeliveryStatusRow(
    int NotificationId, int UserId, string Channel, string TemplateKey, string Title,
    string Status, DateTime? SentAtUtc, int RetryCount, string? LastError, DateTime CreatedAtUtc);

/// <summary>US-086: tổng hợp trạng thái gửi của 1 event khẩn cấp theo từng kênh.</summary>
public sealed record DeliveryStatusDto(
    Guid EventId,
    int Total,
    int Sent,
    int Pending,
    int Failed,
    IReadOnlyList<DeliveryStatusRow> Items);

/// <summary>US-086: truy vấn trạng thái gửi theo EventId — mọi notification có DedupeKey "evt:{EventId}:...".</summary>
public interface IGetDeliveryStatusUseCase
{
    Task<DeliveryStatusDto> ExecuteAsync(Guid eventId, CancellationToken cancellationToken = default);
}

/// <summary>Cổng đọc persistence (Infrastructure EF implement) — Application không phụ thuộc Infrastructure.</summary>
public interface IDeliveryStatusReadStore
{
    Task<List<DeliveryStatusRow>> GetByEventIdAsync(Guid eventId, CancellationToken cancellationToken = default);
}

/// <summary>US-086: tổng hợp Sent/Pending/Failed cho monitoring + UI theo dõi gửi khẩn cấp.</summary>
public sealed class GetDeliveryStatusUseCase(IDeliveryStatusReadStore store) : IGetDeliveryStatusUseCase
{
    public async Task<DeliveryStatusDto> ExecuteAsync(Guid eventId, CancellationToken cancellationToken = default)
    {
        var rows = await store.GetByEventIdAsync(eventId, cancellationToken);
        return new DeliveryStatusDto(
            EventId: eventId,
            Total: rows.Count,
            Sent: rows.Count(r => r.Status == "Sent"),
            Pending: rows.Count(r => r.Status == "Pending"),
            Failed: rows.Count(r => r.Status == "Failed"),
            Items: rows);
    }
}