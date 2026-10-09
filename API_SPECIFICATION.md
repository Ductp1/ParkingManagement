# 📊 NotificationService API Specification

## 🏗️ Architecture Overview (C4 Model)

### Container Diagram
```
┌─────────────────────────────────────────────────────────────────┐
│                     ParkingManagement System                      │
├─────────────────────────────────────────────────────────────────┤
│                                                                   │
│  ┌──────────────────┐  ┌─────────────────┐  ┌──────────────────┐ │
│  │  Gateway         │  │ ParkingService  │  │  UserService     │ │
│  │  (Ocelot)        │  │  (SignalR       │  │  (gRPC)          │ │
│  │                  │  │  + Webhooks)    │  │                  │ │
│  └────────┬─────────┘  └────────┬────────┘  └────────┬─────────┘ │
│           │                     │                    │            │
│           └─────────────────────┼────────────────────┘            │
│                                 │                                 │
│                    ┌────────────▼──────────────┐                │
│                    │  🔔 NotificationService   │                │
│                    └────────────┬──────────────┘                │
│                                 │                                 │
│         ┌───────────────────────┼───────────────────────┐        │
│         │                       │                       │        │
│    ┌────▼─────┐       ┌────────▼────────┐     ┌────────▼───┐   │
│    │  REST    │       │  SignalR Hubs   │     │   Webhooks │   │
│    │  API     │       │  - /hubs/notify │     │   (AMQP)   │   │
│    │          │       │  - /hubs/parking│     │            │   │
│    └────┬─────┘       └────────┬────────┘     └────────┬───┘   │
│         │                      │                       │        │
│         └──────────────────────┼───────────────────────┘        │
│                                │                                 │
│         ┌──────────────────────▼──────────────────────┐        │
│         │         Application Layer                   │        │
│         │  - GetInboxUseCase                          │        │
│         │  - GetNotificationsUseCase                  │        │
│         │  - MarkNotificationAsReadUseCase            │        │
│         │  - SendNotificationUseCase (Dispatcher)     │        │
│         │  - RegisterDeviceTokenUseCase              │        │
│         │  - GetNotificationPreferencesUseCase        │        │
│         └──────────────────────┬──────────────────────┘        │
│                                │                                 │
│         ┌──────────────────────▼──────────────────────┐        │
│         │         Infrastructure Layer                │        │
│         │  - NotificationDbContext (EF Core)         │        │
│         │  - Repositories (Query, Command)           │        │
│         │  - Channel Senders:                         │        │
│         │    • EmailSender (SMTP)                    │        │
│         │    • SmsSender (AWS SNS / Twilio)          │        │
│         │    • PushSender (FCM)                      │        │
│         │    • InAppSender (SignalR broadcast)       │        │
│         └──────────────────────┬──────────────────────┘        │
│                                │                                 │
│         ┌──────────────────────▼──────────────────────┐        │
│         │         Data Storage                        │        │
│         │  - PM_NotificationDb (SQL Server)          │        │
│         │    • Notifications                         │        │
│         │    • NotificationTemplates                 │        │
│         │    • NotificationPreferences               │        │
│         │    • DeviceTokens                          │        │
│         │    • OutboxMessages (Outbox pattern)       │        │
│         └──────────────────────────────────────────────┘        │
│                                                                   │
│         ┌──────────────────────────────────────────────┐        │
│         │         External Services                   │        │
│         │  - Email: SMTP Server                       │        │
│         │  - SMS: Twilio / AWS SNS                    │        │
│         │  - Push: Firebase Cloud Messaging (FCM)     │        │
│         │  - Message Broker: RabbitMQ / Service Bus   │        │
│         └──────────────────────────────────────────────┘        │
│                                                                   │
└─────────────────────────────────────────────────────────────────┘
```

---

## 🚀 REST API Endpoints

### Base URL
```
http://localhost:8080/api/v1/notifications
```

### 1️⃣ **GET** `/` - Get Inbox (Danh sách thông báo)
**Module:** TV6 (S1-T601)
**Purpose:** Lấy danh sách thông báo của user với unread count

#### Request
```http
GET /api/v1/notifications?userId=5&unreadOnly=false&page=1&pageSize=20
```

**Query Parameters:**
| Parameter | Type | Required | Default | Constraint |
|-----------|------|----------|---------|-----------|
| `userId` | int | ✅ Yes | - | > 0 |
| `unreadOnly` | bool | ❌ No | false | - |
| `page` | int | ❌ No | 1 | ≥ 1 |
| `pageSize` | int | ❌ No | 20 | 1-100 |

#### Response (200 OK)
```json
{
  "unreadCount": 3,
  "items": {
	"items": [
	  {
		"id": 1,
		"channel": "InApp",
		"templateKey": "BOOKING_CONFIRMED",
		"title": "Đặt chỗ thành công",
		"body": "Booking BK-0002 tại Bãi xe Vincom đã được xác nhận",
		"dataJson": "{\"bookingId\": 42}",
		"status": "Sent",
		"isRead": false,
		"createdAtUtc": "2026-10-02T10:30:00Z"
	  },
	  {
		"id": 2,
		"channel": "Email",
		"templateKey": "OTP_CODE",
		"title": "Mã xác thực",
		"body": "Mã OTP của bạn là 123456",
		"dataJson": null,
		"status": "Sent",
		"isRead": true,
		"createdAtUtc": "2026-10-02T09:15:00Z"
	  }
	],
	"page": 1,
	"pageSize": 20,
	"totalCount": 15
  }
}
```

#### Error Responses
```json
// 400 Bad Request - userId ≤ 0
{
  "message": "Validation error",
  "errors": ["userId phải là số nguyên dương"]
}

// 400 Bad Request - page/pageSize invalid
{
  "message": "Validation error",
  "errors": ["page ≥ 1 và pageSize trong khoảng 1–100"]
}
```

---

### 2️⃣ **PUT** `/{notificationId}/read` - Mark as Read
**Module:** TV6 (S2+)
**Purpose:** Đánh dấu thông báo là đã đọc

#### Request
```http
PUT /api/v1/notifications/123/read?userId=5
Content-Type: application/json
```

**Parameters:**
| Parameter | Type | Location | Required | Constraint |
|-----------|------|----------|----------|-----------|
| `notificationId` | int | route | ✅ Yes | > 0 |
| `userId` | int | query | ✅ Yes | > 0 |

#### Response (204 No Content)
```
(Empty body)
```

#### Error Responses
```json
// 400 Bad Request
{
  "message": "notificationId và userId phải > 0"
}

// 404 Not Found
{
  "message": "Thông báo không tồn tại"
}

// 500 Internal Server Error
{
  "message": "Lỗi khi đánh dấu đã đọc",
  "error": "Database connection timeout"
}
```

---

### 3️⃣ **POST** `/send` - Send Notification (Internal API)
**Module:** TV6 (S1-T602)
**Purpose:** Gửi thông báo (chỉ dùng nội bộ - service internal hoặc admin)
**Auth:** Requires Admin role or Service Token

#### Request
```http
POST /api/v1/notifications/send
Content-Type: application/json
Authorization: Bearer <admin-token>

{
  "userId": 5,
  "channel": "InApp",
  "templateKey": "BOOKING_CONFIRMED",
  "title": "Đặt chỗ thành công",
  "body": "Booking BK-0042 đã được xác nhận",
  "dataJson": {
	"bookingId": 42,
	"lotName": "Bãi xe Vincom"
  }
}
```

**Request Body:**
```csharp
public sealed record SendNotificationRequest(
	int UserId,                          // Recipient
	NotificationChannel Channel,         // InApp, Email, Sms, Push
	string TemplateKey,                  // Template để tracking
	string Title,                        // Tiêu đề
	string Body,                         // Nội dung
	string? DataJson = null              // Dữ liệu thêm (serialized)
);
```

#### Response (200 OK)
```json
{
  "id": 123,
  "userId": 5,
  "channel": "InApp",
  "templateKey": "BOOKING_CONFIRMED",
  "title": "Đặt chỗ thành công",
  "body": "Booking BK-0042 đã được xác nhận",
  "dataJson": "{\"bookingId\": 42}",
  "status": "Sent",
  "retryCount": 0,
  "lastError": null,
  "sentAtUtc": "2026-10-02T15:45:30Z",
  "isRead": false,
  "readAtUtc": null,
  "createdAtUtc": "2026-10-02T15:45:30Z"
}
```

#### Error Responses
```json
// 400 Bad Request - Validation failed
{
  "message": "Validation error",
  "errors": ["userId phải > 0", "channel không hợp lệ"]
}

// 500 Internal Server Error - Send failed
{
  "message": "Gửi thông báo thất bại",
  "error": "SMTP connection failed"
}
```

---

### 4️⃣ **POST** `/send-multi` - Send to Multiple Channels
**Module:** TV6 (S1-T602)
**Purpose:** Gửi thông báo qua nhiều channel cùng lúc

#### Request
```http
POST /api/v1/notifications/send-multi
Content-Type: application/json
Authorization: Bearer <admin-token>

{
  "userId": 5,
  "channels": ["InApp", "Email", "Push"],
  "templateKey": "BOOKING_CONFIRMED",
  "title": "Đặt chỗ thành công",
  "body": "Booking BK-0042 đã được xác nhận",
  "dataJson": {
	"bookingId": 42
  }
}
```

#### Response (200 OK)
```json
{
  "results": [
	{
	  "id": 123,
	  "channel": "InApp",
	  "status": "Sent",
	  "sentAtUtc": "2026-10-02T15:45:30Z"
	},
	{
	  "id": 124,
	  "channel": "Email",
	  "status": "Sent",
	  "sentAtUtc": "2026-10-02T15:45:31Z"
	},
	{
	  "id": 125,
	  "channel": "Push",
	  "status": "Failed",
	  "lastError": "FCM service unavailable"
	}
  ]
}
```

---

### 5️⃣ **GET** `/preferences` - Get Preferences
**Module:** TV6 (S4)
**Purpose:** Lấy cấu hình thông báo của user

#### Request
```http
GET /api/v1/notifications/preferences?userId=5
```

#### Response (200 OK)
```json
[
  {
	"id": 1,
	"userId": 5,
	"templateKey": "BOOKING_REMINDER",
	"channel": "Email",
	"isEnabled": false,
	"quietFrom": null,
	"quietTo": null
  },
  {
	"id": 2,
	"userId": 5,
	"templateKey": "BOOKING_REMINDER",
	"channel": "Push",
	"isEnabled": true,
	"quietFrom": "23:00:00",
	"quietTo": "06:00:00"
  }
]
```

---

### 6️⃣ **PUT** `/preferences` - Update Preferences
**Module:** TV6 (S4)
**Purpose:** Cập nhật cấu hình thông báo

#### Request
```http
PUT /api/v1/notifications/preferences
Content-Type: application/json

{
  "userId": 5,
  "templateKey": "BOOKING_REMINDER",
  "channel": "Push",
  "isEnabled": true,
  "quietFrom": "23:00:00",
  "quietTo": "06:00:00"
}
```

#### Response (204 No Content)

---

### 7️⃣ **POST** `/devices/register` - Register Device Token
**Module:** TV6 (S1-T602)
**Purpose:** Đăng ký FCM token cho thiết bị (Push notification)

#### Request
```http
POST /api/v1/notifications/devices/register
Content-Type: application/json

{
  "userId": 5,
  "token": "fcm-token-abc123xyz",
  "platform": "Android",
  "deviceName": "Samsung Galaxy S24"
}
```

**Body:**
```csharp
public sealed record RegisterDeviceTokenRequest(
	int UserId,              // Device owner
	string Token,            // FCM token
	string Platform,         // "iOS", "Android", "Web"
	string? DeviceName       // "Samsung Galaxy S24", "Chrome", etc.
);
```

#### Response (201 Created)
```json
{
  "id": 42,
  "userId": 5,
  "token": "fcm-token-abc123xyz",
  "platform": "Android",
  "deviceName": "Samsung Galaxy S24",
  "lastUsedAtUtc": "2026-10-02T15:50:00Z",
  "isRevoked": false,
  "createdAtUtc": "2026-10-02T15:50:00Z"
}
```

---

### 8️⃣ **DELETE** `/devices/{token}` - Unregister Device Token
**Module:** TV6 (S1-T602)
**Purpose:** Hủy đăng ký thiết bị (revoke FCM token)

#### Request
```http
DELETE /api/v1/notifications/devices/fcm-token-abc123xyz
```

#### Response (204 No Content)

---

## 🔌 SignalR WebSocket Endpoints

### Hub URL: `/hubs/notify`
**Module:** TV6 (S1-T601)
**Purpose:** Real-time notification delivery khác REST polling

### Client Methods (Hub → Browser)
```csharp
// Nhận thông báo mới từ server
await connection.on("ReceiveNotification", (notification) => {
	console.log("New notification:", notification);
});

// Broadcast thông báo cho tất cả users
await connection.on("BroadcastNotification", (notification) => {
	console.log("Broadcast:", notification);
});

// Server heartbeat
await connection.on("Heartbeat", () => {
	console.log("Heartbeat from server");
});
```

### Server Methods (Browser → Hub)
```csharp
// Gửi thông báo cho user cụ thể
await hubConnection.invoke("SendNotificationToUserAsync", userId, notification);

// Heartbeat ACK
await hubConnection.invoke("HeartbeatAsync");
```

### Connection Example (JavaScript)
```javascript
const connection = new signalR.HubConnectionBuilder()
	.withUrl("/hubs/notify", {
		accessTokenFactory: () => getAuthToken()
	})
	.withAutomaticReconnect()
	.build();

connection.start().then(() => {
	console.log("Connected to NotificationHub");
}).catch(err => console.error("Connection failed:", err));
```

---

## 📊 Data Models

### NotificationDto
```csharp
public sealed record NotificationDto(
	int Id,                      // Unique ID
	string Channel,              // "InApp", "Email", "Sms", "Push"
	string TemplateKey,          // "OTP_CODE", "BOOKING_CONFIRMED", etc.
	string Title,                // UI display title
	string Body,                 // UI display body
	string? DataJson,            // Extra data (JSON string)
	string Status,               // "Pending", "Sent", "Failed"
	bool IsRead,                 // Has user read it?
	DateTime CreatedAtUtc        // Creation timestamp
);
```

### NotificationChannel (Enum)
```csharp
public enum NotificationChannel : byte
{
	InApp = 1,      // Real-time via SignalR
	Email = 2,      // via SMTP
	Sms = 3,        // via Twilio/SNS
	Push = 4        // via FCM
}
```

### NotificationStatus (Enum)
```csharp
public enum NotificationStatus : byte
{
	Pending = 0,    // Not sent yet
	Sent = 1,       // Successfully sent
	Failed = 2      // Send failed, pending retry
}
```

---

## 🔄 Notification Flow Diagram

```
┌─────────────────────────────────────────────────────────┐
│           Step 1: Event Triggered                       │
│  (Booking created, Payment completed, etc.)            │
└──────────────────┬──────────────────────────────────────┘
				   │
				   ▼
┌─────────────────────────────────────────────────────────┐
│    Step 2: Send to NotificationService                 │
│    POST /api/v1/notifications/send                     │
│    or RabbitMQ queue                                   │
└──────────────────┬──────────────────────────────────────┘
				   │
				   ▼
┌─────────────────────────────────────────────────────────┐
│    Step 3: Dispatcher Routes to Channels               │
│  ┌─────────────┐  ┌─────────┐  ┌────────┐  ┌───────┐  │
│  │ Email (SMTP)│  │SMS(SNS) │  │FCM(Push)│ │InApp  │  │
│  └────────┬────┘  └────┬────┘  └───┬────┘  └──┬───┘  │
└───────────┼──────────────┼───────────┼─────────┼────────┘
			│              │           │         │
			▼              ▼           ▼         ▼
	 ┌─────────┐  ┌──────────┐  ┌────────┐  ┌──────────┐
	 │ SMTP    │  │ Twilio   │  │Firebase│  │SignalR   │
	 │ Server  │  │ / AWS    │  │  FCM   │  │ Hub      │
	 └─────────┘  └──────────┘  └────────┘  └──────────┘
			│              │           │         │
			└──────────────┼───────────┼─────────┘
						   │
						   ▼
			  ┌────────────────────────┐
			  │  Save to Database      │
			  │  - Status: Sent/Failed │
			  │  - Retry Info          │
			  │  - Timestamp           │
			  └────────────────────────┘
						   │
						   ▼
			  ┌────────────────────────┐
			  │  Delivery to User      │
			  │  ✉️ Email inbox        │
			  │  📱 SMS message        │
			  │  🔔 Push notification  │
			  │  💬 In-app message     │
			  └────────────────────────┘
```

---

## 🔁 Retry Logic Flow

```
Notification Sent
	│
	├─ Success ──► Status = Sent ✅
	│
	└─ Failed ──► Status = Failed, RetryCount++
					│
					├─ RetryCount < 3 ──► Schedule Retry (5 min later)
					│                          │
					│                          ▼
					│                    Retry SendAsync()
					│                          │
					│                    ├─ Success ──► Status = Sent ✅
					│                    │
					│                    └─ Failed ──► RetryCount++
					│
					└─ RetryCount >= 3 ──► Move to Dead Letter Queue
										  (Manual admin review)
```

---

## 📈 Performance Considerations

### Database Indexes
```sql
-- Query notification list by user
CREATE INDEX IX_Notifications_UserId_IsRead_CreatedAtUtc 
ON Notifications (UserId, IsRead, CreatedAtUtc DESC);

-- Query failed notifications for retry
CREATE INDEX IX_Notifications_Status_RetryCount 
ON Notifications (Status, RetryCount);

-- Quick device token lookup
CREATE UNIQUE INDEX IX_DeviceTokens_Token 
ON DeviceTokens (Token);

-- User preferences lookup
CREATE UNIQUE INDEX IX_NotificationPreferences_UserId_TemplateKey_Channel 
ON NotificationPreferences (UserId, TemplateKey, Channel);
```

### Caching Strategy
- **Device Tokens**: Cache in Redis (TTL 24 hours)
- **Preferences**: Cache in Redis (TTL 1 hour)
- **Templates**: Cache in memory (refresh every 6 hours)

### Rate Limiting
- **Per User**: Max 100 notifications per day
- **Per Channel**: Email 10/day, SMS 5/day, Push unlimited
- **Burst**: Max 5 requests per 10 seconds

---

## 🔐 Security

### Authentication
- Uses JWT Bearer tokens
- Internal services use service-to-service authentication (mTLS)

### Authorization
- Public: GET /api/v1/notifications (own inbox only)
- Admin: POST /api/v1/notifications/send
- Admin: PUT /api/v1/notifications/preferences

### Data Protection
- Sensitive data (phone numbers, emails) are masked in logs
- Notification bodies are encrypted at rest (future implementation)

---

## 📚 References

- **Database Schema**: See `LEARNING_GUIDE_VI.md`
- **Unit Tests**: See `DispatcherTests.cs`, `NotificationManagementTests.cs`, `DeviceAndPreferenceTests.cs`
- **Implementation Status**: Sprint 1 (S1-T601, S1-T602)
