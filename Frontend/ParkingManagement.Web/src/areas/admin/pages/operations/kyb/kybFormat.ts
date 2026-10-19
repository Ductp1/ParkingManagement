import { formatDate } from '../../../utils/format';

export const KYB_BASE = '/admin/operations/kyb-review';

/** Mã hiển thị hồ sơ KYB – entity chỉ có Id (chưa có cột Code dạng KYB-2026-0031). */
export const kybCode = (id: number) => `#${id}`;

/** "Yes · 16/10/2026" / "No" như cột Field survey trên Figma. */
export const surveyText = (required: boolean, at: string | null) => (required ? `Yes · ${formatDate(at)}` : 'No');

/** Tên file hiển thị từ URL tài liệu (giay-phep-kinh-doanh.pdf). */
export const fileNameFromUrl = (url: string) => {
  try {
    const path = new URL(url, window.location.origin).pathname;
    return decodeURIComponent(path.split('/').filter(Boolean).pop() ?? url);
  } catch {
    return url;
  }
};
