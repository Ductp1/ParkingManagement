import {
  MAX_RANGE_DAYS,
  resolveRange,
  type CommissionPeriod,
  type CommissionRange,
} from '../finance/commission/commissionPeriod';

/**
 * P0-5 (US-081): kỳ của Reports › Revenue – dùng lại cách tính khoảng ICT của trang Commission (resolveRange),
 * nhãn và thông báo lỗi tiếng Anh (commissionPeriod.ts vẫn trả tiếng Việt cho trang Commission; Nhánh 5 thống nhất).
 */
export const REVENUE_PERIOD_OPTIONS: Array<{ value: CommissionPeriod; label: string }> = [
  { value: 'month', label: 'This month' },
  { value: 'prev-month', label: 'Last month' },
  { value: '7d', label: 'Last 7 days' },
  { value: 'custom', label: 'Custom range' },
];

export type RevenueRangeResult = { ok: true; range: CommissionRange } | { ok: false; error: string; period: CommissionPeriod };

const DATE_RE = /^\d{4}-\d{2}-\d{2}$/;
const DAY_MS = 24 * 60 * 60 * 1000;

const customError = (from: string, to: string): string => {
  if (!DATE_RE.test(from) || !DATE_RE.test(to) || Number.isNaN(Date.parse(from)) || Number.isNaN(Date.parse(to))) {
    return 'Pick a valid start and end date.';
  }
  if (from > to) return 'The start date must be on or before the end date.';
  if ((Date.parse(to) - Date.parse(from)) / DAY_MS + 1 > MAX_RANGE_DAYS) return `Pick a range of at most ${MAX_RANGE_DAYS} days.`;
  return 'Pick a valid start and end date.';
};

/** Kỳ từ URL (?period=&from=&to=); sai → lỗi tiếng Anh và màn hình KHÔNG gọi API. */
export const resolveRevenueRange = (period: string, from: string, to: string, now: Date = new Date()): RevenueRangeResult => {
  const result = resolveRange(period, from, to, now);
  return result.ok ? result : { ok: false, period: result.period, error: customError(from, to) };
};

/** Tỉ lệ phần trăm có một chữ số thập phân theo vi-VN (12,5%); mẫu số 0 → null. */
export const share = (part: number, whole: number): number | null => (whole > 0 ? (part / whole) * 100 : null);
