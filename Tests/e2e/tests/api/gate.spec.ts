import { expect, test } from '../../lib/api';
import { ACCOUNTS, LOT_TSN_ID, LOT_VINCOM } from '../../lib/demo';
import {
  bookingStatus, confirmedBooking, createBooking, gateCheckInByQr, gateCheckOut, KNOWN_BUGS, knownBug, randomPlate,
  type BookingInfo,
} from '../../lib/flows';

// Luồng nghiệm thu 2 – Cổng: nhân viên quét QR / nhập biển số, chốt phí và mở barie.
test.describe.serial('Luồng 2 – Cổng', () => {
  let booking: BookingInfo;

  test.beforeAll(async ({ api }) => {
    booking = await confirmedBooking(await api.as('driver'), await api.as('owner'));
  });

  test('QR hợp lệ → tạo lượt gửi xe (US-043, T-704)', async ({ api }) => {
    const staff = await api.as('staff');
    const res = await gateCheckInByQr(staff, booking);
    if (res.status === 503) knownBug(KNOWN_BUGS.gateBookingSync);
    else expect(res.status, JSON.stringify(res.body)).toBe(200);

    // Lượt gửi xe phải tồn tại dù đồng bộ booking lỗi.
    const quote = await staff.get(`/api/v1/gate/exit-quote?parkingLotId=${LOT_VINCOM.id}&plate=${booking.plate}`);
    expect(quote.status).toBe(200);
    expect(quote.body.bookingCode).toBe(booking.code);
  });

  test('QR đã dùng không mở cổng lần hai (T-704)', async ({ api }) => {
    const res = await gateCheckInByQr(await api.as('staff'), booking);
    expect(res.status).toBe(409);
  });

  test('QR giả hoặc của bãi khác bị từ chối (T-704)', async ({ api }) => {
    const staff = await api.as('staff');
    expect((await gateCheckInByQr(staff, { plate: randomPlate(), qrToken: 'forged.e2e.token' })).status).toBe(400);
    expect((await gateCheckInByQr(staff, booking, LOT_TSN_ID)).status).toBe(400);
  });

  test('booking chưa xác nhận không được vào cổng (UC-21)', async ({ api }) => {
    const pending = await createBooking(await api.as('driver'));
    expect(pending.status).toBe(201);
    const res = await (await api.as('staff')).post('/api/v1/gate/check-in', {
      parkingLotId: LOT_VINCOM.id, method: 'BookingCode', plate: pending.body.plateNumber,
      bookingCode: pending.body.code, staffUserId: ACCOUNTS.staff.userId,
    });
    expect(res.status).toBe(409);
  });

  test('chốt phí trong ân hạn 15 phút rồi mở barie ra (US-045, US-046)', async ({ api }) => {
    const staff = await api.as('staff');
    const quote = await staff.get(`/api/v1/gate/exit-quote?parkingLotId=${LOT_VINCOM.id}&plate=${booking.plate}`);
    expect(quote.status).toBe(200);
    expect(quote.body.isWithinBillingGrace).toBe(true);
    expect(quote.body.additionalDue).toBe(0);

    const out = await gateCheckOut(staff, booking.plate);
    expect(out.status, JSON.stringify(out.body)).toBe(200);
    expect(out.body.session.status).toBe('Completed');
    expect(out.body.session.exitAtUtc).toBeTruthy();

    const after = await staff.get(`/api/v1/gate/exit-quote?parkingLotId=${LOT_VINCOM.id}&plate=${booking.plate}`);
    expect(after.status).toBe(404);
  });

  test('khách vãng lai: nhập biển số tay để vào/ra (T-702)', async ({ api }) => {
    const staff = await api.as('staff');
    const plate = randomPlate();
    const checkIn = () => staff.post('/api/v1/gate/check-in', {
      parkingLotId: LOT_VINCOM.id, method: 'Manual', plate, staffUserId: ACCOUNTS.staff.userId,
    });
    const first = await checkIn();
    expect(first.status, JSON.stringify(first.body)).toBe(200);
    expect(first.body.isWalkIn).toBe(true);
    expect((await checkIn()).status).toBe(409);       // 1 lượt đang mở / biển số / bãi

    const out = await gateCheckOut(staff, plate);
    expect(out.status).toBe(200);
    expect(out.body.session.status).toBe('Completed');
  });

  test('BUG-E2E-01: booking phải tự sang CheckedIn/Completed khi xe qua cổng', async ({ api }) => {
    test.fail(true, KNOWN_BUGS.gateBookingSync);
    const driver = await api.as('driver');
    const staff = await api.as('staff');
    const fresh = await confirmedBooking(driver, await api.as('owner'));

    const checkIn = await gateCheckInByQr(staff, fresh);
    expect(checkIn.status, JSON.stringify(checkIn.body)).toBe(200);
    expect(await bookingStatus(driver, fresh.code)).toBe('CheckedIn');

    const out = await gateCheckOut(staff, fresh.plate);
    expect(out.body.warning).toBeNull();
    expect(await bookingStatus(driver, fresh.code)).toBe('Completed');
  });
});
