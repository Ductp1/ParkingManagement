import { AdminApiError } from '@/api/adminClientApi';
import type {
  OwnerPayoutAccountDto,
  SettlementFormulaDto,
  SettlementLineType,
  SettlementSummaryDto,
} from '@/types/adminSettlements';
import { EMPTY, formatDate, formatVnd } from '../../../utils/format';

export const SETTLEMENTS_PATH = '/admin/finance/settlements';
export const PAYOUTS_PATH = '/admin/finance/payouts';
export const ADJUSTMENTS_PATH = '/admin/finance/adjustments';

/** Lý do chặn gửi duyệt (mã từ PaymentService) → nhãn + cách xử lý. */
export const BLOCK_REASON_LABEL: Record<string, string> = {
  CommissionPending: 'Commission not calculated yet – wait for the commission job, then regenerate',
  BookingCodeMissing: 'A booking payment has no booking code',
  BookingNotFound: 'BookingService cannot find a booking of this period',
  BookingUnverifiable: 'A booking ended without a status history',
  HoldsNotLoaded: 'Dispute holds not loaded – regenerate to load them from SupportService',
};
export const blockReasonLabel = (code: string) => BLOCK_REASON_LABEL[code] ?? code;

export const LINE_TYPE_LABEL: Record<SettlementLineType, string> = {
  Booking: 'Booking',
  WalkIn: 'Walk-in / gate',
  Refund: 'Refund',
  Adjustment: 'Adjustment',
  DisputeHold: 'Dispute hold',
};

/** Kết quả bỏ qua khi sinh kỳ (SettlementOutcomes của PaymentService). */
export const SKIP_REASON_LABEL: Record<string, string> = {
  NothingToSettle: 'Nothing to settle',
  OpenEarlierSettlement: 'Earlier settlement still open',
  LaterSettlementExists: 'A later settlement exists',
  AlreadyGenerated: 'Already generated',
  NotDraft: 'Already submitted / approved',
  Conflict: 'Changed by another admin',
};

/** −₫150.000 (dấu trừ trước ký hiệu tiền, không phải ₫-150.000). */
export const formatSignedVnd = (value: number): string => (value < 0 ? `−${formatVnd(-value)}` : formatVnd(value));

/** "02/11/2026 – 08/11/2026" từ DateOnly YYYY-MM-DD (ngày ICT, không đổi múi giờ). */
export const formatDay = (day: string | null | undefined): string => {
  if (!day) return EMPTY;
  const [y, m, d] = day.split('-');
  return y && m && d ? `${d}/${m}/${y}` : EMPTY;
};
export const formatPeriod = (start: string, end: string) => `${formatDay(start)} – ${formatDay(end)}`;

/** Các bước công thức NetPayout (sign = cách cộng vào thực chi). */
export interface FormulaStep {
  key: keyof Omit<SettlementFormulaDto, 'expression' | 'netPayout'>;
  label: string;
  sign: '+' | '−';
  hint: string;
}

export const FORMULA_STEPS: FormulaStep[] = [
  { key: 'gross', label: 'Gross', sign: '+', hint: 'All payments recognised in the period (booking ended / paid at the gate)' },
  { key: 'refund', label: 'Refunds', sign: '−', hint: 'Refunds completed before the cut-off' },
  { key: 'ownerCollected', label: 'Collected by owner', sign: '−', hint: 'Cash / VietQR paid straight to the owner (after refunds)' },
  { key: 'commission', label: 'Commission', sign: '−', hint: 'Platform commission, reduced for refunds' },
  { key: 'held', label: 'Held (disputes)', sign: '−', hint: 'New dispute holds minus released holds' },
  { key: 'adjustment', label: 'Adjustments', sign: '+', hint: 'Approved financial adjustments attached to this period' },
  { key: 'carryOver', label: 'Carry-over', sign: '+', hint: 'Negative net payout carried from the previous period' },
];

/** Thực chi ≤ 0: không chuyển tiền, số âm mang sang kỳ sau. */
export const isPayable = (s: Pick<SettlementSummaryDto, 'netPayout'>) => s.netPayout > 0;

// ===== Liên kết =====

/** Chủ bãi → chi tiết hồ sơ đối tác (AdminService trả partnerApplicationId); chưa có hồ sơ → không có link. */
export const ownerLink = (account: OwnerPayoutAccountDto | undefined) =>
  account ? `/admin/operations/partner-onboarding/${account.partnerApplicationId}` : null;

export const lotLink = (parkingLotId: number) => `/admin/operations/parking-lots/${parkingLotId}`;

/** Trang Commission (US-079) cùng khoảng ngày của kỳ (to bao gồm, theo ICT). */
export const commissionLink = (s: Pick<SettlementSummaryDto, 'periodStart' | 'periodEnd'>) =>
  `/admin/finance/commission?period=custom&from=${s.periodStart}&to=${s.periodEnd}`;

export const ownerLabel = (ownerProfileId: number, account: OwnerPayoutAccountDto | undefined) =>
  account?.ownerName ?? `OwnerProfile #${ownerProfileId}`;

export const bankLabel = (account: OwnerPayoutAccountDto | undefined) =>
  account ? `${account.bankName} · ${account.accountNumberMasked}` : null;

// ===== Lỗi thao tác =====

export const CONFLICT_MESSAGE = 'Data has changed since you opened it – the latest version has been reloaded.';

/** 409 = dữ liệu đã đổi / sai trạng thái → báo + tải lại; lỗi khác giữ thông điệp của server. */
export const actionError = (err: unknown): { message: string; conflict: boolean } => {
  if (err instanceof AdminApiError && err.status === 409) return { message: `${CONFLICT_MESSAGE} (${err.message})`, conflict: true };
  return { message: err instanceof Error ? err.message : String(err), conflict: false };
};

/** Ngày ICT YYYY-MM-DD → "Week of 02/11/2026"-style label cho select kỳ. */
export const periodOptionLabel = (p: { periodStart: string; periodEnd: string; cycle: string; hasEnded?: boolean }) =>
  `${formatPeriod(p.periodStart, p.periodEnd)} · ${p.cycle.toLowerCase()}${p.hasEnded === false ? ' (running)' : ''}`;

export const formatApproved = (value: string | null) => (value ? formatDate(value) : EMPTY);
