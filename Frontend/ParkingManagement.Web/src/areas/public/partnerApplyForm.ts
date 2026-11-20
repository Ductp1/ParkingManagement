/**
 * Tiện ích dùng chung cho các trang công khai mở bằng link trong email (kích hoạt tài khoản chủ bãi, bổ sung hồ sơ đối tác).
 * Form đăng ký / bổ sung hồ sơ nằm ở partner-register/ (wizard Figma 221:139…221:161).
 */

/**
 * Đọc tham số sau "#" của link trong email / tin nhắn (token không nằm trong query string nên không lên server, log hay Referer),
 * rồi xóa khỏi thanh địa chỉ để token không ở lại lịch sử trình duyệt.
 */
export const takeHashParams = (): URLSearchParams => {
  const params = new URLSearchParams(window.location.hash.replace(/^#/, ''));
  if (window.location.hash) window.history.replaceState(window.history.state, '', window.location.pathname + window.location.search);
  return params;
};

/** Điều kiện mật khẩu khi kích hoạt tài khoản chủ bãi – cùng quy tắc với đăng ký và UserService (IdentityRules.Password). */
export const passwordProblems = (password: string) => [
  { label: 'Ít nhất 8 ký tự', met: password.length >= 8 && new TextEncoder().encode(password).length <= 72 },
  { label: 'Có chữ cái', met: /\p{L}/u.test(password) },
  { label: 'Có chữ số', met: /\p{Nd}/u.test(password) },
];
