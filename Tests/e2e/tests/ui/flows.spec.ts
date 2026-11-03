import { LOT_VINCOM } from '../../lib/demo';
import { randomPlate } from '../../lib/flows';
import { expect, signInAs, test } from '../../lib/ui';

// 3 luồng nghiệm thu trên giao diện – chỉ các màn đã nối API thật. Màn còn dữ liệu giả để test.fixme.

test.describe('Luồng 1 – Tài xế (UI)', () => {
  test('trang chủ lấy vị trí GPS và hiện bãi gần nhất từ API (US-014)', async ({ page, context }) => {
    await context.grantPermissions(['geolocation']);
    await context.setGeolocation({ latitude: LOT_VINCOM.lat, longitude: LOT_VINCOM.lng });
    await signInAs(page, 'driver');
    const search = page.waitForResponse(r => r.url().includes('/api/v1/parking-lots/search') && r.ok());
    await page.goto('/driver');
    await search;
    await expect(page.getByText(LOT_VINCOM.name).first()).toBeVisible();
  });

  test.fixme('đặt chỗ trên giao diện (LotDetailPage còn danh sách ô đỗ giả AVAILABLE_SPOTS)', async () => {});
  test.fixme('lịch sử đặt chỗ (BookingHistoryPage còn dữ liệu giả INITIAL_BOOKINGS)', async () => {});
});

test.describe('Luồng 2 – Cổng (UI)', () => {
  test('nhân viên check-in khách vãng lai, chốt phí và mở barie ra (T-702, US-045)', async ({ page }) => {
    const plate = randomPlate();
    await signInAs(page, 'staff');
    await page.goto('/gate');

    await page.getByPlaceholder('Nhập biển số hoặc quét QR (VD: 51F-123.45)...').fill(plate);
    await page.getByRole('button', { name: /MỞ BARIE VÀO/ }).click();
    await expect(page.getByText(new RegExp(`Đã check-in xe ${plate.replace('.', '\\.')}`))).toBeVisible();

    await page.getByPlaceholder('Nhập biển số hoặc quét mã vé xuất...').fill(plate);
    await page.getByRole('button', { name: /TÍNH PHÍ/ }).click();
    await expect(page.getByText('Tổng phí lượt gửi')).toBeVisible();

    await page.getByRole('button', { name: /XÁC NHẬN ĐÃ THU TIỀN & MỞ BARIE RA/ }).click();
    await expect(page.getByText(new RegExp(`Xe ${plate.replace('.', '\\.')} đã ra`))).toBeVisible();
  });
});

test.describe('Luồng 3 – Chủ bãi & Admin (UI)', () => {
  test('Admin xem danh sách FAQ lấy từ SupportService (US-092)', async ({ page }) => {
    await signInAs(page, 'admin');
    const faqs = page.waitForResponse(r => r.url().includes('/api/v1/faqs') && r.ok());
    await page.goto('/admin/communications/faq');
    await faqs;
    await expect(page.getByText('Tôi được giữ chỗ trong bao lâu?')).toBeVisible();
  });

  test.fixme('tổng quan chủ bãi (OwnerHomePage còn dữ liệu giả SEVEN_DAYS_DATA)', async () => {});
  test.fixme('Admin kiểm duyệt đánh giá / tranh chấp (support/reviews, support/disputes đang "unavailable")', async () => {});
});
