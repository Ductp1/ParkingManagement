import { describe, expect, it, vi } from 'vitest';
import { createSessionExpiryHandler, loginUrl, safeReturnUrl } from './sessionExpiry';

describe('safeReturnUrl (chống open redirect)', () => {
  it.each([
    ['/admin', '/admin'],
    ['/admin/access/holidays?year=2026', '/admin/access/holidays?year=2026'],
    ['/admin/operations/users/12#data', '/admin/operations/users/12#data'],
    ['/admin?tab=1', '/admin?tab=1'],
  ])('giữ đường dẫn admin nội bộ %s', (raw, expected) => {
    expect(safeReturnUrl(raw)).toBe(expected);
  });

  it.each([
    [null],
    [undefined],
    [''],
    ['https://evil.example/admin'],
    ['//evil.example/admin'],
    ['/\\evil.example'],
    ['/admin\\..\\..\\evil'],
    ['/administrator'],
    ['/admin.evil.example'],
    ['/admin/../driver'],
    ['/driver/find-lot'],
    ['/login'],
    ['javascript:alert(1)'],
    ['/admin/\u0000x'],
  ])('từ chối %s → /admin', (raw) => {
    expect(safeReturnUrl(raw as string | null | undefined)).toBe('/admin');
  });
});

describe('loginUrl', () => {
  it('mã hóa returnUrl đã kiểm tra', () => {
    expect(loginUrl('/admin/access/holidays?year=2026')).toBe('/login?returnUrl=%2Fadmin%2Faccess%2Fholidays%3Fyear%3D2026');
    expect(loginUrl('https://evil.example')).toBe('/login?returnUrl=%2Fadmin');
  });
});

describe('createSessionExpiryHandler', () => {
  it('gọi onExpire một lần với returnUrl an toàn dù nhiều request 401 cùng lúc', () => {
    const onExpire = vi.fn();
    const handle = createSessionExpiryHandler({ getPath: () => '/admin/finance/revenue?period=month', onExpire });
    handle();
    handle();
    handle();
    expect(onExpire).toHaveBeenCalledTimes(1);
    expect(onExpire).toHaveBeenCalledWith('/admin/finance/revenue?period=month');
  });

  it.each(['/login', '/login?returnUrl=%2Fadmin', '/login#x'])('đang ở %s thì không chuyển trang lặp', (path) => {
    const onExpire = vi.fn();
    createSessionExpiryHandler({ getPath: () => path, onExpire })();
    expect(onExpire).not.toHaveBeenCalled();
  });

  it('đường dẫn ngoài admin → returnUrl mặc định /admin', () => {
    const onExpire = vi.fn();
    createSessionExpiryHandler({ getPath: () => '/driver', onExpire })();
    expect(onExpire).toHaveBeenCalledWith('/admin');
  });
});
