# RabbitMQ qua MassTransit — TV1 Event Bus

## Chuyển sang MassTransit (30/10/2026)

- Runtime dùng MassTransit.RabbitMQ **8.5.8**, license Apache-2.0; không dùng v9 thương mại. RabbitMQ vẫn là broker, credentials/compose/port giữ nguyên.
- Đã bỏ RabbitMqConnection và consumer BackgroundService tự quản lý channel. MassTransit quản lý connection, reconnect, nhận message và ack. Native RabbitMQ.Client chỉ dùng trực tiếp trong test để kiểm tra wire format/topology.
- Giữ OutboxDispatcher và TransactionalInbox của dự án: không thêm schema/migration MassTransit Outbox và không chạy hai dispatcher cho cùng bản ghi.
- Publisher dùng raw JSON để giữ envelope eventId/eventType/occurredAtUtc/payload, topic exchange parking.events, routing key theo EventType, MessageId=EventId, persistent, mandatory và publisher confirm.
- Consumer triển khai IConsumer<IntegrationEventEnvelope>, được đăng ký qua AddRabbitMqInboxConsumer<TDbContext,THandler>. Dùng helper này MỘT lần thay cho AddRabbitMqEventBus khi service vừa nhận vừa gửi; helper đã đăng ký cả producer. Service chỉ gửi dùng AddRabbitMqEventBus.
- Lỗi handler được retry ngay tối đa 3 lần (4 lần tính lần đầu). JsonException do envelope sai không retry. Sau lỗi cuối, MassTransit đưa message vào **<queue>_error**; message không có consumer phù hợp có thể vào **<queue>_skipped**. Không tự replay queue lỗi; giữ EventId khi replay và điều tra nguyên nhân.
- Các queue **.dead cũ được giữ** để không mất message. Chúng không phải nơi tìm lỗi mới của consumer MassTransit. Không xóa/recreate queue/volume để chuyển phiên bản. Chưa có consumer business của các team khác thì queue chính tiếp tục giữ Ready.
- Consumer raw JSON chấp nhận message cũ không có header MT-MessageType. Inbox commit cùng DB effects trước khi MassTransit ack. Outbox/Inbox không bảo đảm tác dụng phụ ngoài DB (email/API) chỉ xảy ra một lần.
- Chưa tự commit/push lượt chuyển đổi này. Cần build và restart service để runtime mới có hiệu lực. Không dùng song song consumer cũ và mới cho cùng queue.

Phần bên dưới mô tả setup broker và nền tảng ban đầu; khi có khác biệt về consumer/error queue, ưu tiên mục chuyển đổi này.


UserService gửi `UserRegistered`, `OwnerProfileCreated`, `UserLocked` qua RabbitMQ.Client 7.2.2. OTP giữ SMTP/HTTP riêng. Không cần đổi schema UserService.

## Chạy local

Để demo TV1 trên máy ít RAM, sau khi đã build và chạy PostgreSQL/RabbitMQ/frontend, dùng PowerShell 7.2+: `./scripts/start-tv1-demo.ps1`. Script chỉ chạy UserService và gateway từ executable có sẵn, giữ cấu hình local, không gọi MSBuild hay khởi động cả 9 service. Chạy lại sẽ giữ host đang chạy; log nằm trong `test-results/*.demo.*.log`.

Cần Docker Engine và Docker Compose đang chạy. Tại thư mục gốc repository, dùng PowerShell 7.2+:

```powershell
./scripts/setup-rabbitmq-local.ps1
docker compose --env-file .env.rabbitmq.local -f compose.rabbitmq.yml up -d --wait
docker compose --env-file .env.rabbitmq.local -f compose.rabbitmq.yml ps
```

Khởi động lại UserService để đọc cấu hình mới. Management UI: http://localhost:15672. Tài khoản local nằm trong `.env.rabbitmq.local`; script tạo mật khẩu ngẫu nhiên, đồng bộ vào `appsettings.Local.json` và tạo `definitions.local.json` với password hash. Cả ba file chứa thông tin nhạy cảm đều bị Git bỏ qua. Chạy lại script giữ mật khẩu hiện có. Không chỉ chạy compose khi chưa chạy setup.

Volume giữ dữ liệu broker qua các lần restart. Đổi mật khẩu trong file không tự đổi user đã có trong volume; cần đổi trên broker và đồng bộ cấu hình client. Không xóa volume đang chứa dữ liệu cần giữ.

## Luồng và bảo đảm

Nghiệp vụ và OutboxMessage ghi cùng transaction. Dispatcher gửi lên durable topic exchange `parking.events`, routing key là tên sự kiện, message persistent, MessageId là EventId. Publisher dùng mandatory và chờ publisher confirm; lỗi gửi, nack hoặc không có queue nhận sẽ không đánh dấu Outbox đã xử lý. Retry dùng backoff hiện có, tối đa `EventBus:MaxAttempts` (mặc định 10). Bản ghi hết lượt vẫn được giữ để điều tra; sau khắc phục cần phục hồi có kiểm soát.

Confirm xác nhận broker nhận message, không xác nhận service nhận đã hoàn thành nghiệp vụ. Nếu gửi thành công rồi process chết trước khi đánh dấu Outbox, message có thể được gửi lại. Consumer chống trùng bằng InboxReceipt/EventId trong cùng transaction với thay đổi DB; chỉ ack sau commit. Nếu ack lỗi sau commit, reconnect và nhận lại để deduplicate.

Envelope JSON:

```json
{"eventId":"guid","eventType":"UserRegistered","occurredAtUtc":"UTC timestamp","payload":{"UserId":123}}
```

Payload giữ cấu trúc integration event hiện có. Consumer cần dùng cùng EventId khi replay.

## Topology bàn giao

| Team | Queue |
|---|---|
| TV3 | parking.users |
| TV4 | booking.users |
| TV6 | notification.users |
| TV8 | admin.users |
| TV9 | support.users |

Bootstrap local tạo 5 quorum queue, mỗi queue bind cả ba loại sự kiện trên, cùng một queue `.dead` tương ứng. Mỗi service nhận bản riêng; nhiều instance cùng service dùng chung queue để chia tải. Team nhận có thể chọn lại routing keys theo nhu cầu khi triển khai. TV1 chỉ cung cấp nền tảng, chưa viết handler nghiệp vụ của các team khác.

Handler lỗi hoặc payload không hợp lệ được nack không requeue, chuyển tới `<queue>.dead` qua exchange `parking.events.dead`. Không tự retry vô hạn consumer. Kiểm tra lỗi, sửa nguyên nhân rồi replay có kiểm soát và giữ EventId. DLQ không thay thế việc giám sát queue/Outbox lỗi.

## Tích hợp service nhận

Đăng ký DbContext kế thừa ServiceDbContext, gọi `modelBuilder.ConfigureInbox()` và tạo migration trước khi bật consumer. Handler scoped triển khai `IIntegrationEventHandler`, chỉ thao tác DB trên cùng DbContext trong callback. Không gọi tác dụng phụ ngoài DB trong transaction; muốn phát sự kiện tiếp thì ghi Outbox.

```csharp
builder.Services.AddRabbitMqInboxConsumer<ParkingDbContext, UserEventHandler>(builder.Configuration);
```

Cấu hình `EventBus:RabbitMq` giống UserService (cấp credentials riêng khi deploy); thêm:

```json
{"EventBus":{"Subscription":{"Queue":"parking.users","RoutingKeys":["UserRegistered","OwnerProfileCreated","UserLocked"],"PrefetchCount":10}}}
```

Producer service khác dùng `AddRabbitMqEventBus`, đăng ký OutboxDispatcher/TimeProvider/DbContext và bật EventBus. Nếu dùng retry schema, opt-in `OutboxMessage.NextAttemptAtUtc` trong model và migration tương ứng. UserService đã có dispatcher và schema, IdentityOutboxTransport phân luồng OTP và integration event.

## Kiểm thử

```powershell
./scripts/test-rabbitmq.ps1
```

Script đọc credentials local vào biến môi trường tạm, không in mật khẩu. Broker phải chạy. Hai test broker kiểm tra fan-out, persistent message, EventId ổn định, mandatory unroutable và phục hồi publisher. Đặt thêm `TV1_TEST_DB` tới PostgreSQL test riêng, tài khoản có quyền tạo database, để chạy test consumer thực: duplicate, rollback và dead-letter. Test tự tạo/dọn exchange/queue/DB với tên riêng. Không đặt `TV1_TEST_RABBITMQ` thì bộ test thông thường skip các test broker; không có nghĩa đã kiểm thử broker thật.

## Deploy

Compose và user administrator trên đây dành cho local. Production cần tài khoản riêng theo service với quyền tối thiểu, secrets ngoài source, TLS với chứng chỉ hợp lệ, persistent storage và phương án vận hành/backup/cluster phù hợp. Client yêu cầu TLS khi production kết nối host ngoài loopback. Một broker local chưa cung cấp tính sẵn sàng cao.

Tham khảo: [RabbitMQ .NET guide](https://www.rabbitmq.com/client-libraries/dotnet-api-guide), [definitions](https://www.rabbitmq.com/docs/definitions), [password hashing](https://www.rabbitmq.com/docs/passwords).

Kết quả local 28/10/2026: 107 test UserService đạt (gồm PostgreSQL Outbox/Inbox), 6/6 test HTTP UserService đạt với publisher giả lập. Sau khi Docker Desktop sẵn sàng, RabbitMQ local đã Healthy và bộ test RabbitMQ đạt 8/8, không skip, gồm cả ba test broker thật (fan-out/persistent, unroutable và phục hồi publisher, Inbox rollback/deduplicate/DLQ trên PostgreSQL thật). Chưa kiểm thử handler nghiệp vụ các service khác.

Kiểm chứng MassTransit 30/10/2026: 110/110 test UserService, 6/6 HTTP đạt, không skip; producer/consumer trên broker thật và test legacy JSON/retry/Inbox. UserService đã build/restart, log Bus started và health 200. Còn warning Pkcs đã biết.
