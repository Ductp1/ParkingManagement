# Chạy PostgreSQL và gửi OTP bằng email

PostgreSQL 17.11 đã cài local tại `.tools/postgres/pgsql`, dữ liệu tại `.tools/postgres/data`. Chỉ nghe 127.0.0.1:5432, dùng SCRAM và mật khẩu ngẫu nhiên lưu trong `Services/UserService/UserService.API/appsettings.Local.json`. Không đưa file này vào Git. Có hai DB: `pm_user` và `pm_user_test`; migration đã áp dụng cho pm_user. Đây là bản local, không tự chạy theo Windows service.

Trong terminal tại thư mục ParkingManagement:

```powershell
pwsh -File scripts/postgres-local.ps1 start
pwsh -File scripts/postgres-local.ps1 status
pwsh -File scripts/postgres-local.ps1 stop
```

## Cấu hình Gmail gửi OTP

1. Dùng tài khoản Gmail làm người gửi OTP. Email người đăng ký là người nhận, không cần mật khẩu của họ.
2. Bật Xác minh 2 bước của tài khoản gửi.
3. Mở https://myaccount.google.com/apppasswords, tạo mật khẩu ứng dụng tên ParkingManagement.
4. Chạy lệnh dưới đây; nhập email gửi và mật khẩu ứng dụng khi được hỏi. Mật khẩu nhập bị ẩn và chỉ lưu ở cấu hình local.

```powershell
pwsh -File scripts/configure-otp-email.ps1
dotnet run --project Services/UserService/UserService.API
```

Nếu dùng SMTP khác, truyền `-HostName` và `-Port` phù hợp. SMTP bắt buộc TLS, port 465 dùng SSL trực tiếp, các port khác dùng STARTTLS. Không bỏ kiểm tra chứng chỉ.

Local đã chọn Otp:Transport=Smtp. Thiếu thông tin SMTP sẽ trả lỗi cấu hình; chưa có email nào được gửi thật. HTTP adapter vẫn có để tích hợp/test, không ghi OTP vào log.

Outbox retry có thể gửi lại cùng email khi SMTP đã nhận nhưng DB chưa ghi hoàn tất. Message-Id giữ ổn định qua retry; SMTP không đảm bảo chống gửi trùng tuyệt đối. Mã đã hết hạn/tiêu thụ sẽ không gửi.

Nguồn: [PostgreSQL Windows](https://www.postgresql.org/download/windows/), [EDB binaries](https://www.enterprisedb.com/download-postgresql-binaries), [Google mật khẩu ứng dụng](https://support.google.com/accounts/answer/185833?hl=vi).
