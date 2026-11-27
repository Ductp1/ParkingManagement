# React + TypeScript + Vite

## TV1 — UI Sprint 2

- Hồ sơ có **Liên kết VNeID giả lập**: mở hộp thoại xác nhận, hiển thị trạng thái demo và cho hủy liên kết. Chỉ dùng sessionStorage theo UserId trong phiên trình duyệt; không gọi API VNeID, không nhận CCCD/mật khẩu/OTP, không thay đổi KYC, vai trò hay quyền truy cập. Trạng thái giả lập không phải bằng chứng xác thực danh tính.

- `/forgot-password`: nhận OTP, đếm ngược gửi lại 60 giây/thời hạn tối đa 5 phút và đặt lại mật khẩu. Liên kết từ trang đăng nhập và hồ sơ. Sau khi đổi thành công, xóa phiên local để đăng nhập lại.
- `/account/profile`: người dùng đã đăng nhập xem/sửa tên, chọn ảnh từ máy, xem trước/hoàn tác/xóa ảnh; cập nhật tên/ảnh trên thanh điều hướng sau khi lưu. Nhận JPG/PNG/WebP tối đa 2 MB, 4096 × 4096 pixel, chuẩn hóa PNG tối đa 512px. Email/SĐT chỉ đọc.
- Đã gỡ phần Owner quản lý Staff theo yêu cầu ngày 27/10/2026.
- Trang xác thực tài khoản hiện có hỗ trợ email/SĐT: `/register?action=verify` khi chưa đăng nhập.

Các trang dùng API qua Gateway theo `src/api/client.ts` và phong cách AuthLayout/Owner Portal hiện có. Cần khởi động lại UserService với API avatar mới và Gateway để sử dụng các màn hình hồ sơ/xác thực. Ảnh lưu tại Security:AvatarPath (mặc định App_Data/avatars); cần giữ thư mục này khi deploy. Các API đăng nhập/hồ sơ dùng dữ liệu thật; riêng VNeID là demo UI đã ghi nhãn, không phải KYC.

Kiểm tra: `npm run build`, `npm run lint`. Giao diện OTP và hồ sơ từng được kiểm tra desktop/mobile 390px bằng API giả lập riêng; backend upload avatar đã kiểm thử HTTP trên PostgreSQL. UI Staff đã gỡ nên các kết quả kiểm tra Staff cũ không áp dụng cho phạm vi hiện tại. Chưa kiểm tra trực quan upload avatar/VNeID demo trong trình duyệt hoặc gửi email thật. Build còn cảnh báo bundle lớn; lint còn cảnh báo có sẵn ở phần phương tiện/lịch sử đặt chỗ.

This template provides a minimal setup to get React working in Vite with HMR and some Oxlint rules.

Currently, two official plugins are available:

- [@vitejs/plugin-react](https://github.com/vitejs/vite-plugin-react/blob/main/packages/plugin-react) uses [Oxc](https://oxc.rs)
- [@vitejs/plugin-react-swc](https://github.com/vitejs/vite-plugin-react/blob/main/packages/plugin-react-swc) uses [SWC](https://swc.rs/)

## React Compiler

The React Compiler is not enabled on this template because of its impact on dev & build performances. To add it, see [this documentation](https://react.dev/learn/react-compiler/installation).

## Expanding the Oxlint configuration

If you are developing a production application, we recommend enabling type-aware lint rules by installing `oxlint-tsgolint` and editing `.oxlintrc.json`:

```json
{
  "$schema": "./node_modules/oxlint/configuration_schema.json",
  "plugins": ["react", "typescript", "oxc"],
  "options": {
    "typeAware": true
  },
  "rules": {
    "react/rules-of-hooks": "error",
    "react/only-export-components": ["warn", { "allowConstantExport": true }]
  }
}
```

See the [Oxlint rules documentation](https://oxc.rs/docs/guide/usage/linter/rules) for the full list of rules and categories.
