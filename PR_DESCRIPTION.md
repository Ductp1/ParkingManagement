# 📌 Tiêu đề PR

`feat(us-066): Cấu hình sức chứa & vùng đệm vãng lai (C/B/Q) + Admission nguyên tử tại cổng + Form admin & QA tests`

> **Branch:** `feature/us-066-capacity-config` → **`develop`** · 64 files · +5.706/−45

---

## 📝 Tổng quan (Overview)

PR này hoàn thành task **US-066**: cho phép Admin cấu hình **Sức chứa (C)**, **Vùng đệm vãng lai (B)** và **Hạn mức khách đăng ký (Q)** theo từng bãi; hệ thống tự chặn xe vãng lai khi hết buffer, chặn bãi đầy, chặn đăng ký thuê bao khi hết hạn mức — tất cả đều **chống race condition** bằng thao tác nguyên tử có điều kiện tại cổng vào/ra (GateService).

Kèm theo đó: luồng **đăng ký thuê bao mới** (BookingService) có kiểm tra `A < Q`, phát sự kiện **`CapacityChanged` real-time** qua SignalR hub của US-065 tới mọi client đang mở bãi, audit log mọi thay đổi cấu hình, và **form admin tiếng Việt dễ hiểu** (gợi ý xám, lỗi theo từng ô sau blur, khối lỗi tổng hợp, xử lý 409, real-time không cần F5).

PR cũng bổ sung **bộ unit test QA** cho T-601 (SignalR hub), T-602 (dispatcher retry/multi-channel), US-065 (group routing slot) — gồm test mô phỏng **8 request đồng thời vào chỗ cuối cùng → đúng 1 thành công** trên PostgreSQL thật.

---

## 🎯 Tiêu chí hoàn thành (Acceptance Criteria)

### US-066: Cấu hình sức chứa & vùng đệm vãng lai
- [x] **AC1 (Cấu hình hợp lệ):** `C > 0`, `B ≥ 0`, `Q ≥ 0`, `B ≤ C`, `Q ≤ C`, `B + Q ≤ C` — lỗi trả 400 theo **từng field** kèm mã lỗi ổn định + thông điệp tiếng Việt.
- [x] **AC2 (Admission):** Vãng lai bị từ chối khi `V = B` dù `T < C`; thuê bao/đặt trước vẫn vào được; bãi đầy (`T = C`) từ chối cả hai.
- [x] **AC3 (Hạn mức thuê bao):** Đăng ký vé tháng bị chặn khi `A ≥ Q` (400 `SUBSCRIBER_QUOTA_FULL`), cho phép khi `A = Q − 1`.
- [x] **AC4 (Hạ cấu hình – luật 7):** `C_new < T` → từ chối kèm số xe đang đỗ; `B_new < V` / `Q_new < A` → lưu kèm `warnings[]`, khách mới tự bị chặn bởi điều kiện nguyên tử tới khi counters tụt xuống, không đuổi xe / không hủy thuê bao.
- [x] **AC5 (Hiển thị):** `availableForTransient = max(0, min(C−T, B−V))`, `availableForSubscriber = max(0, C−T)` trả kèm GET.
- [x] **AC6 (Xe ra trừ đúng loại):** Loại xe (Transient/Subscriber) ghi lúc vào, trừ đúng bộ đếm lúc ra, có guard không âm.
- [x] **AC7 (Chống race):** Conditional UPDATE (`rowsAffected = 0` → từ chối) + advisory lock đăng ký; test đồng thời PostgreSQL thật: 8 request khi còn 1 chỗ → đúng 1 thành công.
- [x] **AC8 (Audit & Real-time):** Audit log ai/sửa/lúc nào/old→new C,B,Q/version/warnings; publish `CapacityChanged` sau lưu cấu hình và mỗi vào/ra/đăng ký — lỗi publish không làm hỏng nghiệp vụ.
- [x] **AC9 (Phân quyền):** `PUT` chỉ Admin (403 với role khác); `GET` mọi role đã đăng nhập.

### T-601 / T-602 / US-065 (bổ sung QA tests)
- [x] Hub join/leave group đúng (`user-{id}`, `lot-{id}`); event chỉ tới group của bãi mình; broadcast `SlotStateChanged` / `CapacityChanged` đúng payload.
- [x] Dispatcher: đủ 4 kênh, kênh tắt bỏ qua, dedupe, 1 kênh lỗi không ảnh hưởng kênh khác, retry **đúng 3 lần tổng**, backoff 5s → 7.5s, CancellationToken hủy được, validate payload.