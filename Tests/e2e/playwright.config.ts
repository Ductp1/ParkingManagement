import { defineConfig, devices } from '@playwright/test';
import { runsProject } from './lib/cli';
import { GATEWAY_URL, WEB_URL } from './lib/env';

// T-903: 2 project
//   api – gọi thẳng API qua Gateway (không cần trình duyệt), phủ trọn 3 luồng nghiệm thu
//   ui  – trình duyệt Chromium trên frontend React (Vite dev server), chỉ các màn đã nối API thật
// Hệ thống backend phải chạy trước: ./Tests/e2e/scripts/start-e2e-stack.ps1
export default defineConfig({
  testDir: './tests',
  globalSetup: './global-setup.ts',
  // Các luồng dùng chung dữ liệu seed (CP-0001, bãi Vincom…) → chạy tuần tự cho kết quả ổn định.
  fullyParallel: false,
  workers: 1,
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 1 : 0,
  timeout: 60_000,
  expect: { timeout: 10_000 },
  reporter: [['list'], ['html', { open: 'never' }]],
  use: {
    baseURL: WEB_URL,
    locale: 'vi-VN',
    timezoneId: 'Asia/Ho_Chi_Minh',
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure',
  },
  projects: [
    { name: 'api', testMatch: /api\/.*\.spec\.ts/ },
    { name: 'ui', testMatch: /ui\/.*\.spec\.ts/, use: { ...devices['Desktop Chrome'] } },
  ],
  // Chỉ chạy project api → không cần bật Vite.
  webServer: !runsProject('ui')
    ? undefined
    : {
        command: 'npm run dev -- --port 5173 --strictPort',
        cwd: '../../Frontend/ParkingManagement.Web',
        url: WEB_URL,
        reuseExistingServer: !process.env.CI,
        timeout: 120_000,
        // Site key bất kỳ để widget render; script Turnstile được giả lập trong test (lib/ui.ts).
        env: { VITE_API_GATEWAY_URL: GATEWAY_URL, VITE_TURNSTILE_SITE_KEY: 'e2e-turnstile-stub' },
      },
});
