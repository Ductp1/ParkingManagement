import type { ClosedReason, ShiftStatus } from '@/api/shiftApi';

// ===== Giờ Việt Nam (ICT, UTC+7, không có giờ mùa hè) – không phụ thuộc múi giờ của trình duyệt. =====
const ICT_MS = 7 * 3_600_000;
const ict = (iso: string | number | Date) => new Date(new Date(iso).getTime() + ICT_MS);
const DAYS = ['Chủ nhật', 'Thứ 2', 'Thứ 3', 'Thứ 4', 'Thứ 5', 'Thứ 6', 'Thứ 7'];

/** yyyy-MM-dd theo giờ Việt Nam. */
export const ictDate = (iso: string | number | Date) => ict(iso).toISOString().slice(0, 10);
/** HH:mm theo giờ Việt Nam. */
export const ictTime = (iso: string | number | Date) => ict(iso).toISOString().slice(11, 16);
export const addDays = (date: string, days: number) =>
  new Date(Date.parse(`${date}T00:00:00Z`) + days * 86_400_000).toISOString().slice(0, 10);
const weekday = (date: string) => new Date(`${date}T00:00:00Z`).getUTCDay();
export const mondayOf = (date: string) => addDays(date, -((weekday(date) + 6) % 7));
/** 26/11 */
export const dayMonth = (date: string) => `${date.slice(8, 10)}/${date.slice(5, 7)}`;
/** 26/11/2026 */
export const fullDate = (date: string) => `${dayMonth(date)}/${date.slice(0, 4)}`;
/** Thứ 5, 26/11 */
export const dayLabel = (date: string) => `${DAYS[weekday(date)]}, ${dayMonth(date)}`;
export const weekLabel = (monday: string) => `${dayMonth(monday)} – ${fullDate(addDays(monday, 6))}`;
export const timeRange = (startUtc: string, endUtc: string) => `${ictTime(startUtc)}–${ictTime(endUtc)}`;
export const dateTime = (iso: string) => `${fullDate(ictDate(iso))} ${ictTime(iso)}`;

/** Ngày + giờ nhập trên form (giờ Việt Nam) → UTC. Giờ kết thúc không sau giờ bắt đầu = sang ngày kế tiếp. */
export const shiftRangeUtc = (date: string, start: string, end: string) => {
  const toUtc = (d: string, t: string) => new Date(`${d}T${t}:00+07:00`).toISOString();
  return { startUtc: toUtc(date, start), endUtc: toUtc(end <= start ? addDays(date, 1) : date, end) };
};

// ===== Tiền =====
export const money = (v: number) => `${v < 0 ? '−' : ''}${Math.abs(v).toLocaleString('vi-VN')} ₫`;
/** Chuỗi người dùng nhập ("1.250.000") → số; rỗng hoặc không phải chữ số → null. */
export const parseMoney = (text: string): number | null => {
  const digits = text.replace(/[.\s₫]/g, '');
  return /^\d{1,12}$/.test(digits) ? Number(digits) : null;
};

// ===== Nhãn =====
export const STATUS_LABEL: Record<ShiftStatus, string> = {
  Scheduled: 'Đã xếp lịch',
  Open: 'Đang trực',
  HandoverPending: 'Chờ nhận bàn giao',
  Completed: 'Hoàn tất',
  Missed: 'Vắng ca',
  AutoClosed: 'Tự đóng',
};
export const CLOSED_REASON_LABEL: Record<ClosedReason, string> = {
  Normal: 'Kết ca bình thường',
  Forgot: 'Quên kết ca',
  StaffRevoked: 'Đã bị gỡ khỏi bãi',
  StaffSuspended: 'Tài khoản bị đình chỉ',
  OwnerLocked: 'Hồ sơ chủ bãi bị khóa',
};
export const lotStatusLabel = (status?: string | null) =>
  !status ? 'Chưa rõ trạng thái' : status === 'Active' ? 'Đang hoạt động' : status === 'Suspended' ? 'Tạm đình chỉ' : 'Chưa hoạt động';
export const staffName = (userId: number, names: Map<number, string>, selfId?: number) =>
  names.get(userId) ?? (userId === selfId ? 'Bạn' : `Nhân viên #${userId}`);

// ===== Lỗi: ProblemDetails.code → tiếng Việt =====
const CODE_TEXT: Record<string, string> = {
  STAFF_EMAIL_HAS_ACCOUNT: 'Email này thuộc tài khoản Tài xế hoặc Chủ bãi. Hãy dùng email riêng cho nhân viên.',
  STAFF_EMAIL_OTHER_OWNER: 'Nhân viên này đang được chủ bãi khác phân công. Hãy dùng email khác.',
  STAFF_ALREADY_ASSIGNED: 'Nhân viên đã được phân công tại bãi này.',
  STAFF_EMAIL_REQUIRED: 'Hãy nhập email hợp lệ của nhân viên để gửi link kích hoạt.',
  STAFF_ALREADY_ACTIVE: 'Nhân viên đã kích hoạt tài khoản. Không cần gửi lại lời mời.',
  ACTIVATION_TOKEN_INVALID: 'Link đã hết hạn hoặc đã được sử dụng. Hãy liên hệ Chủ bãi để gửi lại lời mời.',
  SHIFT_OVERLAP: 'Nhân viên đã có ca trùng giờ (tính cả ca tại bãi khác).',
  CASH_HOLDER_OVERLAP: 'Mỗi thời điểm chỉ một ca được giữ két. Bỏ chọn “Giữ két” hoặc đổi giờ.',
  STAFF_NOT_IN_LOT: 'Nhân viên chưa được phân công tại bãi này.',
  SHIFT_START_TOO_EARLY: 'Chưa đến giờ bắt đầu ca. Chỉ được bắt đầu sớm tối đa 15 phút.',
  SHIFT_EXPIRED: 'Ca đã quá giờ kết thúc, không thể bắt đầu.',
  SHIFT_ALREADY_OPEN: 'Bạn đang có một ca chưa kết thúc.',
  SHIFT_ALREADY_STARTED: 'Ca đã bắt đầu hoặc đã đóng, không thể thực hiện thao tác này.',
  SHIFT_NOT_ACTIVE: 'Chưa có ca đang trực. Hãy bắt đầu ca để thao tác ở cổng.',
  LOT_NOT_OPERATIONAL: 'Bãi đang không hoạt động và không còn xe trong bãi, không thể mở ca.',
  NO_CASH_HOLDER: 'Hiện không có ai giữ két tại bãi. Hãy liên hệ Chủ bãi.',
  CASH_COUNT_REQUIRED: 'Hãy đếm két và nhập số tiền mặt.',
  CASH_RECEIVED_REQUIRED: 'Hãy đếm lại và nhập số tiền bạn thực nhận.',
  GATE_PAYMENT_EXISTS: 'Đã có khoản thanh toán khác cho lượt xe. Không tạo thêm khoản thu; hãy kiểm tra giao dịch hoặc liên hệ Chủ bãi.',
};
const STATUS_TEXT: Record<number, string> = {
  400: 'Dữ liệu chưa hợp lệ. Kiểm tra lại các trường đã nhập.',
  401: 'Phiên đăng nhập đã hết hạn. Hãy đăng nhập lại.',
  403: 'Bạn không có quyền thực hiện thao tác này.',
  404: 'Không tìm thấy dữ liệu. Tải lại rồi thử lại.',
  409: 'Dữ liệu vừa thay đổi. Tải lại rồi thử lại.',
  429: 'Bạn thao tác quá nhanh. Đợi một phút rồi thử lại.',
  503: 'Dịch vụ chưa sẵn sàng. Thử lại sau ít phút.',
};
const VIETNAMESE = /[à-ỹđĐÀ-Ỹ]/;

export const errorCode = (err: unknown) => (err as { code?: string } | null)?.code;
export const errorStatus = (err: unknown) => (err as { status?: number } | null)?.status;

/** Mọi lỗi API ra tiếng Việt: theo code, rồi thông báo tiếng Việt của backend, cuối cùng theo HTTP status. */
export function errorText(err: unknown): string {
  const code = errorCode(err);
  if (code && CODE_TEXT[code]) return CODE_TEXT[code];
  const message = err instanceof Error ? err.message : '';
  if (VIETNAMESE.test(message)) return message;
  const status = errorStatus(err);
  if (status === undefined) return 'Không kết nối được máy chủ. Kiểm tra mạng rồi thử lại.';
  return STATUS_TEXT[status] ?? 'Hệ thống gặp lỗi. Thử lại sau ít phút.';
}

/** Bãi được phân công của Staff, đọc từ claim ParkingLotId trong access token (chỉ để hiển thị; server vẫn tự kiểm quyền). */
export function tokenLotIds(token: string | null): number[] {
  try {
    const payload = JSON.parse(atob((token ?? '').split('.')[1].replace(/-/g, '+').replace(/_/g, '/'))) as { ParkingLotId?: string | string[] };
    return [payload.ParkingLotId ?? []].flat().map(Number).filter((id) => id > 0);
  } catch {
    return [];
  }
}
