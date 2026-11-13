# Kịch bản UAT (T-904)

Bộ ca nghiệm thu cho PO / BA, đi theo 3 luồng của T-903 (Tài xế, Cổng, Chủ bãi–Admin) cộng thêm nhóm bảo mật.

| File | Nội dung |
|---|---|
| `uat-cases.csv` | Danh sách ca và sổ ghi kết quả. Mở bằng Excel; điền cột **Kết quả** (Đạt / Không đạt / Bị chặn), **Người kiểm thử**, **Ngày**, **Mã bug** |
| `preflight.mjs` | Tự chạy các ca có thể gọi qua API, để biết trước ca nào sẽ fail trước khi mời PO / BA |

## Điều kiện đạt UAT (Kế Hoạch Kiểm Thử v3 §3.2)

1. 100% ca **P0** và **P1** đạt.
2. Không còn lỗi P0 / P1 đang mở trong `Tests/bugs/bug-register.csv`.
3. API p95 < 500 ms dưới tải 1.000 người dùng đồng thời; không còn lỗ hổng bảo mật High / Critical.
4. PO và BA ký biên bản bên dưới.

## Mức ưu tiên

| Mức | Ý nghĩa |
|---|---|
| P0 | Luồng chính, tiền, bảo mật. Fail là không release |
| P1 | Chức năng quan trọng. Fail là không release |
| P2 | Fail vẫn release được nếu PO đồng ý và có kế hoạch sửa |

## Cột "Sẵn sàng"

| Giá trị | Ý nghĩa |
|---|---|
| API đạt | `preflight.mjs` hoặc k6 đã chạy đúng trên API. **Chưa có nghĩa là ca UAT đạt:** PO / BA vẫn phải tự kiểm tra |
| Integration test đạt | Có integration test tương ứng đang xanh |
| Bị chặn: `<mã bug>` | Đã biết sẽ fail hoặc chưa thao tác được, chờ sửa bug đó |
| Chưa kiểm tra | Chưa có bằng chứng, chạy thử lần đầu trong buổi UAT |

## Quy trình một buổi UAT

1. Chuẩn bị DB sạch: chạy `run-all.ps1` trên DB mới để có dữ liệu demo. Mật khẩu chung và danh sách tài khoản nằm trong `database/README.md`.
2. Chạy kiểm tra trước. **Script tạo dữ liệu thật**, chỉ chạy trên môi trường kiểm thử, và nên chạy trước khi nạp lại DB cho buổi UAT:
   ```bash
   node Tests/uat/preflight.mjs
   ```
3. Thực hiện các ca theo thứ tự P0 → P1 → P2 và điền kết quả vào `uat-cases.csv`.
4. Ca nào không đạt thì thêm dòng vào `Tests/bugs/bug-register.csv` và ghi mã bug vào ca đó.
5. Sửa xong thì chạy lại đúng ca đó và các ca cùng luồng.
6. Khi đủ điều kiện, PO và BA ký biên bản.

## Biên bản nghiệm thu

| Mục | Nội dung |
|---|---|
| Phiên bản / commit | |
| Ngày UAT | |
| Số ca P0 đạt / tổng | |
| Số ca P1 đạt / tổng | |
| Lỗi P0 / P1 còn mở | |
| Kết quả tải (p95 / số người dùng) | |
| Kết luận | Đạt / Đạt có điều kiện / Không đạt |
| Điều kiện kèm theo | |

| Vai trò | Họ tên | Chữ ký | Ngày |
|---|---|---|---|
| Product Owner | | | |
| Business Analyst | | | |
| QA Lead | | | |
