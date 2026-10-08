# TV1 — Phạm vi Sprint 1 (08/10/2026)

Phạm vi hiện tại theo yêu cầu người dùng: T-102, T-104, US-001, US-003. Tài liệu TV1_TASKS.md vẫn giữ lộ trình tổng thể để tham khảo, không phải danh sách chức năng đang bật.

## Đã giữ

- T-102: phát JWT RS256 tại UserService, access token 24 giờ, public key kiểm tra ở Gateway, AddJwtAuth dùng chung.
- T-104: ICurrentUser, tenant OwnerProfileId, multi-role và 4 policy Admin/LotOwner/Staff/Driver. Đọc phân công Staff có sẵn để tạo claim, không có API quản lý Staff.
- US-001: đăng ký Driver/LotOwner bằng email/SĐT, xác nhận/gửi lại OTP; 6 số, đúng 300 giây, sai 3 lần khóa 15 phút. BCrypt cost 12.
- US-003: login, refresh token 7 ngày, rotation, replay protection, logout; kiểm tra sid để phiên thu hồi không tiếp tục dùng access token.
- API đọc Users có sẵn: chính chủ/Admin xem chi tiết, Admin xem danh sách; GET me phục vụ kiểm tra người dùng đăng nhập.
- Nền tảng gửi OTP qua HTTP/outbox, AES bảo vệ mã trong hàng đợi, retry và dọn token/OTP hết hạn. Đây là phụ thuộc của đăng ký OTP; không có API vận hành Event Bus.
- Giữ entity, bảng và lịch sử migration nền tảng; không xóa dữ liệu database.

## Đã gỡ

- US-004: forgot/reset password và mục đích ResetPassword ở API resend.
- US-006: PUT me, use case cập nhật hồ sơ.
- US-075: API tạo/list/xóa Staff, adapter xác minh sở hữu bãi và route owners ở Gateway.
- US-005: API upload/xem/duyệt KYC, lưu file private.
- US-007: API yêu cầu/xuất/sửa/xóa dữ liệu cá nhân, xử lý file export/deletion.
- API Admin vận hành outbox.
- API Admin khóa/mở thủ công đã thêm ở lần triển khai toàn bộ. Tự khóa OTP/login vẫn giữ.
- Các DTO, repository method, DI, cấu hình và test riêng của các phần trên.

Không thay đổi nghiệp vụ các service TV2–TV9. Sinh trắc học không triển khai.

## API đang có

- POST /api/v1/auth/register
- POST /api/v1/auth/otp/verify
- POST /api/v1/auth/otp/resend (purpose Register)
- POST /api/v1/auth/login
- POST /api/v1/auth/refresh
- POST /api/v1/auth/logout
- GET /api/v1/auth/session (kiểm tra phiên cho Gateway/service)
- GET /api/v1/users/me
- GET /api/v1/users/{id}
- GET /api/v1/users?role=&page=&pageSize=

Register: {"fullName":"Nguyễn Văn A","contact":"a@example.com","password":"Password123","role":"Driver"}
Owner thêm role LotOwner và businessName.
Verify: {"contact":"a@example.com","code":"012345"}
Resend: {"contact":"a@example.com","purpose":"Register"}

## Đọc code theo luồng

- AuthController -> AccountUseCases -> IdentityStore -> UserDbContext: đăng ký/OTP.
- AuthController -> LoginUseCase -> AuthRepository -> PasswordHasher/JwtTokenGenerator: login.
- AuthController -> RefreshSessionUseCase/LogoutUseCase -> SessionRepository: refresh/logout.
- AuthExtensions -> UserPrincipalValidator hoặc RemoteSessionValidator -> CurrentUser: JWT, phiên, role và tenant.
- IdentityOutboxTransport + OutboxDispatcher: gửi OTP và sự kiện đã ghi trong transaction.

## Cấu hình và kiểm chứng

scripts/setup-tv1-local.ps1 tạo cặp RSA và AES key, không ghi đè khóa không khớp; yêu cầu PowerShell 7.2+.
Cần PostgreSQL, Jwt:PrivateKeyPath/PublicKeyPath, Security:EncryptionKey, Otp:DeliveryUrl.
EventBus:Enabled phải bật để giao OTP; EventBus:PublishUrl là điểm nhận sự kiện khi tích hợp.
Gateway/service tích hợp dùng public key và Jwt:SessionValidationUrl trỏ GET /api/v1/auth/session.
Provider OTP phải hỗ trợ Idempotency-Key, HTTPS (Development cho phép localhost HTTP).

Build solution: 0 warning, 0 error. 55 test UserService đạt.
3 test tích hợp đăng ký/login/refresh/logout, khóa OTP và refresh đồng thời đã đạt trên PostgreSQL local thật ngày 08/10/2026. Kênh email bên ngoài được mock trong integration test; chưa gửi mail thật.
PostgreSQL local và cách nhập tài khoản email: xem LOCAL_POSTGRES_EMAIL.md. SMTP đã triển khai, cấu hình tài khoản gửi còn trống. Đã sửa lỗi RSA signer cache giữ khóa đã dispose phát hiện qua integration test.