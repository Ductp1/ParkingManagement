# Tổng hợp bug hằng tuần (T-904)

`bug-register.csv` là sổ bug của nhóm. Script đọc sổ này và sinh báo cáo tuần trong `reports/`.

## Mỗi tuần (QA Lead, chiều thứ Sáu)

1. Cập nhật `bug-register.csv`: thêm lỗi mới, đổi trạng thái lỗi đã sửa.
2. Sinh báo cáo:
   ```bash
   node Tests/bugs/weekly-report.mjs
   ```
3. Gửi `reports/<năm>-W<tuần>.md` vào nhóm, nhắc người xử lý các lỗi P0 / P1.

Chạy lại cho tuần cũ: `node Tests/bugs/weekly-report.mjs --date 2026-11-07`. Báo cáo tuần cũ giữ nguyên số liệu của tuần đó dù lỗi được sửa về sau. Nếu sổ nhập sai (mức độ, trạng thái, ngày, trùng mã), script báo từng dòng và không sinh báo cáo.

## Cột trong sổ bug

| Cột | Giá trị |
|---|---|
| `id` | Mã không trùng, ví dụ `BUG-BOOK-001` |
| `title` | Mô tả ngắn hiện tượng |
| `severity` | `P0`, `P1`, `P2`, `P3` |
| `status` | `Open`, `InProgress`, `Fixed`, `Closed`, `WontFix` |
| `owner` | Người xử lý, ví dụ `TV3` |
| `service` | Nơi có lỗi |
| `found_on`, `fixed_on` | `YYYY-MM-DD`. `Fixed` / `Closed` bắt buộc có `fixed_on` |
| `source` | Phát hiện từ đâu: k6, E2E, UAT, review code… |
| `evidence` | Cách tái hiện, số liệu, file + dòng |

## Mức độ

| Mức | Khi nào | Release? |
|---|---|---|
| P0 | Sai tiền, lỗ hổng bảo mật, hoặc luồng chính không chạy | Chặn |
| P1 | Chức năng quan trọng sai hoặc thiếu, có thể đi đường vòng | Chặn |
| P2 | Sai một phần, ảnh hưởng ít | Không chặn, cần kế hoạch sửa |
| P3 | Hiển thị, văn bản, rủi ro nhỏ | Không chặn |

Mức độ trong sổ hiện tại do QA Lead đề xuất; người xử lý và PO có thể điều chỉnh.

## Kiểm thử script

```bash
node --test Tests/bugs/weekly-report.test.mjs
```
