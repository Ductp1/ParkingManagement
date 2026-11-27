import { formatDate } from '../../../utils/format';

export const KYB_BASE = '/admin/operations/kyb-review';

/** Mã hiển thị hồ sơ KYB – entity chỉ có Id (chưa có cột Code dạng KYB-2026-0031). */
export const kybCode = (id: number) => `#${id}`;

/** Chủ bãi: tên doanh nghiệp thuộc UserService, ParkingService chỉ trả OwnerProfileId. */
export const ownerLabel = (ownerProfileId: number) => `OwnerProfile #${ownerProfileId}`;

/** "Done · 16/10/2026" / "Yes · 16/10/2026" / "No" cho cột Field survey. */
export const surveyText = (required: boolean, at: string | null, completedAt: string | null) =>
  completedAt ? `Done · ${formatDate(completedAt)}` : required ? `Yes · ${formatDate(at)}` : 'No';

/** "Thủ Đức, TP.HCM" – quận/huyện có thể trống. */
export const cityText = (city: string, district: string | null) => (district ? `${district}, ${city}` : city);

/** "06:00:00" → "06:00". */
export const shortTime = (value: string) => value.slice(0, 5);

/** Tên file hiển thị từ URL tài liệu (giay-phep-kinh-doanh.pdf). */
export const fileNameFromUrl = (url: string) => {
  try {
    const path = new URL(url, window.location.origin).pathname;
    return decodeURIComponent(path.split('/').filter(Boolean).pop() ?? url);
  } catch {
    return url;
  }
};
