# Playwright E2E – 3 luồng nghiệm thu (T-903)

Kiểm thử đầu-cuối trên hệ thống thật (9 service + Gateway + PostgreSQL + frontend React):

| Luồng | File | Nội dung chính |
|---|---|---|
| 1 – Tài xế | `tests/api/driver.spec.ts`, `tests/ui/flows.spec.ts` | Tìm bãi theo GPS (US-014), chi tiết bãi (US-018), đặt chỗ + giữ 15' + QR (US-022/026), qua cổng → Completed, đánh giá (US-093), khiếu nại (US-088), FAQ (US-092) |
| 2 – Cổng | `tests/api/gate.spec.ts`, `tests/ui/flows.spec.ts` | QR hợp lệ / đã dùng / giả / sai bãi (US-043, T-704), booking chưa Confirmed, chốt phí ân hạn 15' & check-out (US-045/046), khách vãng lai (T-702) |
| 3 – Chủ bãi & Admin | `tests/api/owner-admin.spec.ts`, `tests/ui/flows.spec.ts` | 401/403 theo vai trò, phản hồi tranh chấp (US-089), phản hồi / khiếu nại / gỡ / bác đánh giá (US-094), audit log (US-100) |
| Chung | `tests/ui/auth.spec.ts` | Đăng nhập form theo 4 vai trò, sai mật khẩu, route guard, màn 403 |

- **Project `api`**: gọi API qua Gateway, không cần trình duyệt – phủ trọn 3 luồng.
- **Project `ui`**: Chromium trên Vite dev server – chỉ các màn đã nối API thật. Màn còn dữ liệu giả để `test.fixme` (hiện “skipped”), bỏ fixme khi frontend nối API.

## Chạy trên máy

Yêu cầu: PostgreSQL theo `.env` ở thư mục gốc (giống `run-all.ps1`), .NET 10, Node 20+, PowerShell 7.

```powershell
cd Tests/e2e
npm install
npx playwright install chromium
cd ../../Frontend/ParkingManagement.Web; npm install; cd ../../Tests/e2e   # cho project ui

# 1) Bật hệ thống E2E (xóa + seed lại DB pm_e2e_*, build, chạy nền 9 service + Gateway)
./scripts/start-e2e-stack.ps1

# 2) Chạy test
npm test              # cả api + ui (tự bật Vite :5173)
npm run test:api      # chỉ API
npm run test:ui       # chỉ giao diện
npm run report        # mở báo cáo HTML (trace + ảnh khi lỗi)

# 3) Tắt hệ thống
./scripts/start-e2e-stack.ps1 -Stop
```

Lưu ý:
- Script cần **PowerShell 7** (`pwsh`) và **tắt hệ thống dev trước** (run-all.ps1 / Visual Studio) – nếu cổng 5000, 5101–5109 đang bận script sẽ dừng, tránh test chạy nhầm vào DB dev.
- Chạy script trực tiếp trong terminal, không nối `| ...` (tiến trình nền giữ pipe nên lệnh không trả về).
- Biến môi trường E2E chỉ truyền cho tiến trình con; terminal của bạn được khôi phục như cũ sau khi script chạy xong.
- **Mỗi lần chạy lại `npm test` (project api) cần chạy lại `./scripts/start-e2e-stack.ps1` trước** – tranh chấp CP-0001 chỉ phản hồi được 1 lần. Bước kiểm tra trước (`global-setup.ts`) sẽ báo rõ nếu bạn quên.

## Môi trường E2E khác dev ở đâu

`start-e2e-stack.ps1` tách hẳn khỏi dữ liệu dev:

| Thiết lập | Lý do |
|---|---|
| Database `pm_e2e_*`, xóa & seed lại mỗi lần (`-KeepData` để giữ) | Dữ liệu seed sạch → ID khớp `DemoIds`, CP-0001 chưa phản hồi, kết quả lặp lại được. Không đụng DB dev `pm_*` |
| Cặp khóa JWT tạm trong `.keys/` (git ignore) | Không cần private key thật của nhóm |
| `Captcha__Enabled=false` (chỉ hợp lệ ở Development) | UI test giả lập widget Turnstile, không gọi Cloudflare |
| `EventBus__Enabled=false` | Không phụ thuộc RabbitMQ (sự kiện tích hợp không được kiểm ở E2E) |
| `QRTOKEN_SECRET` sinh ngẫu nhiên | BookingService và GateService dùng chung khi ký / kiểm QR |

UI test chặn mọi request ra ngoài `localhost` (Google Fonts, cdnjs…) – `index.html` tải CSS chặn hiển thị từ CDN nên mạng chậm làm trang trắng.

Biến môi trường tùy chọn: `E2E_GATEWAY_URL` (mặc định `http://localhost:5000`), `E2E_WEB_URL` (`http://localhost:5173`), `E2E_DEMO_PASSWORD` (mặc định đọc từ `DemoIds.cs`).

## Lỗi / khoảng trống phát hiện khi chạy E2E

| Mã | Mô tả | Cách test xử lý |
|---|---|---|
| **BUG-E2E-01** (TV7 / TV3) | GateService đồng bộ check-in/check-out gửi `"gateDeviceId": null`, nhưng `GateActionCommand.GateDeviceId` của BookingService là `int` → 400. Lượt gửi xe vẫn tạo, staff nhận 503, booking kẹt ở `Confirmed`, check-out chỉ cảnh báo | Test `BUG-E2E-01` dùng `test.fail()` – **sẽ báo đỏ khi lỗi được sửa** (lúc đó xóa dòng `test.fail`). Các luồng khác đi vòng bằng endpoint nội bộ của BookingService và gắn annotation `known-bug` |
| **GAP-E2E-02** (TV3 / TV4) | BookingService chưa nhận `PaymentSucceeded` → booking không tự `Confirmed` sau thanh toán | E2E xác nhận booking bằng `POST /bookings/{code}/approve` của chủ bãi |
| GAP-E2E-03 (TV3) | `BookingDetailDto` chưa trả `OwnerProfileId` → khiếu nại mới không chuyển cho chủ bãi (US-089) | Luồng tranh chấp dùng CP-0001 trong seed |
| Ghi chú UI | Chủ bãi Vincom có cả role Driver → đăng nhập vào `/driver` thay vì `/owner` | UI test chủ bãi dùng tài khoản TSN |
