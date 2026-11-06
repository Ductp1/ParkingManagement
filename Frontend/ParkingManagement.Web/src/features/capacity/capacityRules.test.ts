import { describe, expect, it } from 'vitest';
import {
  CAPACITY_ERROR_CODES,
  validateCapacityConfig,
  type CapacityTextValues,
} from './capacityRules';

const v = (capacity: string, transientBuffer: string, subscriberQuota: string): CapacityTextValues =>
  ({ capacity, transientBuffer, subscriberQuota });

/** US-066 – validate client: thông điệp đúng nguyên văn, mỗi ô một lỗi, lỗi tổng hợp riêng. */
describe('validateCapacityConfig', () => {
  it('hợp lệ: Q + B < C → không lỗi, parsed đủ', () => {
    const outcome = validateCapacityConfig(v('50', '10', '10'));
    expect(Object.keys(outcome.fieldErrors)).toHaveLength(0);
    expect(outcome.summary).toBeNull();
    expect(outcome.isValid).toBe(true);
    expect(outcome.parsed).toEqual({ capacity: 50, transientBuffer: 10, subscriberQuota: 10 });
  });

  it('Tổng số chỗ trống → CAPACITY_REQUIRED với thông điệp đúng', () => {
    const outcome = validateCapacityConfig(v('', '0', '0'));
    expect(outcome.fieldErrors.capacity).toEqual({
      code: CAPACITY_ERROR_CODES.capacityRequired,
      message: 'Hãy nhập tổng số chỗ của bãi (ví dụ: 100).',
    });
  });

  it('Tổng số chỗ = 0 → CAPACITY_INVALID', () => {
    const outcome = validateCapacityConfig(v('0', '0', '0'));
    expect(outcome.fieldErrors.capacity).toEqual({
      code: CAPACITY_ERROR_CODES.capacityInvalid,
      message: 'Tổng số chỗ phải là số nguyên từ 1 trở lên.',
    });
  });

  it('Tổng số chỗ âm → CAPACITY_INVALID', () => {
    const outcome = validateCapacityConfig(v('-5', '0', '0'));
    expect(outcome.fieldErrors.capacity?.code).toBe(CAPACITY_ERROR_CODES.capacityInvalid);
  });

  it('Vãng lai trống → TRANSIENT_REQUIRED với thông điệp đúng', () => {
    const outcome = validateCapacityConfig(v('50', '', '0'));
    expect(outcome.fieldErrors.transientBuffer).toEqual({
      code: CAPACITY_ERROR_CODES.transientRequired,
      message: 'Hãy nhập số chỗ cho xe vãng lai. Nhập 0 nếu không nhận.',
    });
  });

  it('Vãng lai âm → TRANSIENT_INVALID', () => {
    const outcome = validateCapacityConfig(v('50', '-1', '0'));
    expect(outcome.fieldErrors.transientBuffer?.code).toBe(CAPACITY_ERROR_CODES.transientInvalid);
  });

  it('Vãng lai số lẻ (10.5) → TRANSIENT_INVALID', () => {
    const outcome = validateCapacityConfig(v('50', '10.5', '0'));
    expect(outcome.fieldErrors.transientBuffer?.code).toBe(CAPACITY_ERROR_CODES.transientInvalid);
  });

  it('Đăng ký trống → SUBSCRIBER_REQUIRED với thông điệp đúng', () => {
    const outcome = validateCapacityConfig(v('50', '0', ''));
    expect(outcome.fieldErrors.subscriberQuota).toEqual({
      code: CAPACITY_ERROR_CODES.subscriberRequired,
      message: 'Hãy nhập số chỗ cho khách đăng ký. Nhập 0 nếu không nhận.',
    });
  });

  it('Đăng ký số lẻ → SUBSCRIBER_INVALID', () => {
    const outcome = validateCapacityConfig(v('50', '0', '2.5'));
    expect(outcome.fieldErrors.subscriberQuota?.code).toBe(CAPACITY_ERROR_CODES.subscriberInvalid);
  });

  it('B > C → lỗi gắn vào ô vãng lai với thông điệp có số', () => {
    const outcome = validateCapacityConfig(v('50', '60', '0'));
    expect(outcome.fieldErrors.transientBuffer).toEqual({
      code: CAPACITY_ERROR_CODES.transientExceeds,
      message: 'Chỗ cho xe vãng lai (60) nhiều hơn tổng số chỗ (50). Hãy giảm xuống tối đa 50.',
    });
  });

  it('Q > C → lỗi gắn vào ô đăng ký với thông điệp có số', () => {
    const outcome = validateCapacityConfig(v('50', '0', '60'));
    expect(outcome.fieldErrors.subscriberQuota).toEqual({
      code: CAPACITY_ERROR_CODES.subscriberExceeds,
      message: 'Chỗ cho khách đăng ký (60) nhiều hơn tổng số chỗ (50). Hãy giảm xuống tối đa 50.',
    });
  });

  it('B + Q > C → lỗi tổng hợp riêng (không gắn field), nói rõ thiếu bao nhiêu', () => {
    const outcome = validateCapacityConfig(v('50', '30', '30'));
    expect(Object.keys(outcome.fieldErrors)).toHaveLength(0);
    expect(outcome.summary).toEqual({
      code: CAPACITY_ERROR_CODES.totalExceeds,
      message: 'Vãng lai (30) + đăng ký (30) = 60, vượt tổng số chỗ 50. Hãy giảm bớt ít nhất 10 chỗ.',
    });
    expect(outcome.isValid).toBe(false);
  });

  it('biên B = C, Q = C → hợp lệ', () => {
    expect(validateCapacityConfig(v('50', '50', '0')).isValid).toBe(true);
    expect(validateCapacityConfig(v('50', '0', '50')).isValid).toBe(true);
  });

  it('biên B + Q = C → hợp lệ; B + Q = C + 1 → lỗi tổng hợp thiếu 1', () => {
    expect(validateCapacityConfig(v('50', '25', '25')).summary).toBeNull();
    const outcome = validateCapacityConfig(v('50', '25', '26'));
    expect(outcome.summary?.message).toBe('Vãng lai (25) + đăng ký (26) = 51, vượt tổng số chỗ 50. Hãy giảm bớt ít nhất 1 chỗ.');
  });
});

/** US-066 yêu cầu 4 – "Chỗ dùng chung": chỉ tính khi đủ 3 ô hợp lệ. */
describe('shared zone (chỗ dùng chung)', () => {
  it('còn ô trống → state incomplete, hiện "—"', () => {
    const outcome = validateCapacityConfig(v('50', '', '10'));
    expect(outcome.shared).toEqual({ state: 'incomplete', value: null, deficit: null });
  });

  it('nhập âm → state invalid, KHÔNG tính ra số dương', () => {
    const outcome = validateCapacityConfig(v('50', '-2', '0'));
    expect(outcome.shared.state).toBe('invalid');
    expect(outcome.shared.value).toBeNull();
  });

  it('hợp lệ → đúng số C - B - Q', () => {
    const outcome = validateCapacityConfig(v('50', '10', '10'));
    expect(outcome.shared).toEqual({ state: 'ok', value: 30, deficit: null });
  });

  it('B + Q > C → overflow kèm deficit, không có số âm trơ trọi', () => {
    const outcome = validateCapacityConfig(v('50', '30', '30'));
    expect(outcome.shared).toEqual({ state: 'overflow', value: null, deficit: 10 });
  });
});

describe('isValid (regression: PUT với payload rỗng)', () => {
  it('false khi còn lỗi từng ô dù summary null', () => {
    expect(validateCapacityConfig(v('', '0', '0')).isValid).toBe(false);
    expect(validateCapacityConfig(v('50', '60', '0')).isValid).toBe(false);
    expect(validateCapacityConfig(v('50', '30', '30')).isValid).toBe(false); // chỉ summary
    expect(validateCapacityConfig(v('50', '10', '10')).isValid).toBe(true);
  });
});