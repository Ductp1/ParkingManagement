import { expect, test } from '@playwright/test';
import type { Api } from './api';
import { ACCOUNTS, DRIVER_CAR_ID, LOT_VINCOM } from './demo';

/**
 * Lỗi đã biết phát hiện khi chạy E2E – test vẫn đi tiếp bằng đường vòng và gắn annotation "known-bug"
 * vào báo cáo. Test riêng trong gate.spec.ts dùng test.fail() để báo ngay khi lỗi được sửa.
 */
export const KNOWN_BUGS = {
  gateBookingSync:
    'BUG-E2E-01 (TV7/TV3): GateService gửi {"gateDeviceId": null} khi đồng bộ check-in/check-out, nhưng ' +
    'GateActionCommand.GateDeviceId của BookingService là int → 400. Lượt gửi xe vẫn tạo nhưng booking kẹt ở Confirmed.',
  paymentNotConfirmingBooking:
    'GAP-E2E-02 (TV3/TV4): BookingService chưa nhận PaymentSucceeded nên booking không tự sang Confirmed sau thanh toán. ' +
    'E2E dùng POST /bookings/{code}/approve của chủ bãi để xác nhận.',
} as const;

export function knownBug(description: string) {
  test.info().annotations.push({ type: 'known-bug', description });
}

/** Biển số ngẫu nhiên đúng định dạng VN (51E-123.45) để mỗi lần chạy không trùng lượt gửi xe đang mở. */
export function randomPlate(): string {
  const n = (max: number, width: number) => String(Math.floor(Math.random() * max)).padStart(width, '0');
  return `51E-${n(1000, 3)}.${n(100, 2)}`;
}

export interface BookingInfo { code: string; plate: string; qrToken: string; status: string; holdExpiresAtUtc: string }

/** Tài xế đặt chỗ tại bãi Vincom (US-022). Mặc định bắt đầu sau 20 phút, gửi 2 giờ. */
export async function createBooking(driver: Api, opts: { plate?: string; startInMinutes?: number } = {}) {
  const plate = opts.plate ?? randomPlate();
  const start = new Date(Date.now() + (opts.startInMinutes ?? 20) * 60_000);
  const end = new Date(start.getTime() + 2 * 3_600_000);
  return driver.post('/api/v1/bookings', {
    userId: ACCOUNTS.driver.userId,
    vehicleId: DRIVER_CAR_ID,
    plateNumber: plate,
    vehicleType: 'Sedan',
    parkingLotId: LOT_VINCOM.id,
    ownerProfileId: LOT_VINCOM.ownerProfileId,
    parkingLotName: LOT_VINCOM.name,
    zoneId: null,
    slotId: null,
    slotCode: null,
    allocationMode: 'Dynamic',
    startAtUtc: start.toISOString(),
    endAtUtc: end.toISOString(),
    totalAmount: 50_000,
    promotionCode: null,
  });
}

/** Booking đã Confirmed, sẵn sàng qua cổng. */
export async function confirmedBooking(driver: Api, owner: Api): Promise<BookingInfo> {
  const created = await createBooking(driver);
  expect(created.status, JSON.stringify(created.body)).toBe(201);
  knownBug(KNOWN_BUGS.paymentNotConfirmingBooking);
  const approved = await owner.post(`/api/v1/bookings/${created.body.code}/approve`, {
    ownerProfileId: LOT_VINCOM.ownerProfileId,
  });
  expect(approved.status, JSON.stringify(approved.body)).toBe(200);
  expect(approved.body.status).toBe('Confirmed');
  return { ...created.body, plate: created.body.plateNumber, status: 'Confirmed' };
}

export async function bookingStatus(api: Api, code: string): Promise<string> {
  const res = await api.get(`/api/v1/bookings/${code}`);
  expect(res.status).toBe(200);
  return res.body.status;
}

/**
 * Bảo đảm booking đã sang trạng thái cổng mong muốn. Khi BUG-E2E-01 còn tồn tại, gọi thay GateService
 * endpoint nội bộ của BookingService (đúng hợp đồng: gateDeviceId là số) để luồng tài xế đi tiếp được.
 */
export async function ensureGateSynced(api: Api, code: string, target: 'CheckedIn' | 'Completed') {
  if ((await bookingStatus(api, code)) === target) return;
  knownBug(KNOWN_BUGS.gateBookingSync);
  const action = target === 'CheckedIn' ? 'check-in' : 'check-out';
  const res = await api.post(`/api/v1/bookings/${code}/${action}`, { staffUserId: ACCOUNTS.staff.userId, gateDeviceId: 0 });
  expect(res.status, JSON.stringify(res.body)).toBe(200);
  expect(res.body.status).toBe(target);
}

export function gateCheckInByQr(staff: Api, booking: Pick<BookingInfo, 'plate' | 'qrToken'>, parkingLotId = LOT_VINCOM.id) {
  return staff.post('/api/v1/gate/check-in', {
    parkingLotId, method: 'Qr', plate: booking.plate, qrToken: booking.qrToken, staffUserId: ACCOUNTS.staff.userId,
  });
}

export function gateCheckOut(staff: Api, plate: string) {
  return staff.post('/api/v1/gate/check-out', {
    parkingLotId: LOT_VINCOM.id, plate, method: 'Manual', staffUserId: ACCOUNTS.staff.userId, confirmFee: true,
  });
}

/** Trọn vòng: đặt → xác nhận → vào cổng bằng QR → ra cổng chốt phí → booking Completed. */
export async function completedBooking(driver: Api, owner: Api, staff: Api): Promise<BookingInfo> {
  const booking = await confirmedBooking(driver, owner);
  const checkIn = await gateCheckInByQr(staff, booking);
  expect([200, 503], JSON.stringify(checkIn.body)).toContain(checkIn.status);
  await ensureGateSynced(driver, booking.code, 'CheckedIn');
  const checkOut = await gateCheckOut(staff, booking.plate);
  expect(checkOut.status, JSON.stringify(checkOut.body)).toBe(200);
  await ensureGateSynced(driver, booking.code, 'Completed');
  return { ...booking, status: 'Completed' };
}
