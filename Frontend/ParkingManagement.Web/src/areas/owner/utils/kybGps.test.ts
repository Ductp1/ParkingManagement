import { describe, expect, it } from 'vitest';
import { calculateHaversineDistanceMeters } from '@/utils/haversine';
import { lotReferenceCoordinates } from './kybGps';

describe('lotReferenceCoordinates (US-054)', () => {
  it('dùng đúng tọa độ đã đăng ký của bãi #5, không phải tọa độ Q.1', () => {
    expect(lotReferenceCoordinates({ latitude: 10.851325, longitude: 106.797915 })).toEqual({ latitude: 10.851325, longitude: 106.797915 });
  });

  it.each([
    [null],
    [undefined],
    [{}],
    [{ latitude: null, longitude: null }],
    [{ latitude: 0, longitude: 0 }],
    [{ latitude: Number.NaN, longitude: 106.7 }],
    [{ latitude: 91, longitude: 106.7 }],
    [{ latitude: 10.8, longitude: 181 }],
  ])('không có tọa độ hợp lệ %o → null (FE không chặn, BE kiểm tra)', (lot) => {
    expect(lotReferenceCoordinates(lot as never)).toBeNull();
  });

  it('ảnh chụp tại bãi #5 lệch 0 m; tọa độ Q.1 lệch hơn 500 m', () => {
    const lot = lotReferenceCoordinates({ latitude: 10.851325, longitude: 106.797915 })!;
    expect(calculateHaversineDistanceMeters(lot.latitude, lot.longitude, 10.851325, 106.797915)).toBe(0);
    expect(calculateHaversineDistanceMeters(lot.latitude, lot.longitude, 10.776889, 106.700806)).toBeGreaterThan(500);
  });
});
