/** Định dạng hiển thị của Admin Portal: giờ ICT (Asia/Ho_Chi_Minh), số và tiền theo vi-VN. */
const TIME_ZONE = 'Asia/Ho_Chi_Minh';

const dateTimeFmt = new Intl.DateTimeFormat('en-GB', {
  timeZone: TIME_ZONE,
  day: '2-digit',
  month: '2-digit',
  year: 'numeric',
  hour: '2-digit',
  minute: '2-digit',
  hour12: false,
});
const dateFmt = new Intl.DateTimeFormat('en-GB', { timeZone: TIME_ZONE, day: '2-digit', month: '2-digit', year: 'numeric' });
const shortFmt = new Intl.DateTimeFormat('en-GB', {
  timeZone: TIME_ZONE,
  day: '2-digit',
  month: '2-digit',
  hour: '2-digit',
  minute: '2-digit',
  hour12: false,
});
const numberFmt = new Intl.NumberFormat('vi-VN');

export const EMPTY = '—';

const parse = (value: string | null | undefined): Date | null => {
  if (!value) return null;
  const d = new Date(value);
  return Number.isNaN(d.getTime()) ? null : d;
};

/** 09/10/2026 14:02 */
export const formatDateTime = (value: string | null | undefined): string => {
  const d = parse(value);
  return d ? dateTimeFmt.format(d).replace(',', '') : EMPTY;
};

/** 09/10/2026 14:02 ICT */
export const formatDateTimeIct = (value: string | null | undefined): string => {
  const text = formatDateTime(value);
  return text === EMPTY ? EMPTY : `${text} ICT`;
};

/** 09/10/2026 */
export const formatDate = (value: string | null | undefined): string => {
  const d = parse(value);
  return d ? dateFmt.format(d) : EMPTY;
};

/** 19/10 10:42 */
export const formatShortDateTime = (value: string | null | undefined): string => {
  const d = parse(value);
  return d ? shortFmt.format(d).replace(',', '') : EMPTY;
};

/** ₫60.000 */
export const formatVnd = (value: number | null | undefined): string =>
  value === null || value === undefined ? EMPTY : `₫${numberFmt.format(value)}`;

/** 1.284 */
export const formatNumber = (value: number | null | undefined): string =>
  value === null || value === undefined ? EMPTY : numberFmt.format(value);

export const orEmpty = (value: string | number | null | undefined): string =>
  value === null || value === undefined || value === '' ? EMPTY : String(value);

/** Giá trị input datetime-local (giờ trình duyệt) → ISO UTC gửi API. */
export const localInputToUtcIso = (value: string): string | null => {
  if (!value) return null;
  const d = new Date(value);
  return Number.isNaN(d.getTime()) ? null : d.toISOString();
};

/** Ngày giờ hiện tại dạng giá trị cho input datetime-local (giờ trình duyệt). */
export const nowLocalInputValue = (): string => {
  const d = new Date();
  d.setSeconds(0, 0);
  const offset = d.getTimezoneOffset() * 60000;
  return new Date(d.getTime() - offset).toISOString().slice(0, 16);
};
