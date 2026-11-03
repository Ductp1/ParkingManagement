import { ACCOUNTS, type Role } from '../../lib/demo';
import { demoPassword } from '../../lib/env';
import { expect, signInAs, test } from '../../lib/ui';

// Cửa vào chung của 3 luồng: đăng nhập theo vai trò và chặn truy cập sai phân hệ (US-003, US-102).
test.describe('Đăng nhập & phân quyền giao diện', () => {
  const roles: Role[] = ['driver', 'staff', 'ownerTsn', 'admin'];
  for (const role of roles) {
    test(`${role} đăng nhập bằng form → vào đúng phân hệ ${ACCOUNTS[role].landing}`, async ({ page }) => {
      await page.goto('/login');
      await page.locator('#login-contact').fill(ACCOUNTS[role].email);
      await page.locator('#login-password').fill(demoPassword());
      const submit = page.locator('button[type="submit"]');
      await expect(submit).toBeEnabled();          // Turnstile (giả lập) đã trả token
      await submit.click();
      await expect(page).toHaveURL(new RegExp(`${ACCOUNTS[role].landing}(/|$)`));
    });
  }

  test('sai mật khẩu → báo lỗi, vẫn ở trang đăng nhập', async ({ page }) => {
    await page.goto('/login');
    await page.locator('#login-contact').fill(ACCOUNTS.driver.email);
    await page.locator('#login-password').fill('SaiMatKhau@123');
    await page.locator('button[type="submit"]').click();
    await expect(page.getByRole('alert')).toBeVisible();
    await expect(page).toHaveURL(/\/login/);
  });

  test('chưa đăng nhập vào trang tài xế → chuyển về /login', async ({ page }) => {
    await page.goto('/driver/history');
    await expect(page).toHaveURL(/\/login/);
  });

  test('tài xế vào phân hệ Admin → màn hình 403', async ({ page }) => {
    await signInAs(page, 'driver');
    await page.goto('/admin');
    await expect(page.getByText('Không có quyền truy cập (403 Forbidden)')).toBeVisible();
  });
});
