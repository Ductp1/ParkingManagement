# 🎯 NotificationService - Test Execution Guide

## 📋 Unit Tests Overview

Chúng tôi đã tạo **5 file test chính** với tổng cộng **65 test cases** để cover tất cả use cases:

### Test Files

| File | Test cases | Coverage |
|------|-----------|----------|
| **InboxTests.cs** | 10 | Inbox (S1-T601): unread count, validation, paging |
| **DispatcherTests.cs** | 8 | Send/Retry use cases, delegation contract, error propagation |
| **NotificationDispatcherTests.cs** | 13 | **Dispatcher thật (Infrastructure)**: Sent/Failed, retry transient với FakeTimeProvider, permanent không retry, idempotent |
| **NotificationManagementTests.cs** | 15 | GetNotifications, Mark as read (404 khi không thuộc user), paging validation |
| **DeviceAndPreferenceTests.cs** | 19 | **Device token + Preferences persist thật**: upsert/revoke token, map preference rows, validation |

> **Kiến trúc test:** Use case (Application) được test với repository thay bằng NSubstitute.
> `NotificationDispatcherTests.cs` test **dispatcher thật của Infrastructure** (route qua Email/Sms/Fcm/
> InApp senders + retry exponential backoff) với `FakeTimeProvider` — vẫn **KHÔNG cần database**
> (feedback PR: test phải bảo vệ cả 2 path transient => Pending/retry, permanent => Failed ngay).
> Device token / preferences / mark-as-read đã persist thật qua port repository.

---

## ✨ Test Chạy Nhanh

### Cách 1: Visual Studio Test Explorer
1. **Open**: Menu `Test` → `Test Explorer`
2. **Run All**: Nhấn `Run All Tests` (biểu tượng play xanh)
3. **Filter**: Type tên test trong search box

### Cách 2: Command Line (PowerShell)
```powershell
# Run all tests
dotnet test Services/NotificationService/NotificationService.Test/

# Run specific test class
dotnet test Services/NotificationService/NotificationService.Test/ `
  --filter "FullyQualifiedName~NotificationDispatcherTests"

# Run with detailed output
dotnet test Services/NotificationService/NotificationService.Test/ `
  --logger "console;verbosity=detailed"
```

---

## 🔍 Test Descriptions

### **DispatcherTests.cs** - 8 Tests

> Application layer chỉ chứa use case ủy thác cho port `INotificationDispatcher`.
> Routing thật qua channel senders và retry/backoff nằm ở `NotificationDispatcher` (Infrastructure).

```csharp
[Fact] SendAsync_valid_request_returns_dispatcher_response
	// Verify: response (NotificationId, Status, Message) trả về nguyên vẹn
[Fact] SendAsync_passes_request_and_cancellation_token_to_dispatcher
	// Verify: Received(1).SendAsync(request, token)
[Fact] SendAsync_preserves_pending_status_from_dispatcher
	// Verify: Status Pending (row đã lưu DB, chờ retry job) được giữ nguyên
[Fact] RetryAsync_returns_dispatcher_response
[Fact] RetryAsync_passes_notification_id_to_dispatcher
[Fact] SendAsync_dispatcher_validation_failure_propagates
	// Verify: ValidationException (UserId <= 0) không bị nuốt
[Fact] RetryAsync_dispatcher_not_found_propagates
[Fact] SendAsync_failed_status_propagates_with_error_message
	// Verify: Status Failed + message "sau 3 lần thử" (MaxRetryAttempts = 3)
```

### **NotificationDispatcherTests.cs** - 13 Tests (dispatcher thật, Infrastructure)

> Test `NotificationDispatcher` với `INotificationRepository` + 4 sender là NSubstitute và
> `FakeTimeProvider` advance qua backoff — không cần DB, mỗi test < 1s.

```csharp
// ===== SendAsync – thành công =====
[Fact] SendAsync_success_on_first_attempt_marks_sent
	// Verify: Status=Sent, "OK", sender 1 lần, AddAsync + UpdateAsync đúng 1 lần
[Fact] SendAsync_success_sets_sent_at_utc_on_row
	// Verify: row Status=Sent, SentAtUtc != null, RetryCount=1

// ===== SendAsync – VĨNH VIỄN: Failed ngay, không retry (feedback PR) =====
[Fact] SendAsync_permanent_failure_marks_failed_immediately_without_retry
[Fact] SendAsync_unsupported_channel_fails_without_calling_any_sender

// ===== SendAsync – TẠM THỜI: retry (FakeTimeProvider advance backoff) =====
[Fact] SendAsync_transient_failure_exhausts_retries_marks_failed
	// Verify: sender bị gọi đúng 3 lần (MaxRetryAttempts) rồi Failed, RetryCount=3
[Fact] SendAsync_transient_failure_then_success_retries_until_sent
	// Verify: fail-fail-Ok → Sent sau 3 lần, UpdateAsync persist tiến độ mỗi lần

// ===== SendAsync – validation =====
[Fact] SendAsync_zero_user_id_throws_validation_exception
[Fact] SendAsync_blank_title_throws_validation_exception

// ===== RetryAsync (background job / trigger tay) =====
[Fact] RetryAsync_notification_not_found_returns_failed_without_sending
[Fact] RetryAsync_already_sent_is_idempotent_and_never_resends
[Fact] RetryAsync_exhausted_budget_marks_failed_without_sending
[Fact] RetryAsync_transient_with_remaining_budget_stays_pending
	// Verify: RetryCount 1→2 (< 3) + transient → vẫn Pending (chờ lần sau)
[Fact] RetryAsync_permanent_failure_marks_failed_immediately
```

### **NotificationManagementTests.cs** - 15 Tests

```csharp
// GetNotificationsUseCase
[Fact] GetNotifications_valid_request_returns_unread_count_and_items
[Theory] GetNotifications_invalid_user_id_throws_argument_exception (0, -5)
[Fact]   GetNotifications_zero_page_throws_argument_exception
[Theory] GetNotifications_invalid_page_size_throws_argument_exception (0, 101)
[Fact]   GetNotifications_passes_unread_only_flag_and_paging_to_queries
[Fact]   GetNotifications_queries_are_called_exactly_once_each
[Fact]   GetNotifications_empty_inbox_returns_zero_unread_and_no_items

// MarkNotificationAsReadUseCase (S2)
[Fact]   MarkAsRead_valid_notification_marks_read_once
[Fact]   MarkAsRead_notification_not_owned_throws_not_found
	// Verify: thông báo không thuộc user → NotFoundException (404)
[Theory] MarkAsRead_invalid_input_throws_argument_exception (4 cases)
```

> **GetInboxUseCase (S1-T601)** được cover trong **InboxTests.cs** (10 tests) — dùng fake
> `INotificationQueries` và `ValidationException`: `Inbox_returns_unread_count_with_items`,
> `Inbox_negative_user_id_throws_validation_exception`, `Inbox_zero_user_id_throws_validation_exception`,
> `Inbox_invalid_page_throws_validation_exception`, `Inbox_invalid_page_size_throws_validation_exception`,
> `Inbox_unread_only_filters_correctly`, `Inbox_empty_result_returns_zero_count`.

### **DeviceAndPreferenceTests.cs** - 19 Tests (persist thật)

```csharp
// RegisterDeviceTokenUseCase (upsert theo token)
[Fact]  RegisterDevice_new_token_adds_active_row
	// Verify: AddAsync 1 row mới, đúng UserId/Token/Platform, IsRevoked=false
[Fact]  RegisterDevice_existing_revoked_token_reactivates_instead_of_duplicate
	// Verify: UpdateAsync + bật lại (không AddAsync) – idempotent
[Theory] RegisterDevice_invalid_user_id_throws_argument_exception (0, -2)
[Theory] RegisterDevice_blank_token_throws_argument_exception ("", "   ")
[Fact]  RegisterDevice_null_request_throws_argument_null_exception

// UnregisterDeviceTokenUseCase
[Fact]  UnregisterDevice_known_token_marks_revoked
[Fact]  UnregisterDevice_unknown_token_throws_not_found
	// Verify: NotFoundException → 404
[Fact]  UnregisterDevice_invalid_user_id_throws_argument_exception
[Theory] UnregisterDevice_blank_device_id_throws_argument_exception ("", "   ")

// GetNotificationPreferencesUseCase (S4)
[Fact]  GetPreferences_maps_persisted_rows_and_fills_defaults
	// Verify: row lưu DB thắng default; dòng thiếu dùng default (marketing=false)
[Theory] GetPreferences_invalid_user_id_throws_argument_exception (0, -1)

// UpdateNotificationPreferencesUseCase (S4)
[Fact]  UpdatePreferences_persists_each_changed_flag
	// Verify: UpsertAsync đúng (TemplateKey, Channel, IsEnabled) cho từng flag đổi
[Fact]  UpdatePreferences_null_flags_persist_nothing
[Fact]  UpdatePreferences_null_request_throws_argument_null_exception
[Fact]  UpdatePreferences_invalid_user_id_throws_argument_exception
```

---

## 🧪 Kịch bản chạy thử

### Scenario 1: Notification Management
```powershell
dotnet test Services/NotificationService/NotificationService.Test/ `
  --filter "FullyQualifiedName~NotificationManagementTests"
```

### Scenario 2: Dispatcher (use case + dispatcher thật)
```powershell
dotnet test Services/NotificationService/NotificationService.Test/ `
  --filter "FullyQualifiedName~DispatcherTests|FullyQualifiedName~NotificationDispatcherTests"
```

### Scenario 3: Device & Preferences
```powershell
dotnet test Services/NotificationService/NotificationService.Test/ `
  --filter "FullyQualifiedName~DeviceAndPreferenceTests"
```

---

## 📊 Test Coverage Report

### Mocking Strategy
Sử dụng **NSubstitute** (đã có sẵn trong `NotificationService.Test.csproj` — repo quy ước NSubstitute, KHÔNG dùng Moq):

```csharp
// Substitute queries (port của Application layer)
var queries = Substitute.For<INotificationQueries>();
queries.CountUnreadAsync(Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(3);
queries.ListByUserAsync(Arg.Any<int>(), Arg.Any<bool>(), Arg.Any<int>(),
		Arg.Any<int>(), Arg.Any<CancellationToken>())
	.Returns(new PagedResult<NotificationDto>(items, 1, 20, 5));

// Substitute dispatcher port / device token repository
var dispatcher = Substitute.For<INotificationDispatcher>();
dispatcher.SendAsync(Arg.Any<SendNotificationRequest>(), Arg.Any<CancellationToken>())
	.Returns(new SendNotificationResponse(42, NotificationStatus.Sent, "OK"));
var deviceTokens = Substitute.For<IDeviceTokenRepository>();
deviceTokens.GetByUserAndTokenAsync(5, "tok", Arg.Any<CancellationToken>()).Returns((DeviceToken?)null);

// Verify interactions
await dispatcher.Received(1).SendAsync(request, token);  // Called exactly once
```

### Coverage Areas
- ✅ **Input validation**: userId/page/pageSize/token/deviceId/request null
- ✅ **Error cases**: ValidationException, NotFoundException, ArgumentException, ArgumentNullException
- ✅ **Dispatcher**: transient vs permanent, retry budget (MaxRetryAttempts=3), idempotent retry, unsupported channel
- ✅ **Persistence**: upsert/revoke device token, upsert preference theo (TemplateKey, Channel), mark-as-read theo user
- ⚠️ **Chưa cover** (cần môi trường thật): EF Core persistence trên PostgreSQL thật, SignalR hub runtime,
  wire-call tới Twilio/FCM/SMTP thật → thuộc Integration Tests (Tests/ParkingManagement.IntegrationTests).

---

## 🔧 Troubleshooting Test Failures

### Test fails: "ServiceProvider not found"
**Cause**: Thiếu DI registration → **Fix**: `Program.cs` đã có fail-fast check resolve dispatcher/sender/broadcaster lúc startup.

### Test fails: "Mock verification failed"
**Fix**: dùng `Received(1)` / `DidNotReceiveWithAnyArgs()` của NSubstitute (KHÔNG dùng Moq `Times.Once`).

### Test fails: "Type mismatch"
**Fix**: verify DTO trong `API_SPECIFICATION.md` — ví dụ `Platform` của DeviceToken là **string** ("Android"), không phải enum.

---

## 📝 Adding New Tests

```csharp
[Fact]  // or [Theory] with [InlineData(...)]
public async Task MethodName_WhenCondition_ReturnsExpected()
{
	// ARRANGE
	var queries = Substitute.For<INotificationQueries>();
	queries.CountUnreadAsync(Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(3);
	var useCase = new GetNotificationsUseCase(queries);

	// ACT
	var (unread, items) = await useCase.ExecuteAsync(new GetNotificationsRequest(5));

	// ASSERT
	Assert.Equal(3, unread);
	await queries.Received(1).CountUnreadAsync(5, Arg.Any<CancellationToken>());
}
```

Naming: `MethodName_WhenCondition_ReturnsExpected` — ví dụ thật:
`SendAsync_transient_failure_exhausts_retries_marks_failed`,
`RegisterDevice_existing_revoked_token_reactivates_instead_of_duplicate`,
`GetPreferences_maps_persisted_rows_and_fills_defaults`.

---

## 🎯 Next Steps

1. Run tests locally: `dotnet test Services/NotificationService/NotificationService.Test/`
2. Thêm integration test (WebApplicationFactory + PostgreSQL) cho controller + EF persistence thật
3. E2E test full flow booking → notification → SignalR nhận real-time
4. Load test cho high volume

---

**Happy Testing! 🚀**