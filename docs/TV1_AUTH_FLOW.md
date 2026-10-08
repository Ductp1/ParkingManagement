# Login, refresh và logout — 06/10/2026

> Cập nhật 07/10/2026: đã thêm sid và kiểm tra phiên DB, cùng các nghiệp vụ TV1 còn lại. Tài liệu hiện hành đầy đủ ở `TV1_IMPLEMENTATION.md`.

Phạm vi: US-003 và logout, cấu hình route Gateway. Không bổ sung đăng ký, OTP, quên mật khẩu, KYC hoặc quản lý Staff.

## API

Gọi qua Gateway `http://localhost:5000` hoặc trực tiếp UserService `http://localhost:5101`. Đường dẫn thực tế dùng `/api/v1`, giữ quy ước hiện tại của project.

### Login

`POST /api/v1/auth/login`

```json
{"emailOrPhone":"driver1@smartparking.vn","password":"Demo@123"}
```

Trả HTTP 200 với `accessToken` và `refreshToken`. Sai thông tin đăng nhập hoặc tài khoản không được phép: 401. Đầu vào rỗng: 400. Token không được cache (`Cache-Control: no-store`).

Luồng: `AuthController.Login` → `LoginUseCase.ExecuteAsync` → `AuthRepository.GetUserByEmailOrPhoneAsync` → `UserDbContext.Users` → PostgreSQL. Repository tải Roles và OwnerProfile. Use case kiểm tra trạng thái, khóa, BCrypt password; gọi JwtTokenGenerator phát JWT RS256 24 giờ; tạo refresh token ngẫu nhiên 32 byte, lưu SHA-256 và hạn 7 ngày; gọi UpdateUserAsync → SaveChangesAsync. Trả TokenResponseDto → Controller.Ok → JSON.

### Refresh

`POST /api/v1/auth/refresh`

```json
{"refreshToken":"<refreshToken nhận từ login hoặc lần refresh gần nhất>"}
```

Trả HTTP 200 với cặp token mới. Token không tồn tại, hết hạn, đã bị thu hồi hoặc tài khoản không đủ điều kiện: 401. Sai định dạng: 400. Không cần access token vì nó có thể đã hết hạn; refresh token chính là bằng chứng sở hữu phiên.

Luồng: `AuthController.Refresh` → `RefreshSessionUseCase` → `SessionRepository.FindAsync` tra SHA-256 qua unique index TokenHash, tải user/role/tenant hiện tại → kiểm tra token/tài khoản → phát access token và tạo refresh token mới → `TryRotateAsync` → transaction PostgreSQL → khóa hàng User bằng SELECT FOR UPDATE → đọc lại token và kiểm tra tài khoản/hạn sau khi lấy khóa → UPDATE token cũ có điều kiện, ghi RevokedAtUtc và ReplacedByTokenHash → INSERT token mới → commit → trả cặp token mới.

Token mới giữ hạn kết thúc 7 ngày của phiên gốc, không kéo dài phiên vô hạn. Mỗi lần refresh phải dùng token mới nhất và thay cặp token lưu ở client. Client phải gom các request refresh đồng thời thành một request; gửi token cũ lần nữa được coi là replay và thu hồi cả chuỗi thay thế của phiên.

### Logout

`POST /api/v1/auth/logout`

```json
{"refreshToken":"<refreshToken của phiên cần đăng xuất>"}
```

Trả HTTP 204, không có body. Token đúng định dạng nhưng không tồn tại hoặc đã thu hồi vẫn trả 204 để logout có thể lặp lại an toàn và không tiết lộ phiên có tồn tại không. Sai định dạng trả 400.

Luồng: `AuthController.Logout` → `LogoutUseCase` → `SessionRepository.RevokeAsync` → transaction + cùng khóa User như refresh → theo ReplacedByTokenHash thu hồi token và các token thay thế → commit → Controller.NoContent. Không thu hồi các phiên login độc lập của cùng người dùng. Client xóa cả access token lẫn refresh token đang lưu.

Logout thu hồi quyền refresh và sid của access token. UserService kiểm tra phiên DB nên access token của phiên bị thu hồi sẽ bị từ chối ngay. Gateway/service khác nhận cùng hành vi khi bật RemoteSessionValidator; nếu chỉ kiểm tra chữ ký JWT thì exp vẫn là 24 giờ.

## Lý do chọn phương pháp

- Password vẫn BCrypt cost 12: mật khẩu do người chọn thường yếu, cần hash chậm để chống dò.
- Refresh token ngẫu nhiên mật mã 256 bit + SHA-256: token có entropy cao; hash cố định giúp tra cứu theo index thay vì quét tất cả BCrypt hash. Không lưu token thô vào DB.
- Rotation + phát hiện replay: token cũ không thể làm mới phiên lần hai; token bị dùng lại làm mất hiệu lực các token thay thế. Theo hướng dẫn refresh token rotation tại RFC 9700 §4.14: https://www.rfc-editor.org/rfc/rfc9700.html#section-4.14
- Transaction + khóa ở PostgreSQL: refresh và logout không thể đồng thời thay đổi cùng chuỗi phiên, kể cả chạy nhiều instance. Lỗi INSERT hoặc commit sẽ rollback việc thu hồi token cũ. ExecuteUpdate cần transaction rõ ràng khi kết hợp nhiều thao tác: https://learn.microsoft.com/en-us/ef/core/saving/execute-insert-update-delete
- Khóa hàng User thay vì khóa trong bộ nhớ: một khóa DB dùng chung cho các phiên của user, tránh vấn đề khóa token cũ nhưng một request khác đang xoay token con. Đánh đổi là các thao tác refresh/logout của cùng user chạy tuần tự.
- Giữ Controller → UseCase → Repository: HTTP, nghiệp vụ và SQL được tách ra; có thể kiểm thử nghiệp vụ bằng fake repository mà không cần DB.
- Giữ bảng/cột RefreshTokens hiện có: không cần migration mới.

## Tương thích và kiểm chứng

- Các refresh token được phát trước thay đổi này dùng BCrypt hash nên không tra được bằng SHA-256: người dùng cần login lại một lần. Không xóa dữ liệu hoặc âm thầm quét BCrypt toàn bảng để tương thích.
- Cần public/private key RSA được cấu hình theo Jwt:PublicKeyPath / Jwt:PrivateKeyPath. Không lưu private key vào Git.
- 28 unit test UserService bao gồm login, refresh, token hết hạn, trạng thái user, replay, logout lặp lại và nhánh mất lượt rotation. Unit test dùng fake repository; không chứng minh khóa/transaction PostgreSQL hoạt động thực tế.
- PostgreSQL localhost:5432 không truy cập được trong lần kiểm tra này; chưa chạy integration test DB/HTTP. Build API được kiểm tra riêng.
