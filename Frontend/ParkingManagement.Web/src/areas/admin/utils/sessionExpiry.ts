/**
 * Hết phiên (401) trong Admin Portal: xóa phiên rồi về /login?returnUrl=…
 * returnUrl chỉ nhận đường dẫn nội bộ của admin ("/admin", "/admin/…", "/admin?…", "/admin#…") để chống open redirect.
 */
export const ADMIN_FALLBACK_PATH = '/admin';

const ADMIN_PATH = /^\/admin(?:[/?#]|$)/;

export const safeReturnUrl = (raw: string | null | undefined): string => {
  if (!raw || !ADMIN_PATH.test(raw)) return ADMIN_FALLBACK_PATH;
  // Chặn "\" (trình duyệt coi như "/"), ký tự điều khiển và mọi thứ resolve ra origin khác.
  // eslint-disable-next-line no-control-regex
  if (raw.includes('\\') || /[\u0000-\u001f]/.test(raw)) return ADMIN_FALLBACK_PATH;
  try {
    const base = 'http://admin.invalid';
    const url = new URL(raw, base);
    if (url.origin !== base || !ADMIN_PATH.test(url.pathname + url.search + url.hash)) return ADMIN_FALLBACK_PATH;
    return url.pathname + url.search + url.hash;
  } catch {
    return ADMIN_FALLBACK_PATH;
  }
};

export const loginUrl = (returnUrl: string | null | undefined): string =>
  `/login?returnUrl=${encodeURIComponent(safeReturnUrl(returnUrl))}`;

const isLoginPath = (path: string): boolean => path === '/login' || path.startsWith('/login?') || path.startsWith('/login/') || path.startsWith('/login#');

interface SessionExpiryOptions {
  /** Đường dẫn hiện tại (pathname + search + hash). */
  getPath: () => string;
  /** Xóa phiên và điều hướng; nhận returnUrl đã kiểm tra. */
  onExpire: (returnUrl: string) => void;
}

/**
 * Trả về hàm gọi khi gặp 401: chỉ chạy một lần cho mỗi phiên layout (nhiều request 401 cùng lúc không điều hướng lặp)
 * và không làm gì khi đang ở /login.
 */
export const createSessionExpiryHandler = ({ getPath, onExpire }: SessionExpiryOptions): (() => void) => {
  let fired = false;
  return () => {
    const path = getPath();
    if (fired || isLoginPath(path)) return;
    fired = true;
    onExpire(safeReturnUrl(path));
  };
};
