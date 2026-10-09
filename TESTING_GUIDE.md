# 🎯 NotificationService - Test Execution Guide

## 📋 Unit Tests Overview

Chúng tôi đã tạo **5 file test chính** với tổng cộng **61 test cases** để cover tất cả use cases:

### Test Files

| File | Test cases | Coverage |
|------|-----------|----------|
| **InboxTests.cs** | 10 | Inbox (S1-T601): unread count, validation, paging |
| **DispatcherTests.cs** | 8 | Send/Retry use cases, delegation contract, error propagation |
| **NotificationDispatcherTests.cs** | 13 | **Dispatcher thật (Infrastructure)**: Sent/Failed, retry transient với FakeTimeProvider, permanent không retry, idempotent |
| **NotificationManagementTests.cs** | 13 | GetNotifications, Mark as read, paging validation |
| **DeviceAndPreferenceTests.cs** | 17 | Device registration, Preferences defaults, input validation |

> **Kiến trúc test:** Use case (Application) được test qua port `INotificationDispatcher` thay bằng
> NSubstitute. Riêng `NotificationDispatcherTests.cs` test **dispatcher thật của Infrastructure**
> (route qua Email/Sms/Fcm/InApp senders + retry exponential backoff) với repository + sender
> substitute và `FakeTimeProvider` — vẫn **KHÔNG cần database** (feedback PR: test phải bảo vệ
> cả 2 path transient => Pending/retry, permanent => Failed ngay).

---

## ✨ Test Chạy Nhanh

### Cách 1: Visual Studio Test Explorer
1. **Open**: Menu `Test` → `Test Explorer`
2. **Run All**: Nhấn `Run All Tests` (biểu tượng play xanh)
3. **Filter**: Type tên test trong search box

```
Example: "GetNotifications"
  ✅ GetNotifications_invalid_user_id_throws_exception
  ✅ GetNotifications_valid_params_returns_paginated_items
  ✅ GetNotifications_unread_only_filters_correctly
```

### Cách 2: Command Line (PowerShell)
```powershell
# Run all tests
dotnet test Services/NotificationService/NotificationService.Test/

# Run specific test class
dotnet test Services/NotificationService/NotificationService.Test/ `
  --filter "FullyQualifiedName~DispatcherTests"

# Run specific test method
dotnet test Services/NotificationService/NotificationService.Test/ `
  --filter "Name=SendAsync_valid_notification_routes_to_email_sender"

# Run with detailed output
dotnet test Services/NotificationService/NotificationService.Test/ `
  --logger "console;verbosity=detailed"

# Run with coverage report
dotnet test Services/NotificationService/NotificationService.Test/ `
  /p:CollectCoverage=true /p:CoverageFormat=opencover
```

### Cách 3: VS Code / Command Palette
```
Ctrl+Shift+P → "Test: Run All Tests"
```

---

## 🔍 Test Descriptions

### **DispatcherTests.cs** - 8 Tests

> Application layer chỉ chứa use case ủy thác cho port `INotificationDispatcher`.
> Routing thật qua channel senders và retry/backoff nằm ở `NotificationDispatcher` (Infrastructure).

#### ✅ Send/Retry Delegation Tests
```csharp
[Fact]
public async Task SendAsync_valid_request_returns_dispatcher_response()
	// Verify: response (NotificationId, Status, Message) trả về nguyên vẹn

[Fact]
public async Task SendAsync_passes_request_and_cancellation_token_to_dispatcher()
	// Verify: Received(1).SendAsync(request, token)

[Fact]
public async Task SendAsync_preserves_pending_status_from_dispatcher()
	// Verify: Status Pending (row đã lưu DB, chờ retry job) được giữ nguyên

[Fact]
public async Task RetryAsync_returns_dispatcher_response()
	// Verify: kết quả retry (Sent) trả về đúng

[Fact]
public async Task RetryAsync_passes_notification_id_to_dispatcher()
	// Verify: Received(1).RetryAsync(id, token)
```

#### ✅ Error Propagation Tests
```csharp
[Fact]
public async Task SendAsync_dispatcher_validation_failure_propagates()
	// Verify: ValidationException (UserId <= 0) không bị nuốt

[Fact]
public async Task RetryAsync_dispatcher_not_found_propagates()
	// Verify: NotFoundException khi notification không tồn tại

[Fact]
public async Task SendAsync_failed_status_propagates_with_error_message()
	// Verify: Status Failed + message "sau 3 lần thử" (MaxRetryAttempts = 3)
```

---

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
	// Verify: sender trả PermanentFailure → Failed, gửi đúng 1 lần
[Fact] SendAsync_unsupported_channel_fails_without_calling_any_sender
	// Verify: channel 99 → Failed "không được hỗ trợ", không sender nào chạy

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
	// Verify: "Notification không tìm thấy", không gọi sender
[Fact] RetryAsync_already_sent_is_idempotent_and_never_resends
	// Verify: đã Sent → trả OK, không gửi lại (chống duplicate)
[Fact] RetryAsync_exhausted_budget_marks_failed_without_sending
	// Verify: RetryCount=3 → Failed "Vượt quá 3 lần thử", không gửi thêm
[Fact] RetryAsync_transient_with_remaining_budget_stays_pending
	// Verify: RetryCount 1→2 (< 3) + transient → vẫn Pending (chờ lần sau)
[Fact] RetryAsync_permanent_failure_marks_failed_immediately
	// Verify: permanent → Failed ngay cả khi còn ngân sách retry
```

Chạy riêng:
```powershell
dotnet test Services/NotificationService/NotificationService.Test/ `
  --filter "FullyQualifiedName~NotificationDispatcherTests"
```

---

### **NotificationManagementTests.cs** - 13 Tests

#### ✅ GetNotificationsUseCase Validation
```csharp
[Theory]
[InlineData(0)]
[InlineData(-5)]
public async Task GetNotifications_invalid_user_id_throws_argument_exception(int userId)
	// Verify: userId must > 0 → ArgumentException

[Fact]
public async Task GetNotifications_zero_page_throws_argument_exception()
	// Verify: page must >= 1

[Theory]
[InlineData(0)]
[InlineData(101)]
public async Task GetNotifications_invalid_page_size_throws_argument_exception(int pageSize)
	// Verify: pageSize must be 1-100
```

#### ✅ GetNotificationsUseCase Functionality
```csharp
[Fact]
public async Task GetNotifications_valid_request_returns_unread_count_and_items()
	// Returns: (unreadCount=3, items=[1 notification])

[Fact]
public async Task GetNotifications_passes_unread_only_flag_and_paging_to_queries()
	// Verify: Received(1).ListByUserAsync(5, true, 2, 50, token)

[Fact]
public async Task GetNotifications_queries_are_called_exactly_once_each()
	// Verify: 1 lần CountUnreadAsync + 1 lần ListByUserAsync

[Fact]
public async Task GetNotifications_empty_inbox_returns_zero_unread_and_no_items()
	// Returns: (unreadCount=0, items=[])
```

#### ✅ MarkNotificationAsReadUseCase Tests (S2)
```csharp
[Fact]
public async Task MarkAsRead_valid_input_completes_without_error()
	// Verify: input hợp lệ không ném exception (repository gắn ở sprint sau)

[Theory]
[InlineData(0, 5)]   // notificationId = 0
[InlineData(-1, 5)]  // notificationId âm
[InlineData(7, 0)]   // userId = 0
[InlineData(7, -3)]  // userId âm
public async Task MarkAsRead_invalid_input_throws_argument_exception(int notificationId, int userId)
	// Verify: cả 2 Id phải > 0
```

> **Lưu ý:** GetInboxUseCase (S1-T601) được cover trong **InboxTests.cs** (10 tests):
> `Inbox_returns_unread_count_with_items`, `Inbox_negative_user_id_throws_validation_exception`,
> `Inbox_zero_user_id_throws_validation_exception`, `Inbox_invalid_page_throws_validation_exception`,
> `Inbox_invalid_page_size_throws_validation_exception`, `Inbox_unread_only_filters_correctly`,
> `Inbox_empty_result_returns_zero_count` — dùng fake `INotificationQueries` và `ValidationException`.

---

### **DeviceAndPreferenceTests.cs** - 17 Tests

#### ✅ RegisterDeviceTokenUseCase
```csharp
[Fact]
public async Task RegisterDevice_valid_request_completes_without_error()
	// Verify: (userId=5, token, deviceId, "Android") hợp lệ

[Theory]
[InlineData(0)]
[InlineData(-2)]
public async Task RegisterDevice_invalid_user_id_throws_argument_exception(int userId)
	// Verify: userId must > 0

[Theory]
[InlineData("")]
[InlineData("   ")]
public async Task RegisterDevice_blank_token_throws_argument_exception(string token)
	// Verify: Token không được trống/khoảng trắng

[Fact]
public async Task RegisterDevice_null_request_throws_argument_null_exception()
	// Verify: ArgumentNullException khi request = null
```

#### ✅ UnregisterDeviceTokenUseCase
```csharp
[Fact]
public async Task UnregisterDevice_valid_input_completes_without_error()
	// Verify: (userId=5, "device-01") hợp lệ

[Fact]
public async Task UnregisterDevice_invalid_user_id_throws_argument_exception()
	// Verify: userId must > 0

[Theory]
[InlineData("")]
[InlineData("   ")]
public async Task UnregisterDevice_blank_device_id_throws_argument_exception(string deviceId)
	// Verify: deviceId không được trống
```

#### ✅ GetNotificationPreferencesUseCase (S4)
```csharp
[Fact]
public async Task GetPreferences_valid_user_returns_default_preferences()
	// Returns: Email/Sms/Push/Transactional/Urgent = true, Marketing = false

[Theory]
[InlineData(0)]
[InlineData(-1)]
public async Task GetPreferences_invalid_user_id_throws_argument_exception(int userId)
	// Verify: userId must > 0
```

#### ✅ UpdateNotificationPreferencesUseCase (S4)
```csharp
[Fact]
public async Task UpdatePreferences_valid_request_completes_without_error()
	// Verify: tắt SMS + bật marketing hợp lệ

[Fact]
public async Task UpdatePreferences_null_request_throws_argument_null_exception()
	// Verify: ArgumentNullException khi request = null

[Fact]
public async Task UpdatePreferences_invalid_user_id_throws_argument_exception()
	// Verify: userId must > 0
```

---

## 🚀 Chạy Tests Theo Scenario

### Scenario 1: Test Inbox Feature (S1-T601)
```powershell
dotnet test Services/NotificationService/NotificationService.Test/ `
  --filter "Name~Inbox OR Name~GetNotifications"
```

**Expected Results:**
```
✅ GetNotifications_valid_request_returns_unread_count_and_items
✅ GetNotifications_invalid_user_id_throws_argument_exception (2 cases)
✅ GetNotifications_zero_page_throws_argument_exception
✅ GetNotifications_invalid_page_size_throws_argument_exception (2 cases)
✅ GetNotifications_passes_unread_only_flag_and_paging_to_queries
✅ GetNotifications_queries_are_called_exactly_once_each
✅ GetNotifications_empty_inbox_returns_zero_unread_and_no_items
✅ MarkAsRead_valid_input_completes_without_error
✅ MarkAsRead_invalid_input_throws_argument_exception (4 cases)
✅ Inbox_* (10 cases trong InboxTests.cs)
```

### Scenario 2: Test Dispatcher (S1-T602)
```powershell
dotnet test Services/NotificationService/NotificationService.Test/ `
  --filter "FullyQualifiedName~DispatcherTests"
```

**Expected Results:**
```
✅ SendAsync_valid_request_returns_dispatcher_response
✅ SendAsync_passes_request_and_cancellation_token_to_dispatcher
✅ SendAsync_preserves_pending_status_from_dispatcher
✅ SendAsync_dispatcher_validation_failure_propagates
✅ SendAsync_failed_status_propagates_with_error_message
✅ RetryAsync_returns_dispatcher_response
✅ RetryAsync_passes_notification_id_to_dispatcher
✅ RetryAsync_dispatcher_not_found_propagates
```

### Scenario 3: Test Device & Preferences (S1-T602, S4)
```powershell
dotnet test Services/NotificationService/NotificationService.Test/ `
  --filter "FullyQualifiedName~DeviceAndPreferenceTests"
```

**Expected Results:**
```
✅ RegisterDevice_valid_request_completes_without_error
✅ RegisterDevice_invalid_user_id_throws_argument_exception (2 cases)
✅ RegisterDevice_blank_token_throws_argument_exception (2 cases)
✅ RegisterDevice_null_request_throws_argument_null_exception
✅ UnregisterDevice_valid_input_completes_without_error
✅ UnregisterDevice_invalid_user_id_throws_argument_exception
✅ UnregisterDevice_blank_device_id_throws_argument_exception (2 cases)
✅ GetPreferences_valid_user_returns_default_preferences
✅ GetPreferences_invalid_user_id_throws_argument_exception (2 cases)
✅ UpdatePreferences_valid_request_completes_without_error
✅ UpdatePreferences_null_request_throws_argument_null_exception
✅ UpdatePreferences_invalid_user_id_throws_argument_exception
```

---

## 📊 Test Coverage Report

### Mocking Strategy
Sử dụng **NSubstitute** (đã có sẵn trong `NotificationService.Test.csproj` — repo quy ước NSubstitute, KHÔNG dùng Moq):

```csharp
// Substitute repository/queries (interface của Application layer)
var queries = Substitute.For<INotificationQueries>();
queries.CountUnreadAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
	.Returns(3);
queries.ListByUserAsync(Arg.Any<int>(), Arg.Any<bool>(), Arg.Any<int>(),
		Arg.Any<int>(), Arg.Any<CancellationToken>())
	.Returns(new PagedResult<NotificationDto>(items, 1, 20, 5));

// Substitute dispatcher port (Infrastructure thật sẽ route qua channel senders)
var dispatcher = Substitute.For<INotificationDispatcher>();
dispatcher.SendAsync(Arg.Any<SendNotificationRequest>(), Arg.Any<CancellationToken>())
	.Returns(new SendNotificationResponse(42, NotificationStatus.Sent, "OK"));
// hoặc ném exception để test propagation:
dispatcher.RetryAsync(999, Arg.Any<CancellationToken>())
	.Returns<SendNotificationResponse>(_ => throw new NotFoundException("Notification", 999));

// Verify interactions
await dispatcher.Received(1).SendAsync(request, token);  // Called exactly once
```

### Coverage Areas

#### ✅ Input Validation (Happy Path)
- Valid user IDs, pages, page sizes
- Valid channels, template keys
- Valid platform names

#### ✅ Error Cases (Unhappy Path)
- Invalid IDs (0, negative)
- Out-of-range paging
- Sender failures
- Database errors

#### ✅ Edge Cases
- Empty results
- Multiple channels
- Partial failures
- Max retries exceeded

#### ✅ Integration Points
- Repository calls
- Channel sender invocations
- Exception handling
- Status updates

---

## 🔧 Troubleshooting Test Failures

### Test fails: "ServiceProvider not found"
**Cause**: Missing DI registration
**Fix**: Ensure `AddNotificationApplication()` registered in `Program.cs`

### Test fails: "Mock verification failed"
**Cause**: Method not called as expected
**Fix**: Check `Received(1)`, `Received(3)`, etc.

```csharp
// ❌ Wrong: Expects 2 calls but only 1 happened
await dispatcher.Received(2).SendAsync(request, Arg.Any<CancellationToken>());

// ✅ Right:
await dispatcher.Received(1).SendAsync(request, Arg.Any<CancellationToken>());
```

### Test fails: "Type mismatch"
**Cause**: DTO or Model type changed
**Fix**: Verify in `API_SPECIFICATION.md` and update test data

```csharp
// Old (wrong): Platform was enum
new DeviceToken { Platform = DevicePlatform.Android }

// New (correct): Platform is string
new DeviceToken { Platform = "Android" }
```

---

## 📝 Adding New Tests

### Template cho Unit Test mới

```csharp
[Fact]  // or [Theory] with [InlineData(...)]
public async Task MethodName_WhenCondition_ReturnsExpected()
{
	// ARRANGE: Setup substitute
	var queries = Substitute.For<INotificationQueries>();
	queries.CountUnreadAsync(Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(3);
	var useCase = new GetNotificationsUseCase(queries);
	var input = new GetNotificationsRequest(5);

	// ACT: Call the method
	var (unread, items) = await useCase.ExecuteAsync(input);

	// ASSERT: Verify result
	Assert.Equal(3, unread);
	await queries.Received(1).CountUnreadAsync(5, Arg.Any<CancellationToken>());
}
```

### Naming Convention
```
MethodName_WhenCondition_ReturnsExpected

Examples (thật trong NotificationService.Test):
- SendAsync_valid_request_returns_dispatcher_response
- RegisterDevice_invalid_user_id_throws_argument_exception
- GetPreferences_valid_user_returns_default_preferences
```

---

## 🎯 Next Steps

1. **Run tests locally**: `dotnet test`
2. **Check coverage**: Use OpenCover or Coverlet
3. **Add API integration tests**: Test actual HTTP calls
4. **Add E2E tests**: Test full notification flow
5. **Performance tests**: Load testing for high volume

---

**Happy Testing! 🚀**
