# Kiểm tra phần TV1 — 05/10/2026

> Cập nhật 07/10/2026: danh sách thiếu bên dưới là snapshot ngày 05/10, không còn phản ánh mã nguồn hiện tại. Đã triển khai nghiệp vụ bắt buộc; xem `TV1_IMPLEMENTATION.md` cho trạng thái, điều kiện tích hợp và kiểm chứng mới nhất.

> Cập nhật 06/10/2026: người dùng đã yêu cầu bổ sung refresh/logout. Đã triển khai hai API, rotation/replay, logout thu hồi chuỗi phiên và route Auth tại Gateway. Xem `TV1_AUTH_FLOW.md` để biết hành vi và kiểm chứng mới. Các phần còn lại dưới đây ghi nhận lần review ngày 05/10.

Phạm vi: sửa lỗi trong chức năng đang có, không thêm API hoặc triển khai task mới. Đối chiếu với `TV1_TASKS.md`.

## Các lỗi đã sửa

1. Login: kiểm tra đầu vào rỗng và bỏ khoảng trắng đầu/cuối định danh, giữ nguyên mật khẩu.
2. Login: chặn tài khoản không Active, bị xóa hoặc đang khóa tạm thời.
3. Login: dùng TimeProvider để kiểm tra chính xác thời gian và kiểm thử được các mốc khóa/hết hạn.
4. Login: reset bộ đếm khi đạt ngưỡng khóa mật khẩu hiện có (5 lần), tránh khóa ngay lại ở lần sai đầu tiên sau khi hết hạn. Đăng nhập thành công xóa thời hạn/lý do khóa cũ. Ngưỡng 5 lần là hành vi cũ, không phải quy tắc OTP 3 lần.
5. Refresh token được phát trong login: dùng 32 byte ngẫu nhiên mật mã, lưu hash như trước và hết hạn sau 7 ngày.
6. Repository đăng nhập: tải OwnerProfile để JWT có Tenant Claim OwnerProfileId.
7. JWT: không sinh khóa ngẫu nhiên khi thiếu file; kiểm tra RSA tối thiểu 2048 bit, private key có thể ký và public/private thuộc cùng cặp. Access token RS256 có thời hạn đúng 24 giờ.
8. Đường dẫn khóa lấy từ Jwt:PublicKeyPath / Jwt:PrivateKeyPath và giải quyết theo ContentRootPath; mặc định Keys/public.key và Keys/private.key.
9. Xác minh JWT: chỉ chấp nhận thuật toán RS256; giữ kiểm tra issuer, audience, lifetime, chữ ký.
10. RBAC: giữ tên policy hiện có nhưng sửa role RequireOwner -> LotOwner, RequireGateKeeper -> Staff; cả bốn policy yêu cầu xác thực.
11. Hai API Users hiện có: yêu cầu đăng nhập; danh sách chỉ Admin; chi tiết chỉ chính chủ hoặc Admin.
12. Middleware dùng chung: chỉ bật authentication/authorization khi dịch vụ tương ứng đã đăng ký, tránh làm hỏng service chưa tích hợp JWT.

## Đối chiếu nhiệm vụ

| Hạng mục | Trạng thái trong mã nguồn |
|---|---|
| BCrypt cost 12 | Có |
| JWT RS256, access token 24 giờ | Có; cần cấu hình cặp khóa thực tế để chạy |
| Multi-role, ICurrentUser và OwnerProfileId | Có; đã sửa tải tenant claim |
| Bốn policy RBAC | Có; đã sửa tên role |
| Login và phát refresh token 7 ngày | Có |
| Refresh/logout | Chưa có API/use case |
| Đăng ký, OTP, khóa sau ba lần sai OTP | Chỉ có entity, chưa có luồng xử lý |
| GET Users chi tiết/danh sách | Có; đã giới hạn quyền |
| Outbox Dispatcher và phát sự kiện người dùng | Có hợp đồng/bảng nền tảng; chưa có dispatcher và luồng phát |
| Quên/reset mật khẩu, cập nhật hồ sơ, quản lý Staff, KYC, Data Requests | Chưa có API/use case |
| Tích hợp JWT ở Gateway và tám service khác | Chưa hoàn tất; không triển khai trong lần sửa này |

Không đánh dấu toàn bộ TV1 hoàn thành. Sự hiện diện của entity hoặc hợp đồng sự kiện không có nghĩa nghiệp vụ đã được triển khai.

## Kiểm chứng

- Build UserService.API thành công, 0 warning, 0 error (kiểm tra lại sau thay đổi cuối).
- 11 test UserService đạt, gồm 7 trường hợp mới kiểm tra lỗi đăng nhập (theory có ba trạng thái).
- Không xác nhận chạy HTTP với PostgreSQL hoặc xác thực JWT thực tế: chưa cấu hình cặp khóa triển khai và chưa chạy kiểm thử tích hợp.
- Không tạo/commit private key vào repository. Cấu hình khóa là điều kiện vận hành bắt buộc; thiếu public key hiện sẽ báo lỗi ngay khi đăng ký JWT.
