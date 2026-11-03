import { test as base, request, type Page } from '@playwright/test';
import { login } from './api';
import type { Role } from './demo';
import { GATEWAY_URL } from './env';

/**
 * Giả lập Cloudflare Turnstile: trả script đặt window.turnstile, widget gọi callback ngay với token giả.
 * Backend E2E tắt CAPTCHA (Captcha__Enabled=false, chỉ hợp lệ ở Development) nên token giả được chấp nhận.
 */
export async function stubTurnstile(page: Page) {
  await page.route('https://challenges.cloudflare.com/turnstile/**', route => route.fulfill({
    contentType: 'application/javascript',
    body: `window.turnstile = {
      render: function (el, opts) { setTimeout(function () { opts.callback('e2e-turnstile-token'); }, 0); return 'e2e'; },
      remove: function () {}
    };`,
  }));
}

/**
 * UI test kín: chặn mọi request ra ngoài localhost (Google Fonts, cdnjs, ảnh Unsplash…). index.html tải
 * stylesheet chặn hiển thị từ các CDN này – mạng chậm làm trang trắng hàng chục giây. Turnstile được giả lập.
 */
export const test = base.extend({
  page: async ({ page }, use) => {
    await page.route('**/*', route => {
      const host = new URL(route.request().url()).hostname;
      return host === 'localhost' || host === '127.0.0.1' ? route.continue() : route.abort();
    });
    await stubTurnstile(page);   // đăng ký sau nên được ưu tiên hơn route chặn ở trên
    await use(page);
  },
});

export { expect } from '@playwright/test';

/**
 * Đăng nhập nhanh: lấy token qua API rồi ghi vào sessionStorage trước khi app chạy
 * (frontend lưu phiên ở sessionStorage 'parkmaster_token' / 'parkmaster_refresh').
 * Luồng đăng nhập qua form được kiểm tra riêng trong auth.spec.ts.
 */
export async function signInAs(page: Page, role: Role) {
  const ctx = await request.newContext({ baseURL: GATEWAY_URL });
  const tokens = await login(ctx, role, true);
  await ctx.dispose();
  await page.addInitScript(([access, refresh]) => {
    sessionStorage.setItem('parkmaster_token', access);
    sessionStorage.setItem('parkmaster_refresh', refresh);
  }, [tokens.accessToken, tokens.refreshToken]);
}
