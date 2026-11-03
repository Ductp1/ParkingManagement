import { expect, test } from '../../lib/api';
import { ACCOUNTS, LOT_VINCOM } from '../../lib/demo';
import { completedBooking, createBooking, type BookingInfo } from '../../lib/flows';

// Luồng nghiệm thu 1 – Tài xế: tìm bãi → đặt chỗ → qua cổng → đánh giá / khiếu nại / trợ giúp.
test.describe.serial('Luồng 1 – Tài xế', () => {
  let completed: BookingInfo;

  test('tìm bãi gần vị trí GPS, sắp theo khoảng cách (US-014)', async ({ api }) => {
    const res = await api.anonymous.get(
      `/api/v1/parking-lots/search?lat=${LOT_VINCOM.lat}&lng=${LOT_VINCOM.lng}&radiusKm=5`);
    expect(res.status).toBe(200);
    const lot = res.body.find((l: any) => l.id === LOT_VINCOM.id);
    expect(lot, 'bãi Vincom phải nằm trong kết quả').toBeTruthy();
    expect(lot.distanceKm).toBeLessThanOrEqual(5);
    const distances = res.body.map((l: any) => l.distanceKm);
    expect(distances).toEqual([...distances].sort((a: number, b: number) => a - b));
  });

  test('từ chối tọa độ / bán kính không hợp lệ (US-014)', async ({ api }) => {
    expect((await api.anonymous.get('/api/v1/parking-lots/search?lat=91&lng=106.7')).status).toBe(400);
    expect((await api.anonymous.get(
      `/api/v1/parking-lots/search?lat=${LOT_VINCOM.lat}&lng=${LOT_VINCOM.lng}&radiusKm=51`)).status).toBe(400);
  });

  test('xem chi tiết bãi (US-018)', async ({ api }) => {
    const res = await api.anonymous.get(`/api/v1/parking-lots/${LOT_VINCOM.id}`);
    expect(res.status).toBe(200);
    expect(res.body.name).toBe(LOT_VINCOM.name);
    expect(res.body.availableSlots).toBeGreaterThanOrEqual(0);
  });

  test('đặt chỗ: giữ chỗ 15 phút và cấp mã QR (US-022)', async ({ api }) => {
    const driver = await api.as('driver');
    const before = Date.now();
    const res = await createBooking(driver);
    expect(res.status, JSON.stringify(res.body)).toBe(201);
    expect(res.body.code).toMatch(/^BK-/);
    expect(res.body.status).toBe('PendingPayment');
    expect(res.body.qrToken).toBeTruthy();
    const holdMinutes = (Date.parse(res.body.holdExpiresAtUtc) - before) / 60_000;
    expect(holdMinutes).toBeGreaterThan(14);
    expect(holdMinutes).toBeLessThanOrEqual(15.5);

    const mine = await driver.get(`/api/v1/bookings?userId=${ACCOUNTS.driver.userId}`);
    expect(mine.status).toBe(200);
    expect(mine.body.map((b: any) => b.code)).toContain(res.body.code);
  });

  test('chặn đặt chỗ sát giờ, phải trước ít nhất 15 phút (US-026)', async ({ api }) => {
    const res = await createBooking(await api.as('driver'), { startInMinutes: 5 });
    expect(res.status).toBe(400);
  });

  test('booking được xác nhận, xe vào/ra cổng và hoàn tất', async ({ api }) => {
    completed = await completedBooking(await api.as('driver'), await api.as('owner'), await api.as('staff'));
    const detail = await (await api.as('driver')).get(`/api/v1/bookings/${completed.code}`);
    expect(detail.body.status).toBe('Completed');
  });

  test('đánh giá bãi sau khi hoàn tất, mỗi booking 1 lần (US-093)', async ({ api }) => {
    const driver = await api.as('driver');
    const eligibility = await driver.get(`/api/v1/reviews/bookings/${completed.code}/eligibility`);
    expect(eligibility.status).toBe(200);
    expect(eligibility.body.canReview).toBe(true);

    const comment = `E2E ${completed.code}: bãi rộng, dễ tìm chỗ.`;
    const created = await driver.post('/api/v1/reviews', {
      bookingCode: completed.code, rating: 4, comment, reviewerName: 'Nguyễn Văn An',
    });
    expect(created.status, JSON.stringify(created.body)).toBe(201);
    expect(created.body.parkingLotId).toBe(LOT_VINCOM.id);

    const again = await driver.post('/api/v1/reviews', { bookingCode: completed.code, rating: 5 });
    expect(again.status).toBe(409);

    const lotReviews = await api.anonymous.get(`/api/v1/reviews?parkingLotId=${LOT_VINCOM.id}`);
    expect(lotReviews.body.latest.map((r: any) => r.comment)).toContain(comment);
  });

  test('không cho đánh giá khi chưa đăng nhập (US-093)', async ({ api }) => {
    const res = await api.anonymous.post('/api/v1/reviews', { bookingCode: completed.code, rating: 5 });
    expect(res.status).toBe(401);
  });

  test('gửi khiếu nại cho booking, không gửi trùng khi ticket chưa xong (US-088)', async ({ api }) => {
    const driver = await api.as('driver');
    const created = await driver.post('/api/v1/complaints', {
      userId: ACCOUNTS.driver.userId,
      bookingCode: completed.code,
      category: 'Overcharge',
      description: 'Tôi ra trước giờ kết thúc nhưng vẫn bị tính đủ block cuối.',
    });
    expect(created.status, JSON.stringify(created.body)).toBe(201);
    expect(created.body.code).toMatch(/^CP-/);

    const duplicate = await driver.post('/api/v1/complaints', {
      userId: ACCOUNTS.driver.userId, bookingCode: completed.code, category: 'Other',
      description: 'Gửi lại khiếu nại cho cùng booking.',
    });
    expect(duplicate.status).toBe(409);

    const mine = await driver.get(`/api/v1/complaints/my?userId=${ACCOUNTS.driver.userId}`);
    expect(mine.body.map((c: any) => c.code)).toContain(created.body.code);
  });

  test('xem FAQ trợ giúp theo nhóm chủ đề (US-092)', async ({ api }) => {
    const res = await api.anonymous.get('/api/v1/faqs?audience=Driver');
    expect(res.status).toBe(200);
    expect(res.body.categories.length).toBeGreaterThan(0);
    expect(res.body.categories[0].items[0].question).toBeTruthy();
  });
});
