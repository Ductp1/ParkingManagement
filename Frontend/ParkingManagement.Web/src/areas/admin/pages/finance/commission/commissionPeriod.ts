/**
 * US-079: kỳ báo cáo của trang Hoa hồng – tính theo ngày Việt Nam (ICT, UTC+7, không có giờ mùa hè).
 * URL: ?period=month|prev-month|7d|custom&from=YYYY-MM-DD&to=YYYY-MM-DD (from / to là ngày ICT, to BAO GỒM).
 * API nhận [fromUtc, toUtc) = 00:00 ICT ngày from → 00:00 ICT ngày sau to.
 */

export type CommissionPeriod = 'month' | 'prev-month' | '7d' | 'custom';

export const PERIOD_OPTIONS: Array<{ value: CommissionPeriod; label: string }> = [
  { value: 'month', label: 'Tháng này' },
  { value: 'prev-month', label: 'Tháng trước' },
  { value: '7d', label: '7 ngày qua' },
  { value: 'custom', label: 'Tùy chọn' },
];

/** Giới hạn của PaymentService (GetAdminCommissionSummaryUseCase.MaxRangeDays). */
export const MAX_RANGE_DAYS = 366;

const ICT_OFFSET_MS = 7 * 60 * 60 * 1000;
const DAY_MS = 24 * 60 * 60 * 1000;
const DATE_RE = /^(\d{4})-(\d{2})-(\d{2})$/;

/** Ngày theo lịch (không giờ) – lưu dạng số ngày kể từ 1970-01-01 để cộng trừ không lệch múi giờ. */
const toDayNumber = (iso: string): number | null => {
  const m = DATE_RE.exec(iso);
  if (!m) return null;
  const ms = Date.UTC(Number(m[1]), Number(m[2]) - 1, Number(m[3]));
  const d = new Date(ms);
  // Loại 2026-02-31…
  if (d.getUTCFullYear() !== Number(m[1]) || d.getUTCMonth() !== Number(m[2]) - 1 || d.getUTCDate() !== Number(m[3])) return null;
  return ms / DAY_MS;
};

const fromDayNumber = (day: number): string => new Date(day * DAY_MS).toISOString().slice(0, 10);

/** Ngày ICT hiện tại (YYYY-MM-DD). */
export const ictToday = (now: Date = new Date()): string => new Date(now.getTime() + ICT_OFFSET_MS).toISOString().slice(0, 10);

/** 00:00 ICT của ngày → ISO UTC ("2026-10-31T17:00:00.000Z"). */
const ictMidnightUtc = (day: number): string => new Date(day * DAY_MS - ICT_OFFSET_MS).toISOString();

export interface CommissionRange {
  period: CommissionPeriod;
  /** Ngày ICT đầu (bao gồm) và cuối (bao gồm). */
  from: string;
  to: string;
  fromUtc: string;
  toUtc: string;
  days: number;
}

export type RangeResult = { ok: true; range: CommissionRange } | { ok: false; error: string; period: CommissionPeriod };

const build = (period: CommissionPeriod, fromDay: number, toDay: number): CommissionRange => ({
  period,
  from: fromDayNumber(fromDay),
  to: fromDayNumber(toDay),
  fromUtc: ictMidnightUtc(fromDay),
  toUtc: ictMidnightUtc(toDay + 1),
  days: toDay - fromDay + 1,
});

export const isPeriod = (value: string): value is CommissionPeriod => PERIOD_OPTIONS.some((o) => o.value === value);

/**
 * Kỳ từ tham số URL. Mặc định "Tháng này" (từ ngày 1 tới hôm nay theo ICT). Tùy chọn: from ≤ to, tối đa 366 ngày;
 * sai → lỗi để màn hình báo và KHÔNG gọi API.
 */
export const resolveRange = (periodParam: string, fromParam: string, toParam: string, now: Date = new Date()): RangeResult => {
  const period: CommissionPeriod = isPeriod(periodParam) ? periodParam : 'month';
  const today = toDayNumber(ictToday(now))!;
  const [y, m] = ictToday(now).split('-').map(Number);

  switch (period) {
    case 'prev-month': {
      const first = Date.UTC(y, m - 2, 1) / DAY_MS;
      const last = Date.UTC(y, m - 1, 1) / DAY_MS - 1;
      return { ok: true, range: build(period, first, last) };
    }
    case '7d':
      return { ok: true, range: build(period, today - 6, today) };
    case 'custom': {
      const from = toDayNumber(fromParam);
      const to = toDayNumber(toParam);
      if (from === null || to === null) return { ok: false, period, error: 'Chọn ngày bắt đầu và ngày kết thúc hợp lệ.' };
      if (from > to) return { ok: false, period, error: 'Ngày bắt đầu phải trước hoặc bằng ngày kết thúc.' };
      if (to - from + 1 > MAX_RANGE_DAYS) return { ok: false, period, error: `Khoảng thời gian tối đa ${MAX_RANGE_DAYS} ngày.` };
      return { ok: true, range: build(period, from, to) };
    }
    default:
      return { ok: true, range: build('month', Date.UTC(y, m - 1, 1) / DAY_MS, today) };
  }
};

/** 2026-11-06 → 06/11/2026. */
export const displayDate = (iso: string): string => {
  const m = DATE_RE.exec(iso);
  return m ? `${m[3]}/${m[2]}/${m[1]}` : iso;
};

/** 2026-11-06 → 06/11. */
export const displayDayMonth = (iso: string): string => {
  const m = DATE_RE.exec(iso);
  return m ? `${m[3]}/${m[2]}` : iso;
};
