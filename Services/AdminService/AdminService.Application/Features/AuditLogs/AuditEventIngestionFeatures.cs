using System.Net;
using System.Text;
using System.Text.Json;
using AdminService.Domain.Entities;
using Microsoft.Extensions.Logging;
using ParkingManagement.SharedKernel.Exceptions;

namespace AdminService.Application.Features.AuditLogs;

// ===== COMMAND / RESULT =====
/// <summary>
/// T-802: một request do OutboxDispatcher của service khác gửi tới. CallerService = service sở hữu khóa nội bộ đã xác thực
/// (controller lấy từ header Authorization, không nhận từ body). IdempotencyKey = header Idempotency-Key, Body = JSON gốc.
/// </summary>
public sealed record RecordAuditEventCommand(string CallerService, string? IdempotencyKey, string Body);

public enum AuditEventReceiptStatus
{
    /// <summary>Ghi mới một dòng AuditLogs.</summary>
    Recorded,
    /// <summary>EventId đã nhận trước đó (gửi lặp hoặc hai request cùng lúc) – không ghi thêm, phía gửi coi như đã xử lý.</summary>
    Duplicate,
    /// <summary>Sự kiện loại khác AuditRecorded – AdminService không xử lý, không ghi gì.</summary>
    Ignored,
}

public sealed record RecordAuditEventResult(AuditEventReceiptStatus Status, int? AuditLogId = null);

/// <summary>Envelope do IOutboxTransport gửi: { eventId, eventType, occurredAtUtc, payload }.</summary>
public sealed record IntegrationEventEnvelope(Guid EventId, string EventType, DateTime OccurredAtUtc, JsonElement Payload);

/// <summary>
/// Payload của AuditRecorded (SharedKernel). OwnerProfileId / IpAddress chưa có trong hợp đồng: đọc nếu payload có,
/// để khi hợp đồng mở rộng thì không phải sửa AdminService.
/// </summary>
public sealed record AuditRecordedPayload(string? SourceService, int? UserId, string? Action, string? EntityName, string? EntityId,
    string? OldValuesJson, string? NewValuesJson, string? Reason, int? OwnerProfileId = null, string? IpAddress = null);

// ===== PORT (ghi) =====
public interface IAuditEventInboxRepository
{
    /// <summary>EventId đã được ghi nhận (bảng InboxReceipts) chưa.</summary>
    Task<bool> HasReceivedAsync(Guid eventId, CancellationToken cancellationToken);
    /// <summary>
    /// Ghi InboxReceipt(eventId) + AuditLog trong CÙNG 1 lần SaveChanges. Trả false khi request khác vừa ghi cùng EventId
    /// (unique violation trên khóa chính InboxReceipts) – không có dòng nào được ghi thêm.
    /// </summary>
    Task<bool> TryRecordAsync(Guid eventId, AuditLog auditLog, DateTime receivedAtUtc, CancellationToken cancellationToken);
}

// ===== USE CASE (ghi) =====
public interface IRecordAuditEventUseCase
{
    Task<RecordAuditEventResult> ExecuteAsync(RecordAuditEventCommand command, CancellationToken cancellationToken = default);
}

/// <summary>
/// T-802: lưu sự kiện AuditRecorded của service khác thành một dòng AuditLogs (giữ nguyên các trường, thời điểm theo
/// OccurredAtUtc). Idempotent theo EventId của envelope (= Idempotency-Key, cố định qua mọi lần retry của cùng dòng outbox):
/// gửi lặp → Duplicate (thành công) để dispatcher đánh dấu đã xử lý. Payload sai → 400, không ghi gì.
/// </summary>
public sealed class RecordAuditEventUseCase(IAuditEventInboxRepository repository, TimeProvider timeProvider,
    ILogger<RecordAuditEventUseCase> logger) : IRecordAuditEventUseCase
{
    public const string AuditRecordedEventType = "AuditRecorded";
    public const int MaxBodyBytes = 256 * 1024;
    public const int MaxValuesJsonBytes = 64 * 1024;
    private const string OwnService = "AdminService";

    private static readonly JsonSerializerOptions PayloadJson = new(JsonSerializerDefaults.Web);
    private static readonly JsonDocumentOptions StrictJson = new() { AllowDuplicateProperties = false };

    public async Task<RecordAuditEventResult> ExecuteAsync(RecordAuditEventCommand command, CancellationToken cancellationToken = default)
    {
        var envelope = ParseEnvelope(command);
        if (!string.Equals(envelope.EventType, AuditRecordedEventType, StringComparison.Ordinal))
        {
            logger.LogInformation("Bỏ qua sự kiện {EventId} loại {EventType}: AdminService chỉ nhận AuditRecorded.",
                envelope.EventId, envelope.EventType);
            return new(AuditEventReceiptStatus.Ignored);
        }

        var payload = ParsePayload(envelope.Payload);
        var sourceService = Required(payload.SourceService, "sourceService", 50);
        if (string.Equals(sourceService, OwnService, StringComparison.OrdinalIgnoreCase))
            throw new ValidationException("sourceService không được là AdminService: audit của AdminService được ghi trực tiếp.");
        if (!string.Equals(sourceService, command.CallerService, StringComparison.OrdinalIgnoreCase))
            throw new ForbiddenException($"Khóa nội bộ của {command.CallerService} không được gửi audit log thay cho {sourceService}.");

        var action = Required(payload.Action, "action", 100);
        var entityName = Required(payload.EntityName, "entityName", 100);
        var entityId = Optional(payload.EntityId, "entityId", 50);
        var reason = Optional(payload.Reason, "reason", 1000);
        if (payload.UserId is <= 0) throw new ValidationException("userId phải là số nguyên dương.");
        if (payload.OwnerProfileId is <= 0) throw new ValidationException("ownerProfileId phải là số nguyên dương.");
        var ipAddress = Optional(payload.IpAddress, "ipAddress", 45);
        if (ipAddress is not null && !IPAddress.TryParse(ipAddress, out _))
            throw new ValidationException("ipAddress không phải địa chỉ IP hợp lệ.");
        var oldValuesJson = ValuesJson(payload.OldValuesJson, "oldValuesJson");
        var newValuesJson = ValuesJson(payload.NewValuesJson, "newValuesJson");

        if (await repository.HasReceivedAsync(envelope.EventId, cancellationToken))
            return new(AuditEventReceiptStatus.Duplicate);

        var receivedAtUtc = timeProvider.GetUtcNow().UtcDateTime;
        var createdAtUtc = envelope.OccurredAtUtc;
        if (createdAtUtc > receivedAtUtc)
        {
            // Lệch đồng hồ giữa các service: không trả 400 (dispatcher không phân biệt 4xx/5xx sẽ retry rồi kẹt).
            logger.LogWarning("Sự kiện {EventId} có occurredAtUtc sau thời điểm nhận; dùng thời điểm nhận.", envelope.EventId);
            createdAtUtc = receivedAtUtc;
        }

        var auditLog = AuditLog.FromIntegrationEvent(sourceService, payload.UserId, payload.OwnerProfileId, action, entityName,
            entityId, oldValuesJson, newValuesJson, reason, ipAddress, createdAtUtc);
        if (!await repository.TryRecordAsync(envelope.EventId, auditLog, receivedAtUtc, cancellationToken))
            return new(AuditEventReceiptStatus.Duplicate);

        return new(AuditEventReceiptStatus.Recorded, auditLog.Id);
    }

    /// <summary>Đọc envelope; Idempotency-Key bắt buộc và phải trùng eventId (khóa chống trùng là một giá trị duy nhất).</summary>
    public static IntegrationEventEnvelope ParseEnvelope(RecordAuditEventCommand command)
    {
        if (string.IsNullOrWhiteSpace(command.Body)) throw new ValidationException("Body không được để trống.");
        if (Encoding.UTF8.GetByteCount(command.Body) > MaxBodyBytes)
            throw new ValidationException($"Body tối đa {MaxBodyBytes / 1024} KB.");

        JsonDocument document;
        try { document = JsonDocument.Parse(command.Body, StrictJson); }
        catch (JsonException) { throw new ValidationException("Body phải là JSON hợp lệ."); }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object) throw new ValidationException("Body phải là một JSON object.");

            var eventIdElement = Property(root, "eventId");
            if (eventIdElement?.ValueKind != JsonValueKind.String || !eventIdElement.Value.TryGetGuid(out var eventId) || eventId == Guid.Empty)
                throw new ValidationException("eventId phải là GUID khác rỗng.");
            if (!Guid.TryParse(command.IdempotencyKey, out var idempotencyKey))
                throw new ValidationException("Thiếu header Idempotency-Key hoặc giá trị không phải GUID.");
            if (idempotencyKey != eventId)
                throw new ValidationException("Header Idempotency-Key phải trùng eventId trong body.");

            var eventTypeElement = Property(root, "eventType");
            if (eventTypeElement?.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(eventTypeElement.Value.GetString()))
                throw new ValidationException("eventType không được để trống.");

            var occurredElement = Property(root, "occurredAtUtc");
            if (occurredElement?.ValueKind != JsonValueKind.String || !occurredElement.Value.TryGetDateTime(out var occurredAt)
                || occurredAt == default)
                throw new ValidationException("occurredAtUtc phải là thời điểm ISO 8601 hợp lệ.");

            var payload = Property(root, "payload");
            if (payload?.ValueKind != JsonValueKind.Object) throw new ValidationException("payload phải là một JSON object.");

            return new(eventId, eventTypeElement.Value.GetString()!, UtcDateTime.ToUtc(occurredAt)!.Value, payload.Value.Clone());
        }
    }

    private static AuditRecordedPayload ParsePayload(JsonElement payload)
    {
        try
        {
            return payload.Deserialize<AuditRecordedPayload>(PayloadJson)
                ?? throw new ValidationException("payload AuditRecorded không được để trống.");
        }
        catch (JsonException) { throw new ValidationException("payload AuditRecorded sai định dạng (kiểu dữ liệu của trường không đúng)."); }
    }

    /// <summary>Tên thuộc tính so khớp không phân biệt hoa thường (envelope camelCase, payload PascalCase).</summary>
    private static JsonElement? Property(JsonElement obj, string name)
    {
        foreach (var property in obj.EnumerateObject())
            if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase)) return property.Value;
        return null;
    }

    private static string Required(string? value, string field, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ValidationException($"{field} không được để trống.");
        if (value.Length > maxLength) throw new ValidationException($"{field} tối đa {maxLength} ký tự.");
        return value;
    }

    private static string? Optional(string? value, string field, int maxLength)
    {
        if (value is null) return null;
        if (value.Length > maxLength) throw new ValidationException($"{field} tối đa {maxLength} ký tự.");
        return value;
    }

    /// <summary>
    /// Old/NewValuesJson: null hoặc JSON hợp lệ ≤ 64 KB; khóa credential được che trước khi ghi.
    /// Khóa trùng bị từ chối: {"password":"***","password":"bí mật"} không được lách qua bước che.
    /// </summary>
    private static string? ValuesJson(string? value, string field)
    {
        if (value is null) return null;
        if (Encoding.UTF8.GetByteCount(value) > MaxValuesJsonBytes)
            throw new ValidationException($"{field} tối đa {MaxValuesJsonBytes / 1024} KB.");
        try
        {
            using (JsonDocument.Parse(value, StrictJson)) { }
            return AuditCredentialRedactor.Redact(value);
        }
        catch (Exception ex) when (ex is JsonException or ArgumentException or InvalidOperationException)
        {
            throw new ValidationException($"{field} phải là JSON hợp lệ.");
        }
    }
}
