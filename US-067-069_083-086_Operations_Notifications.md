# US-083 → US-086 & US-067 → US-069: Thông báo & Vận hành bãi real-time

> Triển khai 7 user story: US-083, US-084, US-085 (thông báo), US-068, US-067, US-069 (vận hành),
> US-086 (khẩn cấp đa kênh) — theo đúng thứ tự phụ thuộc: foundation → 083/084/085 → 068 → 067 → 069 → 086.

---

## 1. Những gì đã xây theo từng story

### Foundation (đặt nền trước US-083…086)
- **Event ingestion NotificationService**: consumer RabbitMQ duy nhất (`NotificationIntegrationEventHandler`)
  + **TransactionalInbox** chặn xử lý trùng theo `EventId` (idempotency lớp 1, cùng transaction với DB).
- **i18n template renderer** (`DbTemplateRenderer`): chọn mẫu theo `(Key, Channel, Language)`, fallback `vi`;
  `UserNotificationSettings` lưu ngôn ngữ mỗi user.
- **`DedupeKey`** trên `Notification` (unique filtered index) — idempotency lớp 2 chống replay/webhook.
- **BookingService**: bridge `PaymentSucceeded → ConfirmPayment → BookingConfirmed`; phát
  `BookingCreated/Cancelled/Modified` trong cùng transaction (outbox).

### US-083 — Thông báo xác nhận booking & thanh toán
- `BookingConfirmationNotifications`: consume `BookingConfirmed` (InApp + Email) và
  `BookingPaymentCompleted` (thành công/thất bại, InApp).
- Nội dung gồm mã booking, bãi, slot, khung giờ, số tiền; idempotent qua DedupeKey `evt:{EventId}:{Channel}`.
- Test: `BookingConfirmationNotificationsTests` (7).

### US-084 — Nhắc lịch trước giờ đến & sắp hết giờ
- `BookingReminderScheduler`: 2 job/booking — **-60 phút trước giờ đến**, **-30 phút trước giờ hết hạn**
  (config `Reminders:ArrivalLeadMinutes/EndLeadMinutes`); `ReminderSchedulerWorker` poll 15s.
- Cancel khi `BookingCancelled`, reschedule khi `BookingModified`.
- Giờ hiển thị múi giờ **Asia/Ho_Chi_Minh** (`VietnamTime.Format`); migration `Us084_ReminderJobs`.
- Test: `ReminderSchedulerTests` (6).

### US-085 — Thông báo cho chủ bãi về booking
- `OwnerBookingNotifications`: booking mới/hủy/đổi → InApp cho chủ bãi, scope qua `OwnerProfileId`
  trong payload (không broadcast); mapping OwnerProfileId→UserId qua
  `UserServiceDirectory.GetOwnerUserIdAsync` (endpoint `GET /users/owner-profiles/{id}` thêm ở UserService).

### US-068 — Heartbeat & Stale Data
- `POST /api/v1/parking-lots/{lotId}/operations/heartbeat` — thiết bị/controller báo còn sống
  (`ParkingLot.RecordHeartbeat`, tự phục hồi stale).
- `StalenessMonitorWorker`: quét mọi bãi Active mỗi `PollSeconds` (30s); quá
  `StaleAfterMinutes` (3 phút) không heartbeat → `IsDataStale=true` + event `LotDataMarkedStale`;
  heartbeat resume → `LotDataRecovered`. Pure logic tách `StalenessEvaluator` để test.
- Chủ bãi nhận cảnh báo (InApp + Email, US-086 handler).
- Test: `LotOperationsTests` (US-068 phần).

### US-067 — Dashboard vận hành real-time
- `GET /api/v1/parking-lots/{lotId}/operations/overview`: occupancy/available/slot breakdown theo state,
  active/upcoming bookings (internal endpoint `GET /api/v1/bookings/internal/lots/{lotId}/overview` của
  BookingService), trạng thái bãi, **freshness** (`LastHeartbeatAtUtc`, `IsDataStale`, threshold).
- RBAC: LotOwner chỉ xem được bãi của mình (`OwnerProfileId` claim); Staff/Admin toàn quyền.
- **Real-time qua SignalR có sẵn** `/hubs/parking` (group `lot-{id}`): event mới
  **`LotStatusChanged`** broadcast khi emergency stop/lift/stale/recovered (xem US-086).
- Test: `LotOperationsTests` (US-067 phần, kể cả degrade khi BookingService chết).

### US-069 — Emergency Stop
- `POST /api/v1/parking-lots/{lotId}/operations/emergency-stop` — **bắt buộc `confirm=true`**, lý do ≥5 ký tự;
  `Status → Suspended`; audit log `EmergencyStopLogs` (ai, vai trò, khi nào, lý do, số booking bị ảnh hưởng);
  event `LotEmergencyStopped` (kèm danh sách booking bị ảnh hưởng) → outbox.
- `POST .../emergency-stop/lift` — mở lại; `GET .../emergency-logs` — audit trail.
- **Chặn booking mới**: BookingService check `GET /api/v1/parking-lots/{id}/operations/status` trước khi tạo
  (fail-open khi ParkingService chết — ghi chú trong `LotStatusProvider`).
- **Chặn check-in**: GateService consumer cache bảng `LotOperationalStatuses`; `LotAdmissionGate` chặn
  MỌI check-in mới (vãng lai + thuê bao/booking) khi bãi đóng; **check-out không check** → xe trong bãi
  thoát an toàn (in-flight an toàn).
- Idempotent: stop/lift lặp lại không phát thêm event/log.
- Test: `LotOperationsTests` (US-069 phần) + `EmergencyClosureDbTests` (PostgreSQL thật) +
  `CreateBooking_blocked_when_lot_is_emergency_stopped` (BookingService.Test).

### US-086 — Thông báo khẩn cấp đa kênh & theo dõi trạng thái gửi
- `EmergencyNotifications` consume `LotEmergencyStopped/Lifted/MarkedStale/Recovered`:
  - **Khách bị ảnh hưởng**: broadcast **Push + SMS + Email** đồng thời (template `EMERGENCY_CLOSURE`),
    1 row/kênh, DedupeKey `evt:{EventId}:u{UserId}:{Channel}` (replay idempotent).
  - **Chủ bãi**: InApp + Email (`EMERGENCY_CLOSURE_OWNER`, `LOT_REOPENED`, `LOT_DATA_STALE`, `LOT_DATA_RECOVERED`).
  - **Fallback**: cả đợt kênh chính FAILED vĩnh viễn (sau khi dispatcher retry 3 lần exponential backoff) →
    gửi kênh **InApp** dự phòng, DedupeKey `evt:{EventId}:u{UserId}:fb:InApp`. Pending (lỗi tạm thời) không fallback chồng.
  - Nhóm theo user: user có nhiều booking vẫn nhận 1 bộ/kênh (dùng booking sớm nhất).
- **Tracking**: bảng `EmergencyAlerts` (metadata đợt, BatchKey unique `evt:{EventId}`) +
  `Notifications.BatchKey` (index) → trạng thái gửi per-recipient×channel.
- **API tổng hợp cho người gửi** (`EmergencyAlertsController`, `[Authorize]`):
  - `GET /api/v1/notifications/emergency-alerts?lotId=3`
  - `GET /api/v1/notifications/emergency-alerts/{batchKey}/delivery-status`
    → `OverallStatus` (Sent/Partial/InProgress/Failed/NoRecipients) + tổng hợp theo kênh + rows chi tiết.
  - Phân quyền: Staff/Admin mọi bãi; **LotOwner chỉ đợt của bãi mình** (so `OwnerProfileId` claim).
- **Real-time**: `SignalRLotStatusBroadcaster` → `LotStatusChanged` trên `/hubs/parking` (US-067 dashboard).
- Migration `Us086_EmergencyNotifications` (đã test down/up trên PostgreSQL thật).
- Test: `EmergencyNotificationsTests` (9) + `EmergencyDeliveryAggregatorTests` (5).


---

## 2. Migration & schema

| Cơ sở dữ liệu | Thay đổi | Cách áp dụng | Rollback |
|---|---|---|---|
| pm_notification | `Us086_EmergencyNotifications`: bảng `EmergencyAlerts`, cột `Notifications.BatchKey` (+index), `DedupeKey` 500→100, seed **12 mẫu i18n vi/en** (id 38–49) | EF migration (tự chạy khi khởi động service — `Database:ApplyMigrationsOnStartup`) | `dotnet ef database update Us084_ReminderJobs` (đã test trên Postgres thật) |
| pm_parking | `ParkingLots`: `LastHeartbeatAtUtc`, `IsDataStale`, `EmergencyStoppedAtUtc`, `EmergencyStopReason`; bảng `EmergencyStopLogs` | `database/10_US067-069_OperationsAddendum.sql` (idempotent) | mục ROLLBACK trong file |
| pm_gate | bảng `LotOperationalStatuses` (+unique index), `InboxReceipts` | cùng script | mục ROLLBACK trong file |

> ⚠️ Lưu ý đối chiếu: baseline SQL cũ (`database/03_PM_ParkingDb.sql`, T-SQL) ghi cột
> `IsEmergencyStopped/EmergencyReason/IsStale/ConsecutiveHeartbeatFailures` — **khớp tên khác** với
> EF model hiện hành (`EmergencyStoppedAtUtc/EmergencyStopReason/IsDataStale`, không có
> `ConsecutiveHeartbeatFailures`). Addendum mới dùng đúng tên EF runtime; baseline cũ chỉ là tài liệu
> thiết kế ban đầu. `HasData` mẫu thông báo gán id tuần tự — **chỉ APPEND mẫu mới ở cuối danh sách**
> (chèn giữa làm dịch id, migration sẽ ghi đè lịch sử).

---

## 3. Config/env mới

| Key | Mặc định | Ý nghĩa |
|---|---|---|
| `ParkingService:LotHeartbeat:StaleAfterMinutes` | 3 | Ngưỡng phút không heartbeat → stale (US-068) |
| `ParkingService:LotHeartbeat:PollSeconds` | 30 | Chu kỳ quét của `StalenessMonitorWorker` |
| `ParkingService:BookingService:BaseUrl` | http://localhost:5104 | Lookup booking cho dashboard/emergency |
| `BookingService:ParkingService:BaseUrl` | http://localhost:5103 | Check lot status trước khi tạo booking |
| `GateService:EventBus:Subscription.RoutingKeys` | `LotEmergencyStopped, LotEmergencyLifted` | Consumer cache GateService |
| `NotificationService:EmergencyNotifications:CustomerChannels` | `Push, Sms, Email` | Broadcast cho khách bị ảnh hưởng |
| `NotificationService:EmergencyNotifications:FallbackChannels` | `InApp` | Fallback khi cả đợt chính FAILED |
| `NotificationService:EmergencyNotifications:OwnerChannels` | `InApp, Email` | Kênh cho chủ bãi khi stop/lift |
| `NotificationService:EmergencyNotifications:StaleOwnerChannels` | `InApp, Email` | Kênh cảnh báo stale (US-068) |
| `NotificationService:Jwt:PublicKeyPath` | `Keys/public.key` | Public key UserService cho `[Authorize]` endpoint emergency-alerts |


---

## 4. Endpoint mới (đi qua Gateway YARP có sẵn — không đổi cấu hình Gateway)

| Endpoint | Story | Auth |
|---|---|---|
| `POST /api/v1/parking-lots/{lotId}/operations/heartbeat` | US-068 | Owner(own)/Staff/Admin |
| `GET  /api/v1/parking-lots/{lotId}/operations/overview` | US-067 | Owner(own)/Staff/Admin |
| `POST /api/v1/parking-lots/{lotId}/operations/emergency-stop` | US-069 | Owner(own)/Staff/Admin + confirm |
| `POST /api/v1/parking-lots/{lotId}/operations/emergency-stop/lift` | US-069 | Owner(own)/Staff/Admin |
| `GET  /api/v1/parking-lots/{lotId}/operations/emergency-logs` | US-069 | Owner(own)/Staff/Admin |
| `GET  /api/v1/parking-lots/{lotId}/operations/status` | US-069 | nội bộ service-to-service (anonymous) |
| `GET  /api/v1/bookings/internal/lots/{lotId}/overview` | US-067/069 | nội bộ service-to-service (anonymous) |
| `GET  /api/v1/notifications/emergency-alerts?lotId=` | US-086 | `[Authorize]` Staff/Admin; Owner(own) |
| `GET  /api/v1/notifications/emergency-alerts/{batchKey}/delivery-status` | US-086 | `[Authorize]` Staff/Admin; Owner(own) |
| SignalR `LotStatusChanged` trên `/hubs/parking` | US-067/068/069/086 | như hub hiện có |

---

## 5. Giả định (assumptions)

1. **Internal service-to-service endpoint anonymous** — khớp convention toàn repo (Gateway là chốt xác thực);
   chỉ endpoint user-facing mới thêm `[Authorize]` (NotificationService lần đầu có JWT auth → copy
   `Keys/public.key` của cụm service).
2. **Booking bị ảnh hưởng chỉ được THÔNG BÁO**, không tự hủy/hoàn tiền — khớp nội dung mẫu
   ("đội hỗ trợ sẽ liên hệ hoàn tiền/đổi bãi"); luồng hủy+refund tự động là follow-up.
3. **Fail-open khi check lot status** trước khi tạo booking (ưu tiên availability; ParkingService chết
   không chặn toàn bộ booking) — ghi chú trong `LotStatusProvider`.
4. `LotEmergencyLifted` chỉ có `OwnerProfileId` → chỉ chủ bãi nhận "bãi mở lại" (khách cần list booking
   ở thời điểm stop — xem limitations).
5. Notification MỚI của NotificationService dùng JWT; các endpoint cũ giữ nguyên hành vi (không breaking).

## 6. Hạn chế đã biết (known limitations)

1. Check-in chặn qua **eventual consistency** (event RabbitMQ → GateService cache): độ trễ vài giây
   giữa lúc bấm Emergency Stop và lúc gate thực sự chặn; endpoint `/operations/status` cho BookingService
   là pull real-time.
2. Payment đang in-flight cho booking bị ảnh hưởng không bị hủy — refund/đổi bãi xử lý thủ công
   (hoặc follow-up auto-cancel workflow).
3. Delivery-status API là **pull**; chưa push cập nhật tiến độ fan-out real-time cho người gửi.
4. SMS/FCM sender ở môi trường dev là **mock** (`UseMock: true`) — tracking trạng thái vẫn đúng luồng.
5. Baseline SQL cũ (T-SQL) chưa đồng bộ tên cột mới (xem §2) — cần đợt dọn docs sau.

## 7. Follow-up đề xuất

1. Auto-cancel + auto-refund booking bị ảnh hưởng khi Emergency Stop (workflow PaymentService).
2. Push delivery-status updates qua SignalR group `user-{ownerUserId}`.
3. Nối FE màn hình owner dashboard (hiện `NO_ENDPOINT` trong `Frontend/.../ParkingLotDetailPage.tsx`)
   với các endpoint operations đã có.
4. Thêm metric/structured log tổng hợp fan-out (số gửi/kênh/thời gian) vào observability stack.
5. Đồng bộ lại baseline `database/*.sql` với EF runtime schema.

