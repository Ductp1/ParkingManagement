/**
 * US-054: mốc tọa độ để đối chiếu GPS ảnh hiện trường KYB.
 * Lấy đúng tọa độ đã đăng ký của bãi (BE `SubmitKybApplicationUseCase` cũng so với tọa độ này, bán kính 500 m).
 * Không có bãi hoặc tọa độ không hợp lệ (thiếu, NaN, ngoài phạm vi, 0/0 mặc định) → null: FE không chặn, để BE kiểm tra.
 */
export interface LotCoordinates {
  latitude: number;
  longitude: number;
}

export const lotReferenceCoordinates = (
  lot: { latitude?: number | null; longitude?: number | null } | null | undefined
): LotCoordinates | null => {
  if (!lot) return null;
  const { latitude, longitude } = lot;
  if (typeof latitude !== 'number' || typeof longitude !== 'number') return null;
  if (!Number.isFinite(latitude) || !Number.isFinite(longitude)) return null;
  if (Math.abs(latitude) > 90 || Math.abs(longitude) > 180) return null;
  if (latitude === 0 && longitude === 0) return null;
  return { latitude, longitude };
};
