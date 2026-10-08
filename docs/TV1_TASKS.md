> Phạm vi đang triển khai từ 08/10/2026: chỉ T-102, T-104, US-001, US-003 và quy tắc bảo mật liên quan. Các sprint sau là lộ trình, đã gỡ API/logic triển khai thêm theo yêu cầu người dùng. Xem TV1_IMPLEMENTATION.md.

# 🎯 TỔNG HỢP NHIỆM VỤ CỦA TV1 (UserService - Xác thực & Bảo mật)

Đây là tài liệu "kim chỉ nam" thống kê toàn bộ trách nhiệm và các công việc (Task/User Story) mà TV1 phải hoàn thành trong dự án ParkingManagement.

Tài liệu do người dùng cung cấp. Danh sách dưới đây là lộ trình tổng thể; không đồng nghĩa với yêu cầu triển khai tất cả trong một lần làm việc.

## 1. Trách nhiệm Cốt lõi (Domain Ownership)

- **Microservice:** Sở hữu và phát triển toàn bộ `UserService` (Cổng API Xác thực cho toàn bộ hệ thống).
- **Database:** Quản lý `PM_UserDb` gồm các bảng: `Users`, `UserRoles`, `OtpCodes`, `RefreshTokens`, `OwnerProfiles`, `StaffAssignments`, `SecurityEvents`, `DataSubjectRequests`.
- **Core / BuildingBlocks:** Đóng vai trò làm "người đổ móng" cho cả nhóm. Code của TV1 (như Auth, ICurrentUser, EventBus) sẽ được 8 service còn lại tái sử dụng.
- **Sự kiện (Event Driven):** Chịu trách nhiệm bắn các tín hiệu: `UserRegistered`, `UserLocked`, `OwnerProfileCreated` ra Event Bus.

## 2. Các Quy tắc Nghiệp vụ Bắt buộc (Business Rules)

1. **OTP:** Phải là 6 số, hiệu lực trong đúng 300 giây (5 phút). Nhập sai 3 lần => Khóa tài khoản 15 phút.
2. **Mật khẩu:** Bắt buộc băm bằng thuật toán `BCrypt` với độ khó (cost) = 12. Không bao giờ lưu plain text.
3. **Token (JWT):** Dùng thuật toán chữ ký phi đối xứng **RS256**. Access Token sống 24 giờ. Refresh Token sống 7 ngày.
4. **Phân quyền (RBAC):** Một tài khoản có thể có nhiều vai trò (Multi-roles) cùng lúc.

## 3. Lộ trình Triển khai (Theo Từng Sprint)

### Sprint 0 (Nền móng hạ tầng) - ✅ Đã hoàn thành

- **[T-101]** Cấu hình CI/CD GitHub Actions: Build và Test 9 service mỗi khi có PR. Chặn merge nếu bị đỏ. (Đội trưởng đã setup sẵn `ci.yml`).

### Sprint 1 (Xác thực & Bảo mật cơ bản) - ⏳ Đang tiến hành

- **[T-102]** Nền tảng JWT RS256: Phát token tại UserService, viết hàm `AddJwtAuth()` tại `ServiceDefaults` cho Gateway và các service khác dùng chung.
- **[T-104]** Nền tảng Định danh: Tạo `ICurrentUser` bóc tách claims (đặc biệt là Tenant Claim `OwnerProfileId`) và cài đặt 4 policy RBAC.
- **[US-001]** API Đăng ký tài khoản (Bằng SĐT/Email + Xác nhận OTP).
- **[US-002]** Logic khóa tài khoản (Security): Tự động khóa tạm thời khi nhập sai OTP 3 lần.
- **[US-003]** API Đăng nhập và Duy trì phiên (Trả về Access Token & Refresh Token).
- **[US-102]** Phân quyền RBAC & cô lập dữ liệu (Multi-tenant): Code logic chặn quyền các API dựa theo Role.
- **Giao nộp chéo (Cross-team):** Hoàn thiện Auth/RBAC trước **16/10** để cả nhóm tích hợp. API Khóa/mở tài khoản giao cho TV8 trong S1.

### Sprint 2 (Quản lý Hồ sơ & Message Broker)

- **[T-103]** Cấu hình Event Bus: Cài đặt **Outbox Dispatcher** (BackgroundService) để đọc bảng `OutboxMessages` và gửi sự kiện đi qua HTTP/RabbitMQ. Xử lý retry và Idempotent. (Bàn giao trước **06/11** cho TV3, TV4, TV6, TV8, TV9).
- **[US-004]** API Quên mật khẩu / Đặt lại mật khẩu.
- **[US-006]** API Quản lý hồ sơ cá nhân.
- **[US-075]** API Tạo và quản lý tài khoản Staff (Dành riêng cho Owner quản lý nhân viên bãi đỗ).

### Sprint 3 (Nâng cao)

- **[US-005]** Hệ thống định danh điện tử KYC: Upload và xác thực CCCD/GPLX (Mã hóa số giấy tờ bằng AES-256).

### Sprint 4 (Tuân thủ Pháp lý)

- **[US-007]** Quyền dữ liệu cá nhân (GDPR/Data Privacy): Cho phép người dùng xem, trích xuất (Data Requests), sửa và yêu cầu xóa vĩnh viễn dữ liệu cá nhân của họ.

### Phase 2/3 (Tính năng mở rộng - Tùy chọn)

- **[US-008]** Đăng nhập bằng sinh trắc học (FaceID / TouchID).

## 4. Danh sách các API phải code (Checklist)

### Nhóm Auth (Đăng ký/Đăng nhập)

- `POST /api/auth/register`
- `POST /api/auth/otp/verify`
- `POST /api/auth/otp/resend`
- `POST /api/auth/login` (Đã hoàn thiện UseCase)
- `POST /api/auth/refresh`
- `POST /api/auth/logout`
- `POST /api/auth/password/forgot`
- `POST /api/auth/password/reset`

### Nhóm Quản lý Người dùng (Users)

- `GET/PUT /api/users/me` (Profile của tôi)
- `POST /api/users/me/kyc` (Gửi định danh)
- `POST /api/users/me/data-requests` (Yêu cầu trích xuất dữ liệu)
- `POST /api/users/{id}/lock` và `unlock` (Dành cho Admin/Hệ thống)
- `GET /api/users/{id}` (Đã có sẵn)
- `GET /api/users?role=&page=` (Đã có sẵn)

### Nhóm Chủ bãi (Owner & Staff)

- `GET /api/owners/{id}/staff`
- `POST /api/owners/{id}/staff`
- `DELETE /api/owners/{id}/staff`
