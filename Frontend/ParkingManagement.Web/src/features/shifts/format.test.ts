import { describe, expect, it } from 'vitest';
import type { ShiftView } from '@/api/shiftApi';
import { cashGaps, isPendingHandover } from './handover';
import { errorText, ictDate, ictTime, mondayOf, money, parseMoney, shiftRangeUtc, tokenLotIds } from './format';

const apiError = (message: string, status?: number, code?: string) => Object.assign(new Error(message), { status, code });

describe('giờ Việt Nam', () => {
  it('ca đêm: giờ kết thúc sang ngày kế tiếp, ngày nghiệp vụ là ngày bắt đầu theo ICT', () => {
    const range = shiftRangeUtc('2026-11-27', '22:00', '06:00');
    expect(range).toEqual({ startUtc: '2026-11-27T15:00:00.000Z', endUtc: '2026-11-27T23:00:00.000Z' });
    expect(ictDate(range.endUtc)).toBe('2026-11-28');
    expect(ictTime(range.endUtc)).toBe('06:00');
  });
  it('thứ Hai của tuần, kể cả Chủ nhật', () => {
    expect(mondayOf('2026-11-29')).toBe('2026-11-23');
    expect(mondayOf('2026-11-30')).toBe('2026-11-30');
  });
});

describe('tiền', () => {
  it('nhận số có dấu chấm, từ chối rỗng / âm / chữ', () => {
    expect(parseMoney('1.250.000')).toBe(1_250_000);
    expect(parseMoney('0')).toBe(0);
    expect([parseMoney(''), parseMoney('-5'), parseMoney('abc'), parseMoney('1,5')]).toEqual([null, null, null, null]);
    expect(money(-50000)).toBe('−50.000 ₫');
  });
});

describe('errorText', () => {
  it('ưu tiên code, rồi thông báo tiếng Việt, rồi status – không lộ tiếng Anh', () => {
    expect(errorText(apiError('x', 409, 'SHIFT_NOT_ACTIVE'))).toContain('Chưa có ca đang trực');
    expect(errorText(apiError('Hãy nhận bàn giao két trước.', 409, 'HANDOVER_NOT_ACCEPTED'))).toBe('Hãy nhận bàn giao két trước.');
    expect(errorText(apiError('One or more validation errors occurred.', 400))).toContain('Dữ liệu chưa hợp lệ');
    expect(errorText(apiError('Request failed with status code 429', 429))).toContain('quá nhanh');
    expect(errorText(apiError('timeout of 15000ms exceeded'))).toContain('Không kết nối');
  });
});

describe('tokenLotIds', () => {
  const token = (payload: object) => `x.${btoa(JSON.stringify(payload))}.y`;
  it('một hoặc nhiều bãi; token hỏng → rỗng', () => {
    expect(tokenLotIds(token({ ParkingLotId: '3' }))).toEqual([3]);
    expect(tokenLotIds(token({ ParkingLotId: ['1', '2'] }))).toEqual([1, 2]);
    expect(tokenLotIds('rác')).toEqual([]);
  });
});

describe('cashGaps (R19)', () => {
  const shift = { isCashHolder: true, status: 'HandoverPending', cashSystem: 1_000_000, cashCounted: 1_200_000, cashReceived: null, handoverAcceptedAtUtc: null } as ShiftView;
  it('số phải có = đầu ca + thu trong ca; chưa đủ dữ liệu → null, không coi là 0', () => {
    expect(cashGaps(shift, 250_000)).toEqual({ expected: 1_250_000, end: -50_000, handover: null });
    expect(cashGaps({ ...shift, cashSystem: null }, 250_000)).toEqual({ expected: null, end: null, handover: null });
  });
  it('người nhận so với người giao; ca tự đóng so với số phải có', () => {
    expect(cashGaps({ ...shift, cashReceived: 1_180_000 }, 250_000).handover).toBe(-20_000);
    expect(cashGaps({ ...shift, cashCounted: null, cashReceived: 1_250_000 }, 250_000).handover).toBe(0);
  });
  it('chờ bàn giao chỉ áp dụng cho ca giữ két chưa được nhận', () => {
    expect(isPendingHandover(shift)).toBe(true);
    expect(isPendingHandover({ ...shift, isCashHolder: false })).toBe(false);
    expect(isPendingHandover({ ...shift, handoverAcceptedAtUtc: '2026-11-27T07:00:00Z' })).toBe(false);
  });
});
