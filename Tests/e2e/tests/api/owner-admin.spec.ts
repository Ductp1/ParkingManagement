import { expect, test, type Api } from '../../lib/api';
import { DISPUTE_AWAITING_OWNER, LOT_VINCOM } from '../../lib/demo';
import { completedBooking } from '../../lib/flows';

/** Tài xế hoàn tất 1 lượt gửi xe rồi đánh giá → trả về id đánh giá (đầu vào cho luồng kiểm duyệt US-094). */
async function newReview(api: { as(role: 'driver' | 'owner' | 'staff'): Promise<Api> }, rating: number, comment: string) {
  const driver = await api.as('driver');
  const booking = await completedBooking(driver, await api.as('owner'), await api.as('staff'));
  const res = await driver.post('/api/v1/reviews', { bookingCode: booking.code, rating, comment });
  expect(res.status, JSON.stringify(res.body)).toBe(201);
  return res.body.id as number;
}

// Luồng nghiệm thu 3 – Chủ bãi & Admin: phân quyền, tranh chấp, kiểm duyệt đánh giá, audit.
test.describe.serial('Luồng 3 – Chủ bãi & Admin', () => {
  test('API cần đăng nhập trả 401, sai vai trò trả 403 (US-102)', async ({ api }) => {
    expect((await api.anonymous.get('/api/v1/reviews/reported')).status).toBe(401);
    expect((await api.anonymous.post('/api/v1/reviews/1/owner-reply', { reply: 'Cảm ơn bạn đã góp ý.' })).status).toBe(401);
    const driver = await api.as('driver');
    expect((await driver.get('/api/v1/reviews/reported')).status).toBe(403);
    expect((await driver.get('/api/v1/admin/audit-logs')).status).toBe(403);
    expect((await driver.get(`/api/v1/complaints/${DISPUTE_AWAITING_OWNER}/owner-view`)).status).toBe(403);
  });

  test('chủ bãi chỉ xem được tranh chấp của bãi mình (US-089)', async ({ api }) => {
    const view = await (await api.as('owner')).get(`/api/v1/complaints/${DISPUTE_AWAITING_OWNER}/owner-view`);
    expect(view.status).toBe(200);
    expect(view.body.status).toBe('AwaitingOwner');
    expect(Date.parse(view.body.ownerResponseDueAtUtc)).toBeGreaterThan(Date.now());

    const otherOwner = await (await api.as('ownerTsn')).get(`/api/v1/complaints/${DISPUTE_AWAITING_OWNER}/owner-view`);
    expect(otherOwner.status).toBe(404);
  });

  test('chủ bãi phản hồi tranh chấp kèm bằng chứng, chỉ 1 lần (US-089)', async ({ api }) => {
    const owner = await api.as('owner');
    const url = `/api/v1/complaints/${DISPUTE_AWAITING_OWNER}/owner-response`;
    const res = await owner.post(url, {
      response: 'Log cổng cho thấy xe ra lúc 11:52, đúng giờ tính phí. Đính kèm log check-out.',
      evidenceUrls: ['https://cdn.example.com/e2e/gate-log.pdf'],
    });
    expect(res.status, JSON.stringify(res.body)).toBe(200);
    expect(res.body.status).toBe('OwnerResponded');

    expect((await owner.post(url, { response: 'Phản hồi bổ sung lần hai.' })).status).toBe(409);
    expect((await owner.get('/api/v1/complaints/payout-holds')).status).toBe(200);
  });

  test('chủ bãi phản hồi công khai 1 lần, chủ bãi khác không thao tác được (US-094 AC1)', async ({ api }) => {
    const reviewId = await newReview(api, 2, 'E2E: bãi chật, nhân viên hướng dẫn chậm.');
    const owner = await api.as('owner');
    const reply = 'Cảm ơn anh, bãi đã bổ sung nhân viên giờ cao điểm.';

    const res = await owner.post(`/api/v1/reviews/${reviewId}/owner-reply`, { reply });
    expect(res.status, JSON.stringify(res.body)).toBe(200);
    expect(res.body.ownerReply).toBe(reply);
    expect((await owner.post(`/api/v1/reviews/${reviewId}/owner-reply`, { reply: 'Phản hồi lần hai.' })).status).toBe(409);
    expect((await (await api.as('ownerTsn')).post(`/api/v1/reviews/${reviewId}/owner-reply`, { reply })).status).toBe(404);

    const lot = await api.anonymous.get(`/api/v1/reviews?parkingLotId=${LOT_VINCOM.id}`);
    expect(lot.body.latest.find((r: any) => r.id === reviewId)?.ownerReply).toBe(reply);
  });

  test('chủ bãi khiếu nại đánh giá, chỉ Admin được gỡ (US-094 AC2)', async ({ api }) => {
    const reviewId = await newReview(api, 1, 'E2E: đánh giá cần kiểm duyệt.');
    const owner = await api.as('owner');
    const admin = await api.as('admin');

    const report = await owner.post(`/api/v1/reviews/${reviewId}/report`, {
      reason: 'Tài xế không đỗ tại bãi vào ngày được đánh giá.',
    });
    expect(report.status, JSON.stringify(report.body)).toBe(200);
    expect(report.body.isReported).toBe(true);
    expect(report.body.isHidden).toBe(false);
    expect((await owner.post(`/api/v1/reviews/${reviewId}/hide`, { reason: 'Chủ bãi tự gỡ đánh giá.' })).status).toBe(403);

    const queue = await admin.get('/api/v1/reviews/reported');
    expect(queue.status).toBe(200);
    expect(queue.body.map((r: any) => r.id)).toContain(reviewId);

    const hide = await admin.post(`/api/v1/reviews/${reviewId}/hide`, { reason: 'Nội dung sai sự thật, đã đối chiếu log cổng.' });
    expect(hide.status, JSON.stringify(hide.body)).toBe(200);
    expect(hide.body.isHidden).toBe(true);

    const lot = await api.anonymous.get(`/api/v1/reviews?parkingLotId=${LOT_VINCOM.id}`);
    expect(lot.body.latest.map((r: any) => r.id)).not.toContain(reviewId);
  });

  test('Admin bác khiếu nại → đánh giá vẫn hiển thị (US-094 AC2)', async ({ api }) => {
    const reviewId = await newReview(api, 3, 'E2E: đánh giá đúng trải nghiệm thực tế.');
    const admin = await api.as('admin');
    await (await api.as('owner')).post(`/api/v1/reviews/${reviewId}/report`, { reason: 'Chủ bãi cho rằng nội dung chưa chính xác.' });

    const dismiss = await admin.post(`/api/v1/reviews/${reviewId}/dismiss-report`, { reason: 'Đánh giá đúng với trải nghiệm thực tế.' });
    expect(dismiss.status, JSON.stringify(dismiss.body)).toBe(200);
    expect(dismiss.body.isReported).toBe(false);
    expect(dismiss.body.isHidden).toBe(false);
    expect((await admin.get('/api/v1/reviews/reported')).body.map((r: any) => r.id)).not.toContain(reviewId);
  });

  test('Admin xem audit log (US-100)', async ({ api }) => {
    const res = await (await api.as('admin')).get('/api/v1/admin/audit-logs');
    expect(res.status).toBe(200);
    expect(res.body.items.length).toBeGreaterThan(0);
    expect(res.body.items[0]).toHaveProperty('action');
  });
});
