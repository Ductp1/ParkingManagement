# CAPTCHA cho login TV1

Login yêu cầu captchaToken. UserService xác minh bằng Cloudflare Turnstile Siteverify trước khi tìm tài khoản/kiểm tra mật khẩu. Không thêm CAPTCHA cho register, OTP, refresh hoặc logout.

## Cấu hình thật
Thiết lập local bằng khóa thật: trong Cloudflare dashboard → Turnstile → Add widget, đặt tên ParkMaster, thêm hostname `localhost` (không có port), chọn Managed và tạo widget. Lấy SiteKey/SecretKey của cùng widget.

Từ thư mục gốc project chạy `pwsh -File scripts/configure-captcha.ps1`. Script nhập SecretKey ẩn, từ chối khóa test, giữ các cấu hình khác và ghi SiteKey vào `Frontend/ParkingManagement.Web/.env.development.local`, SecretKey vào `Services/UserService/UserService.API/appsettings.Local.json`; cả hai file được Git bỏ qua. Khởi động lại UserService/Vite rồi thử đăng nhập. Nếu đang đặt biến môi trường `Captcha__SecretKey` hoặc `Captcha__ExpectedAction`, cần cập nhật chúng vì chúng ưu tiên hơn JSON. Với hostname khác, truyền `-AllowedHostnames` và thêm cùng hostname trong Cloudflare dashboard.

Script này cấu hình môi trường local. Khi deploy, đặt SiteKey trong môi trường build frontend và SecretKey trong môi trường backend như dưới đây. Chưa có cặp khóa thật thì CAPTCHA và đăng nhập chưa sử dụng được; không thay bằng khóa thử nghiệm.

1. Tạo widget Turnstile trong Cloudflare dashboard, khai báo hostname frontend.
2. Backend: đặt Captcha__SecretKey bằng biến môi trường hoặc User Secrets. Không commit secret.
3. Đặt Captcha__AllowedHostnames__0 (và __1 nếu cần) thành hostname frontend, không chứa scheme, port hoặc path.
4. Frontend: copy .env.example thành .env.local và đặt VITE_TURNSTILE_SITE_KEY bằng public sitekey; khởi động lại Vite/build.
5. Giữ Captcha:Enabled=true, ExpectedAction=login, TimeoutSeconds=5.

Ngoài Development: cấm tắt CAPTCHA và cấm 3 secret test công khai. Backend thiếu secret/hostname hợp lệ sẽ không khởi động. Development chỉ tắt CAPTCHA khi cấu hình rõ Captcha__Enabled=false; frontend login vẫn yêu cầu widget.

## Local test
Dùng test keys chính thức: https://developers.cloudflare.com/turnstile/troubleshooting/testing/
Sitekey luôn pass: 1x00000000000000000000AA
Secret luôn pass: 1x0000000000000000000000000000000AA
Chỉ dùng trong Development. Dummy response có action=test; đặt Captcha__ExpectedAction=test và Captcha__AllowedHostnames__0=localhost khi dùng bộ keys này, mở frontend bằng http://localhost:5173. Ngoài Development bắt buộc action=login và từ chối test secret. Khi dùng keys thật (kể cả trên localhost) trả ExpectedAction về login.

PowerShell trước khi chạy UserService:
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:Captcha__SecretKey = '1x0000000000000000000000000000000AA'
$env:Captcha__ExpectedAction = 'test'
$env:Captcha__AllowedHostnames__0 = 'localhost'

Frontend .env.local:
VITE_TURNSTILE_SITE_KEY=1x00000000000000000000AA
Unit/API tests mock verifier/HTTP, không gọi Cloudflare. Không dùng key test ở production.

## API
POST /api/v1/auth/login
Body: {"emailOrPhone":"...","password":"...","captchaToken":"..."}
CAPTCHA thiếu/sai/hết hạn/dùng lại/sai hostname/action: HTTP 400, ProblemDetails.code=CAPTCHA_INVALID.
Provider lỗi/timeout: HTTP 503, code=CAPTCHA_UNAVAILABLE. Không bỏ qua CAPTCHA khi provider lỗi.
CAPTCHA lỗi không tăng FailedLoginCount. Rate limiting và khóa sau 5 lần sai password vẫn giữ.
Token CAPTCHA dùng một lần, hết hạn sau 300 giây. Frontend làm mới widget sau mỗi lần gửi thất bại.

## Frontend và phiên
/login dùng API login thật rồi GET /api/v1/users/me. Khởi tạo chưa đăng nhập, kiểm tra phiên khi tải lại trang.
Access token và refresh token giữ trong sessionStorage (chỉ tab hiện tại); reload vẫn giữ phiên, đóng tab sẽ mất dữ liệu phiên tại client. Tự refresh khi khôi phục phiên nhận 401.
Đăng xuất gọi API thu hồi refresh token trước khi xóa dữ liệu local. Lỗi thu hồi hiển thị để thử lại.
Phân hệ chỉ chọn vai trò do API trả về; LotOwner được map thành Owner ở frontend.

## Kiểm chứng
dotnet test Services/UserService/UserService.Test/UserService.Test.csproj --no-restore
dotnet test Tests/ParkingManagement.IntegrationTests/ParkingManagement.IntegrationTests.csproj --no-restore --filter FullyQualifiedName~CaptchaApiTests
cd Frontend/ParkingManagement.Web
npm run build
npm run lint
Test vòng đời đăng ký/login/refresh/logout với PostgreSQL cần TV1_TEST_DB trỏ DB test riêng; CAPTCHA được mock ở fixture đó.

Manual: thử CAPTCHA thật trên localhost, sai mật khẩu, widget hết hạn, lỗi tải script, login thành công, reload, logout, và gọi login trực tiếp thiếu captchaToken. Keys thật và kết nối Cloudflare cần người vận hành cấu hình.
