using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using AdminService.Application.Features.AuditLogs;
using AdminService.Domain.Entities;
using Microsoft.Extensions.Logging.Abstractions;
using ParkingManagement.SharedKernel.Contracts;
using ParkingManagement.SharedKernel.Domain;
using ParkingManagement.SharedKernel.Exceptions;

namespace AdminService.Test;

// T-802: nhận AuditRecorded của service khác (unit test với fake IAuditEventInboxRepository, không cần database).
public class AuditEventIngestionTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 27, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateTime NowUtc = Now.UtcDateTime;
    private static readonly DateTime OccurredUtc = new(2026, 10, 27, 7, 55, 0, DateTimeKind.Utc);
    private static readonly Guid EventId = Guid.Parse("8f0d6c1e-3b7a-4f0e-9c55-1a2b3c4d5e6f");
    private const string Caller = "ParkingService";

    /// <summary>Sự kiện giống ApproveKybApplicationUseCase (US-055) phát: Old/New là JSON camelCase.</summary>
    private static AuditRecorded KybApproved() => new("ParkingService", 1, "KybApplication.Approved", "KybApplication", "7",
        "{\"status\":\"UnderReview\",\"lotStatus\":\"PendingApproval\"}", "{\"status\":\"Approved\",\"lotStatus\":\"Active\"}",
        "HD-SP-2026-052 – đã ký")
    { OccurredAtUtc = OccurredUtc };

    /// <summary>
    /// Body đúng như IdentityOutboxTransport (UserService) gửi: outbox lưu PayloadJson = JsonSerializer.Serialize(ev, ev.GetType())
    /// (PascalCase), envelope { EventId = message.Id, EventType, OccurredAtUtc, Payload } qua JsonContent.Create (camelCase).
    /// </summary>
    private static string Body(IntegrationEvent ev, Guid? outboxId = null)
    {
        var message = new OutboxMessage
        {
            Id = outboxId ?? ev.EventId, EventType = ev.GetType().Name, OccurredAtUtc = ev.OccurredAtUtc,
            PayloadJson = JsonSerializer.Serialize(ev, ev.GetType()),
        };
        using var document = JsonDocument.Parse(message.PayloadJson);
        return JsonSerializer.Serialize(new { EventId = message.Id, message.EventType, message.OccurredAtUtc, Payload = document.RootElement.Clone() },
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
    }

    private static RecordAuditEventCommand Command(AuditRecorded ev, string caller = Caller)
        => new(caller, ev.EventId.ToString(), Body(ev));

    private static RecordAuditEventCommand Command(JsonObject envelope, string? idempotencyKey = null)
        => new(Caller, idempotencyKey ?? EventId.ToString(), envelope.ToJsonString());

    private static JsonObject Envelope(Action<JsonObject>? payload = null)
    {
        var envelope = JsonNode.Parse(Body(KybApproved() with { EventId = EventId }))!.AsObject();
        payload?.Invoke(envelope["payload"]!.AsObject());
        return envelope;
    }

    private static RecordAuditEventUseCase CreateUseCase(FakeInboxRepository repo) => new(repo, new FixedTimeProvider(Now),
        NullLogger<RecordAuditEventUseCase>.Instance);

    // ==================== Ghi đúng mapping ====================

    [Fact]
    public async Task Records_audit_event_keeping_every_field_and_occurred_time()
    {
        var repo = new FakeInboxRepository();
        var ev = KybApproved() with { EventId = EventId };

        var result = await CreateUseCase(repo).ExecuteAsync(Command(ev));

        Assert.Equal(AuditEventReceiptStatus.Recorded, result.Status);
        var log = Assert.Single(repo.AuditLogs);
        Assert.Equal(log.Id, result.AuditLogId);
        Assert.Equal("ParkingService", log.SourceService);
        Assert.Equal(1, log.UserId);
        Assert.Equal("KybApplication.Approved", log.Action);
        Assert.Equal("KybApplication", log.EntityName);
        Assert.Equal("7", log.EntityId);
        Assert.Equal(ev.OldValuesJson, log.OldValuesJson);               // không có credential → giữ nguyên từng ký tự
        Assert.Equal(ev.NewValuesJson, log.NewValuesJson);
        Assert.Equal("HD-SP-2026-052 – đã ký", log.Reason);
        Assert.Null(log.OwnerProfileId);                                 // hợp đồng AuditRecorded chưa có trường này
        Assert.Null(log.IpAddress);
        Assert.Equal(OccurredUtc, log.CreatedAtUtc);                     // thời điểm phát sinh, không phải lúc nhận
        Assert.Equal(DateTimeKind.Utc, log.CreatedAtUtc.Kind);
        var receipt = Assert.Single(repo.Receipts);
        Assert.Equal(EventId, receipt.Key);
        Assert.Equal(NowUtc, receipt.Value);                             // thời điểm nhận lưu ở inbox
    }

    [Fact]
    public async Task Event_from_support_service_with_default_serializer_is_recorded()
    {
        // SupportService (US-094): Old/New PascalCase, EntityId là chuỗi, OutboxMessage.Id KHÁC payload.EventId.
        var repo = new FakeInboxRepository();
        var ev = new AuditRecorded("SupportService", 1, "ReviewHidden", "Review", "15",
            JsonSerializer.Serialize(new { IsHidden = false }), JsonSerializer.Serialize(new { IsHidden = true }), "Ngôn từ xúc phạm")
        { OccurredAtUtc = OccurredUtc };

        var result = await CreateUseCase(repo).ExecuteAsync(new("SupportService", EventId.ToString(), Body(ev, EventId)));

        Assert.Equal(AuditEventReceiptStatus.Recorded, result.Status);
        Assert.Equal("{\"IsHidden\":true}", Assert.Single(repo.AuditLogs).NewValuesJson);
        Assert.Equal(EventId, Assert.Single(repo.Receipts).Key);         // khóa chống trùng = eventId của envelope
    }

    [Fact]
    public async Task Null_old_and_new_values_are_allowed()
    {
        var repo = new FakeInboxRepository();
        var ev = KybApproved() with { EventId = EventId, OldValuesJson = null, NewValuesJson = null, EntityId = null, Reason = null, UserId = null };

        await CreateUseCase(repo).ExecuteAsync(Command(ev));

        var log = Assert.Single(repo.AuditLogs);
        Assert.Null(log.OldValuesJson);
        Assert.Null(log.NewValuesJson);
        Assert.Null(log.EntityId);
        Assert.Null(log.Reason);
        Assert.Null(log.UserId);                                         // thao tác của hệ thống, không có người thực hiện
    }

    [Fact]
    public async Task Owner_profile_and_ip_address_are_read_when_payload_has_them()
    {
        var repo = new FakeInboxRepository();
        var envelope = Envelope(p => { p["OwnerProfileId"] = 2; p["IpAddress"] = "113.161.10.20"; });

        await CreateUseCase(repo).ExecuteAsync(Command(envelope));

        var log = Assert.Single(repo.AuditLogs);
        Assert.Equal(2, log.OwnerProfileId);
        Assert.Equal("113.161.10.20", log.IpAddress);
    }

    [Fact]
    public async Task Payload_property_names_are_case_insensitive()
    {
        var repo = new FakeInboxRepository();
        var envelope = new JsonObject
        {
            ["EventId"] = EventId.ToString(), ["EVENTTYPE"] = "AuditRecorded", ["occurredatutc"] = "2026-10-27T07:55:00Z",
            ["payload"] = new JsonObject { ["sourceService"] = "ParkingService", ["action"] = "KybApplication.Rejected", ["entityName"] = "KybApplication" },
        };

        await CreateUseCase(repo).ExecuteAsync(Command(envelope));

        Assert.Equal("KybApplication.Rejected", Assert.Single(repo.AuditLogs).Action);
    }

    [Theory]
    [InlineData("2026-10-27T14:55:00+07:00")]                          // có offset → đổi về UTC
    [InlineData("2026-10-27T07:55:00")]                                // không ghi múi giờ → coi như UTC
    [InlineData("2026-10-27T07:55:00.000Z")]
    public async Task Occurred_time_is_stored_as_utc(string occurredAtUtc)
    {
        var repo = new FakeInboxRepository();
        var envelope = Envelope();
        envelope["occurredAtUtc"] = occurredAtUtc;

        await CreateUseCase(repo).ExecuteAsync(Command(envelope));

        var log = Assert.Single(repo.AuditLogs);
        Assert.Equal(OccurredUtc, log.CreatedAtUtc);
        Assert.Equal(DateTimeKind.Utc, log.CreatedAtUtc.Kind);
    }

    [Fact]
    public async Task Event_from_the_future_is_recorded_at_receive_time_not_rejected()
    {
        // Lệch đồng hồ giữa service: trả 400 thì dispatcher retry rồi kẹt → ghi với thời điểm nhận.
        var repo = new FakeInboxRepository();
        var ev = KybApproved() with { EventId = EventId, OccurredAtUtc = NowUtc.AddHours(2) };

        var result = await CreateUseCase(repo).ExecuteAsync(Command(ev));

        Assert.Equal(AuditEventReceiptStatus.Recorded, result.Status);
        Assert.Equal(NowUtc, Assert.Single(repo.AuditLogs).CreatedAtUtc);
    }

    // ==================== Idempotent ====================

    [Fact]
    public async Task Same_event_sent_again_records_one_row_and_reports_duplicate()
    {
        var repo = new FakeInboxRepository();
        var useCase = CreateUseCase(repo);
        var command = Command(KybApproved() with { EventId = EventId });

        var first = await useCase.ExecuteAsync(command);
        var second = await useCase.ExecuteAsync(command);
        var third = await useCase.ExecuteAsync(command);

        Assert.Equal(AuditEventReceiptStatus.Recorded, first.Status);
        Assert.Equal(AuditEventReceiptStatus.Duplicate, second.Status);  // thành công: dispatcher đánh dấu đã xử lý
        Assert.Equal(AuditEventReceiptStatus.Duplicate, third.Status);
        Assert.Null(second.AuditLogId);
        Assert.Single(repo.AuditLogs);
        Assert.Single(repo.Receipts);
        Assert.Equal(1, repo.TryRecordCount);                            // lần lặp dừng ở bước kiểm tra inbox
    }

    [Fact]
    public async Task Request_losing_the_race_on_unique_event_id_reports_duplicate()
    {
        // Hai request cùng EventId: cả hai thấy inbox trống, request kia ghi trước → khóa chính InboxReceipts chặn request này.
        var repo = new FakeInboxRepository { LoseNextRace = true };

        var result = await CreateUseCase(repo).ExecuteAsync(Command(KybApproved() with { EventId = EventId }));

        Assert.Equal(AuditEventReceiptStatus.Duplicate, result.Status);
        Assert.Null(result.AuditLogId);
        Assert.Empty(repo.AuditLogs);
        Assert.Equal(1, repo.TryRecordCount);
    }

    [Fact]
    public async Task Concurrent_requests_with_same_event_id_record_exactly_one_row()
    {
        var repo = new FakeInboxRepository { PauseBeforeRecord = true };
        var command = Command(KybApproved() with { EventId = EventId });

        var results = await Task.WhenAll(Enumerable.Range(0, 8)
            .Select(_ => Task.Run(() => CreateUseCase(repo).ExecuteAsync(command))));

        Assert.Single(results, r => r.Status == AuditEventReceiptStatus.Recorded);
        Assert.Equal(7, results.Count(r => r.Status == AuditEventReceiptStatus.Duplicate));
        Assert.Single(repo.AuditLogs);
        Assert.Single(repo.Receipts);
    }

    [Fact]
    public async Task Events_with_different_event_ids_are_recorded_separately()
    {
        var repo = new FakeInboxRepository();
        var useCase = CreateUseCase(repo);

        await useCase.ExecuteAsync(Command(KybApproved()));
        await useCase.ExecuteAsync(Command(KybApproved()));

        Assert.Equal(2, repo.AuditLogs.Count);
        Assert.Equal(2, repo.Receipts.Count);
    }

    // ==================== Event loại khác ====================

    [Fact]
    public async Task Other_event_type_is_ignored_without_writing_anything()
    {
        var repo = new FakeInboxRepository();
        var ev = new UserLocked(5, "Sai OTP quá 3 lần");

        var result = await CreateUseCase(repo).ExecuteAsync(new(Caller, ev.EventId.ToString(), Body(ev)));

        Assert.Equal(AuditEventReceiptStatus.Ignored, result.Status);
        Assert.Empty(repo.AuditLogs);
        Assert.Empty(repo.Receipts);                                    // không ghi inbox
        Assert.Equal(0, repo.HasReceivedCount);
    }

    [Fact]
    public async Task Other_event_type_still_needs_a_valid_envelope()
    {
        var envelope = Envelope();
        envelope["eventType"] = "UserLocked";

        await Assert.ThrowsAsync<ValidationException>(() =>
            CreateUseCase(new FakeInboxRepository()).ExecuteAsync(Command(envelope, Guid.NewGuid().ToString())));
    }

    // ==================== Payload sai → 400, không ghi ====================

    public static TheoryData<string, Func<JsonObject, string?>> InvalidEnvelopes => new()
    {
        { "eventId", e => { e.Remove("eventId"); return null; } },
        { "eventId", e => { e["eventId"] = Guid.Empty.ToString(); return Guid.Empty.ToString(); } },
        { "eventId", e => { e["eventId"] = "khong-phai-guid"; return null; } },
        { "eventId", e => { e["eventId"] = 123; return null; } },
        { "Idempotency-Key", _ => "" },
        { "Idempotency-Key", _ => "abc" },
        { "Idempotency-Key", _ => Guid.NewGuid().ToString() },                  // lệch eventId
        { "eventType", e => { e.Remove("eventType"); return null; } },
        { "eventType", e => { e["eventType"] = " "; return null; } },
        { "occurredAtUtc", e => { e.Remove("occurredAtUtc"); return null; } },
        { "occurredAtUtc", e => { e["occurredAtUtc"] = "hôm qua"; return null; } },
        { "occurredAtUtc", e => { e["occurredAtUtc"] = 1730016000; return null; } },
        { "occurredAtUtc", e => { e["occurredAtUtc"] = "0001-01-01T00:00:00"; return null; } },
        { "payload", e => { e.Remove("payload"); return null; } },
        { "payload", e => { e["payload"] = "{}"; return null; } },
        { "payload", e => { e["payload"] = new JsonArray(); return null; } },
        { "sourceService", e => { e["payload"]!["SourceService"] = null; return null; } },
        { "sourceService", e => { e["payload"]!["SourceService"] = new string('S', 51); return null; } },
        { "AdminService", e => { e["payload"]!["SourceService"] = "AdminService"; return null; } },
        { "action", e => { e["payload"]!.AsObject().Remove("Action"); return null; } },
        { "action", e => { e["payload"]!["Action"] = new string('a', 101); return null; } },
        { "entityName", e => { e["payload"]!["EntityName"] = ""; return null; } },
        { "entityName", e => { e["payload"]!["EntityName"] = new string('e', 101); return null; } },
        { "entityId", e => { e["payload"]!["EntityId"] = new string('1', 51); return null; } },
        { "reason", e => { e["payload"]!["Reason"] = new string('r', 1001); return null; } },
        { "userId", e => { e["payload"]!["UserId"] = 0; return null; } },
        { "ownerProfileId", e => { e["payload"]!["OwnerProfileId"] = -1; return null; } },
        { "ipAddress", e => { e["payload"]!["IpAddress"] = "không-phải-ip"; return null; } },
        { "ipAddress", e => { e["payload"]!["IpAddress"] = new string('1', 46); return null; } },
        { "sai định dạng", e => { e["payload"]!["UserId"] = "abc"; return null; } },
        { "sai định dạng", e => { e["payload"]!["UserId"] = true; return null; } },
        { "sai định dạng", e => { e["payload"]!["EntityId"] = 7; return null; } },
        { "oldValuesJson", e => { e["payload"]!["OldValuesJson"] = "{status:"; return null; } },
        { "newValuesJson", e => { e["payload"]!["NewValuesJson"] = "không phải JSON"; return null; } },
        { "newValuesJson", e => { e["payload"]!["NewValuesJson"] = "{\"a\":1,\"a\":2}"; return null; } },   // khóa trùng
        { "newValuesJson", e => { e["payload"]!["NewValuesJson"] = $"{{\"note\":\"{new string('x', 64 * 1024)}\"}}"; return null; } },
    };

    [Theory]
    [MemberData(nameof(InvalidEnvelopes))]
    public async Task Invalid_event_is_rejected_without_writing(string expectedField, Func<JsonObject, string?> mutate)
    {
        var repo = new FakeInboxRepository();
        var envelope = Envelope();
        var idempotencyKey = mutate(envelope);

        var error = await Assert.ThrowsAsync<ValidationException>(() =>
            CreateUseCase(repo).ExecuteAsync(Command(envelope, idempotencyKey)));

        Assert.Contains(expectedField, error.Message);
        Assert.Empty(repo.AuditLogs);
        Assert.Empty(repo.Receipts);
        Assert.Equal(0, repo.HasReceivedCount);                          // từ chối trước khi chạm inbox
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("không phải JSON")]
    [InlineData("[1,2,3]")]
    [InlineData("\"AuditRecorded\"")]
    public async Task Body_that_is_not_a_json_object_is_rejected(string body)
    {
        var repo = new FakeInboxRepository();

        await Assert.ThrowsAsync<ValidationException>(() =>
            CreateUseCase(repo).ExecuteAsync(new(Caller, EventId.ToString(), body)));

        Assert.Empty(repo.AuditLogs);
    }

    [Fact]
    public async Task Body_over_256_kb_is_rejected()
    {
        var envelope = Envelope();
        envelope["padding"] = new string(' ', RecordAuditEventUseCase.MaxBodyBytes);

        var error = await Assert.ThrowsAsync<ValidationException>(() =>
            CreateUseCase(new FakeInboxRepository()).ExecuteAsync(Command(envelope)));
        Assert.Contains("256 KB", error.Message);
    }

    [Fact]
    public async Task Values_json_of_exactly_64_kb_is_accepted()
    {
        var repo = new FakeInboxRepository();
        var prefix = "{\"note\":\"";
        var json = prefix + new string('x', RecordAuditEventUseCase.MaxValuesJsonBytes - prefix.Length - 2) + "\"}";
        Assert.Equal(RecordAuditEventUseCase.MaxValuesJsonBytes, json.Length);

        await CreateUseCase(repo).ExecuteAsync(Command(Envelope(p => p["NewValuesJson"] = json)));

        Assert.Equal(json, Assert.Single(repo.AuditLogs).NewValuesJson);
    }

    [Fact]
    public async Task Source_service_must_match_the_service_owning_the_internal_key()
    {
        var repo = new FakeInboxRepository();

        // Khóa của SupportService không được gửi audit dưới tên ParkingService.
        var error = await Assert.ThrowsAsync<ForbiddenException>(() =>
            CreateUseCase(repo).ExecuteAsync(Command(KybApproved(), caller: "SupportService")));

        Assert.Contains("SupportService", error.Message);
        Assert.Contains("ParkingService", error.Message);
        Assert.Empty(repo.AuditLogs);
    }

    [Fact]
    public async Task Source_service_match_ignores_case()
    {
        var repo = new FakeInboxRepository();

        await CreateUseCase(repo).ExecuteAsync(Command(KybApproved(), caller: "parkingservice"));

        Assert.Equal("ParkingService", Assert.Single(repo.AuditLogs).SourceService);   // giữ nguyên giá trị của payload
    }

    // ==================== Che credential lúc ghi ====================

    [Fact]
    public async Task Credentials_in_old_and_new_values_are_masked_before_writing()
    {
        var repo = new FakeInboxRepository();
        var oldValues = "{\"Password\":\"Abc@12345\",\"profile\":{\"accessToken\":\"eyJhbGci\"},\"cards\":[{\"CVV\":\"123\",\"cardNumber\":\"4111111111111111\"}],"
            + "\"bankAccount\":\"0123456789\",\"phone\":\"0900000003\"}";
        var newValues = "{\"otpCode\":482913,\"refreshToken\":null,\"status\":\"Active\"}";

        await CreateUseCase(repo).ExecuteAsync(Command(Envelope(p => { p["OldValuesJson"] = oldValues; p["NewValuesJson"] = newValues; })));

        var log = Assert.Single(repo.AuditLogs);
        using (var old = JsonDocument.Parse(log.OldValuesJson!))
        {
            var root = old.RootElement;
            Assert.Equal("***", root.GetProperty("Password").GetString());
            Assert.Equal("***", root.GetProperty("profile").GetProperty("accessToken").GetString());
            Assert.Equal("***", root.GetProperty("cards")[0].GetProperty("CVV").GetString());
            Assert.Equal("***", root.GetProperty("cards")[0].GetProperty("cardNumber").GetString());
            Assert.Equal("0123456789", root.GetProperty("bankAccount").GetString());   // dữ liệu nghiệp vụ: US-100 che lúc đọc
            Assert.Equal("0900000003", root.GetProperty("phone").GetString());
        }
        using (var @new = JsonDocument.Parse(log.NewValuesJson!))
        {
            Assert.Equal("***", @new.RootElement.GetProperty("otpCode").GetString());   // số cũng bị thay
            Assert.Equal(JsonValueKind.Null, @new.RootElement.GetProperty("refreshToken").ValueKind);
            Assert.Equal("Active", @new.RootElement.GetProperty("status").GetString());
        }
        Assert.DoesNotContain("Abc@12345", log.OldValuesJson);
        Assert.DoesNotContain("482913", log.NewValuesJson);
    }

    [Theory]
    [InlineData("password")]
    [InlineData("PASSWORD")]
    [InlineData("passwordHash")]
    [InlineData("otp")]
    [InlineData("OtpCode")]
    [InlineData("token")]
    [InlineData("accessToken")]
    [InlineData("RefreshToken")]
    [InlineData("secret")]
    [InlineData("apiKey")]
    [InlineData("ApiKey")]
    [InlineData("cardNumber")]
    [InlineData("cvv")]
    public void Every_credential_key_is_masked(string key)
    {
        var redacted = AuditCredentialRedactor.Redact($"{{\"{key}\":\"bi-mat\",\"x\":1}}");

        using var document = JsonDocument.Parse(redacted);
        Assert.Equal("***", document.RootElement.GetProperty(key).GetString());
        Assert.Equal(1, document.RootElement.GetProperty("x").GetInt32());
    }

    [Theory]
    [InlineData("{\"tokenExpiresAtUtc\":\"2026-10-27T08:00:00Z\"}")]       // chỉ khớp nguyên tên khóa, không khớp một phần
    [InlineData("{\"passwordChangedAtUtc\":\"2026-10-27T08:00:00Z\"}")]
    [InlineData("{\"payoutBankAccount\":\"0123456789\",\"nationalId\":\"079123456789\"}")]
    [InlineData("{\"note\":\"password\"}")]                               // giá trị chứa chữ password, không phải khóa
    [InlineData("[\"token\",\"otp\"]")]
    [InlineData("\"password\"")]
    [InlineData("42")]
    [InlineData("null")]
    [InlineData("{ \"status\" : \"Approved\" }")]
    public void Json_without_credential_keys_is_kept_exactly(string json)
        => Assert.Equal(json, AuditCredentialRedactor.Redact(json));

    [Fact]
    public void Already_masked_value_keeps_original_text()
    {
        const string json = "{ \"password\" : \"***\" }";
        Assert.Equal(json, AuditCredentialRedactor.Redact(json));
    }

    [Fact]
    public void Credential_holding_an_object_is_replaced_entirely()
    {
        var redacted = AuditCredentialRedactor.Redact("{\"secret\":{\"key\":\"abc\",\"iv\":\"def\"}}");
        Assert.Equal("{\"secret\":\"***\"}", redacted);
    }

    // ==================== Tương thích US-096 / US-098 ====================

    [Fact]
    public void Internal_audit_logs_keep_their_default_creation_time()
    {
        // US-096 / US-098 tạo AuditLog bằng object initializer → CreatedAtUtc mặc định lúc tạo, không đổi.
        var before = DateTime.UtcNow;
        var internalLog = new AuditLog { SourceService = "AdminService", Action = "Owner.Locked", EntityName = "OwnerProfile" };
        Assert.InRange(internalLog.CreatedAtUtc, before, DateTime.UtcNow);

        var received = AuditLog.FromIntegrationEvent("ParkingService", 1, null, "KybApplication.Approved", "KybApplication", "7",
            null, null, null, null, OccurredUtc);
        Assert.Equal(OccurredUtc, received.CreatedAtUtc);
        Assert.Null(received.UpdatedAtUtc);
    }

    // ==================== Fakes ====================

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    /// <summary>
    /// Giả lập bảng InboxReceipts (khóa chính EventId) + AuditLogs. TryRecord kiểm tra và ghi dưới cùng một khóa,
    /// giống unique index của PostgreSQL: chỉ request đầu tiên ghi được.
    /// </summary>
    private sealed class FakeInboxRepository : IAuditEventInboxRepository
    {
        private static readonly PropertyInfo IdProperty = typeof(BaseEntity).GetProperty(nameof(BaseEntity.Id))!;
        private readonly Lock sync = new();
        public Dictionary<Guid, DateTime> Receipts { get; } = [];
        public List<AuditLog> AuditLogs { get; } = [];
        public bool LoseNextRace { get; set; }
        public bool PauseBeforeRecord { get; init; }
        public int HasReceivedCount;
        public int TryRecordCount;

        public Task<bool> HasReceivedAsync(Guid eventId, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref HasReceivedCount);
            lock (sync) return Task.FromResult(Receipts.ContainsKey(eventId));
        }

        public async Task<bool> TryRecordAsync(Guid eventId, AuditLog auditLog, DateTime receivedAtUtc, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref TryRecordCount);
            if (PauseBeforeRecord) await Task.Delay(20, cancellationToken);   // để các request cùng lúc đều qua bước kiểm tra
            lock (sync)
            {
                if (LoseNextRace) { LoseNextRace = false; Receipts[eventId] = receivedAtUtc; return false; }
                if (Receipts.ContainsKey(eventId)) return false;               // 23505 trên PK_InboxReceipts
                Receipts[eventId] = receivedAtUtc;
                IdProperty.SetValue(auditLog, AuditLogs.Count + 1);
                AuditLogs.Add(auditLog);
                return true;
            }
        }
    }
}
