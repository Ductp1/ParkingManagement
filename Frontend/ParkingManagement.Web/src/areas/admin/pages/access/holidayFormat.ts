/** US-071: định dạng / tham số cho màn Holidays. Ngày lễ là DateOnly "YYYY-MM-DD" – tính theo lịch, không đổi múi giờ. */

import { ictDateParam } from '../../utils/format';

/** Năm hiện tại theo giờ ICT (31/12 17:00 UTC đã là năm mới ở Việt Nam). */
export const currentIctYear = (): number => Number((ictDateParam(new Date().toISOString()) ?? '').slice(0, 4));

/** Khoảng năm PaymentService chấp nhận ở GET /api/v1/holidays?year=. */
export const MIN_HOLIDAY_YEAR = 2000;
export const MAX_HOLIDAY_YEAR = 2100;

const WEEKDAYS = ['Sunday', 'Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday'];

const parts = (date: string): [number, number, number] | null => {
  const m = /^(\d{4})-(\d{2})-(\d{2})$/.exec(date);
  return m ? [Number(m[1]), Number(m[2]), Number(m[3])] : null;
};

/** "2027-04-30" → "30/04/2027". */
export const formatHolidayDate = (date: string): string => {
  const p = parts(date);
  return p ? `${String(p[2]).padStart(2, '0')}/${String(p[1]).padStart(2, '0')}/${p[0]}` : date;
};

/** "2027-04-30" → "Friday" (theo lịch, dùng UTC để không lệch ngày theo múi giờ trình duyệt). */
export const holidayWeekday = (date: string): string => {
  const p = parts(date);
  return p ? WEEKDAYS[new Date(Date.UTC(p[0], p[1] - 1, p[2])).getUTCDay()] : '';
};

/** ?year= trên URL → năm hợp lệ; thiếu hoặc ngoài khoảng → năm hiện tại (ICT). */
export const parseHolidayYear = (raw: string, currentYear: number): number => {
  const year = Number(raw);
  return Number.isInteger(year) && year >= MIN_HOLIDAY_YEAR && year <= MAX_HOLIDAY_YEAR ? year : currentYear;
};

/** Năm trước, năm nay, 2 năm tới; thêm năm đang chọn nếu nằm ngoài (mở từ URL). */
export const holidayYearOptions = (currentYear: number, selected: number): number[] => {
  const years = [currentYear - 1, currentYear, currentYear + 1, currentYear + 2];
  if (!years.includes(selected)) years.push(selected);
  return years.sort((a, b) => a - b);
};
