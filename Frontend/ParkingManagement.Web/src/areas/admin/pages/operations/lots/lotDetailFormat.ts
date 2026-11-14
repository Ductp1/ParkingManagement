import type { LotClosureDto, LotOperatingHourItemDto } from '@/types/adminLots';
import type { StatusTone } from '../../../utils/enumLabels';
import { formatNumber } from '../../../utils/format';

/** Định dạng cho 4 sub-tab Lot detail (P0-4, US-081). */

/** 1,5 → "×1,5". */
export const multiplier = (value: number) => `×${value.toLocaleString('vi-VN', { maximumFractionDigits: 2 })}`;

/** "07:00:00" → "07:00". */
export const clock = (value: string) => value.slice(0, 5);

/** Giờ mở cửa một ngày: Closed / Open 24 hours / 07:00 – 22:00 (qua đêm thêm "(next day)"). */
export const hoursText = (d: LotOperatingHourItemDto) => {
  if (d.isClosed) return 'Closed';
  if (d.is24Hours) return 'Open 24 hours';
  return `${clock(d.openTime)} – ${clock(d.closeTime)}${d.isOvernight ? ' (next day)' : ''}`;
};

/** Thứ Hai trước, Chủ nhật cuối (DayOfWeek: Sunday = 0). */
export const mondayFirst = (dayOfWeekNumber: number) => (dayOfWeekNumber + 6) % 7;

/** Khung phút của quy tắc giá: "0–60 min" / "from 120 min". */
export const band = (from: number, to: number | null) =>
  to === null ? `from ${formatNumber(from)} min` : `${formatNumber(from)}–${formatNumber(to)} min`;

const CLOSURE_ORDER: Record<string, number> = { Active: 0, Upcoming: 1, Past: 2, Cancelled: 3 };

/** Nhãn trạng thái lịch đóng cửa (ParkingService trả Active | Upcoming | Past | Cancelled). */
export const CLOSURE_LABEL: Record<string, { label: string; tone: StatusTone }> = {
  Active: { label: 'In effect', tone: 'warning' },
  Upcoming: { label: 'Upcoming', tone: 'info' },
  Past: { label: 'Ended', tone: 'neutral' },
  Cancelled: { label: 'Cancelled', tone: 'neutral' },
};

/** Đang hiệu lực → sắp tới → đã qua → đã hủy; trong cùng nhóm, bắt đầu gần nhất trước. */
export const sortClosures = (items: LotClosureDto[]) =>
  [...items].sort((a, b) => (CLOSURE_ORDER[a.status] ?? 9) - (CLOSURE_ORDER[b.status] ?? 9) || b.startsAtUtc.localeCompare(a.startsAtUtc));

/** Các sub-tab của Lot detail (dùng chung cho trang bãi công khai và bãi chưa công khai). */
export const LOT_DETAIL_TABS = [
  { key: '', label: 'Information', available: true },
  { key: 'layout', label: 'Layout', available: true },
  { key: 'capacity', label: 'Capacity', available: true },
  { key: 'hours', label: 'Hours & Closures', available: true },
  { key: 'amenities', label: 'Amenities & Photos', available: true },
  // Chưa có nguồn dữ liệu cho tích hợp/thiết bị theo bãi → ẩn khỏi sub-tab, URL cũ hiện màn "not available yet".
  { key: 'integration', label: 'Integration & Devices', available: false },
  { key: 'pricing', label: 'Pricing', available: true },
];

/** Sub-tab hiện trên trang chi tiết bãi. */
export const lotDetailSubTabs = (base: string) =>
  LOT_DETAIL_TABS.filter((t) => t.available).map((t) => ({ label: t.label, to: t.key ? `${base}/${t.key}` : base }));

/** Tab có trong URL nhưng chưa có nguồn dữ liệu. */
export const isLotTabUnavailable = (tab: string): boolean => LOT_DETAIL_TABS.some((t) => t.key === tab && !t.available);

/** Vì sao tài xế không thấy bãi (GET /parking-lots/{id} trả 404 cho bãi chưa công khai). */
export const hiddenLotNotice = (status: string): string =>
  status === 'Suspended'
    ? "Drivers can't see or book this lot while it is suspended."
    : status === 'PendingApproval'
      ? "Drivers can't see this lot until its KYB review is approved."
      : "Drivers can't see this lot right now.";
