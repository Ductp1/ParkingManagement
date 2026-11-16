# Kiểm thử tải nhẹ bằng k6 (T-904)

Đi qua API Gateway như client thật, với 4 nhóm người dùng:

| Kịch bản | Luồng | Tỷ lệ |
|---|---|---|
| `browse` | Tìm bãi GPS → chi tiết bãi → đánh giá → FAQ | ~70% |
| `booking` | Tạo booking → xem → danh sách → xem trước hủy → hủy | ~17% |
| `gate` | Tạo booking → chủ bãi duyệt → QR vào cổng → báo phí → ra cổng; kiểm tra booking tự chuyển trạng thái | ~8% |
| `backoffice` | Hàng chờ tranh chấp, khoản giữ, tham số, audit log | ~6% |

| Profile | VU đồng thời | Thời lượng | Dùng khi |
|---|---|---|---|
| `smoke` | 1 mỗi kịch bản, 1 vòng | ~20 giây | Kiểm tra script và luồng chạy đúng; không đo thời gian phản hồi |
| `light` | 45 | ~4,5 phút | Tải nhẹ hằng ngày |
| `peak` | 100 | ~4,5 phút | Cao điểm giả lập trên 1 máy |

**Chỉ tiêu** (Kiến Trúc v3 §4.1, Kế Hoạch Kiểm Thử v3 §3.2): p95 < 500 ms, p99 < 1000 ms, HTTP lỗi < 1%, check đạt > 99%, không có lỗi nghiệp vụ. Không đạt thì k6 trả mã thoát khác 0.

## Chạy

Yêu cầu: PostgreSQL theo `.env`, .NET 10, PowerShell 7, và k6 hoặc Docker (script tự dùng image `grafana/k6` nếu máy không có k6).

Tắt hệ thống dev trước, rồi chạy trực tiếp trong terminal, **không nối `| ...`** (tiến trình nền giữ pipe nên lệnh không trả về):

```powershell
./Tests/load/scripts/start-load-stack.ps1             # build Release, chạy nền trên DB riêng pm_load_*
./Tests/load/scripts/run-load.ps1 -Profile smoke      # luôn chạy smoke trước
./Tests/load/scripts/run-load.ps1 -Profile light
node Tests/load/scripts/summarize-results.mjs Tests/load/results/<file>.json   # ra bảng cho báo cáo
./Tests/load/scripts/start-load-stack.ps1 -Stop
```

Lưu ý:

- Script tạo và hủy booking thật, nên từ chối chạy vào máy khác nếu không đặt `ALLOW_REMOTE=true`.
- Mỗi lần so sánh nên bắt đầu từ DB sạch: xóa các database `pm_load_*` bằng pgAdmin hoặc `psql` khi hệ thống đã dừng. Dữ liệu tích lũy làm số liệu xấu đi (xem `BUG-LOAD-01`, `BUG-LOAD-03`).
- Sự kiện không được gửi đi (`EventBus__Enabled=false`), nên đợt tải chưa đo RabbitMQ và các consumer.

## Báo cáo

Báo cáo từng đợt nằm trong `reports/`. Số liệu thô trong `results/` không commit.
