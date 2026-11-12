import type { LotCapacityConfigDto } from '@/api/capacityApi';
import type { ProblemDetails } from '@/types/common';
import {
  CAPACITY_ERROR_CODES,
  validateCapacityConfig,
  type CapacityFieldError,
  type CapacityFieldName,
  type CapacityTextValues,
} from '@/features/capacity/capacityRules';

/**
 * P1-10 (US-066): chữ tiếng Anh + logic thuần cho trang Capacity.
 * Quy tắc tính dùng chung `validateCapacityConfig` (mirror validator của GateService); câu hiển thị tra theo `code`
 * vì server và capacityRules trả message tiếng Việt cố định (audit §7.8).
 */

/** Mã server trả khi tổng số chỗ mới < số xe đang đỗ (UpdateLotCapacityConfigUseCase luật 7a). */
export const CAPACITY_BELOW_OCCUPANCY = 'CAPACITY_BELOW_CURRENT_OCCUPANCY';

export const FIELD_LABELS: Record<CapacityFieldName, { label: string; helper: string }> = {
  capacity: { label: 'Total spots', helper: 'Every spot at this lot, including shared ones. At least 1.' },
  transientBuffer: { label: 'Max walk-in spots', helper: 'Vehicles that drive in without a monthly pass. Enter 0 to stop walk-ins.' },
  subscriberQuota: { label: 'Max monthly-pass spots', helper: 'Spots held for active monthly passes. Enter 0 if the lot sells none.' },
};

const toInt = (text: string) => Number.parseInt(text.trim(), 10);

/** Câu tiếng Anh cho một lỗi theo mã; mã lạ → giữ nguyên câu của server (quyết định 2: không dịch ProblemDetails). */
export const fieldErrorText = (error: CapacityFieldError, values: CapacityTextValues, parked: number): string => {
  const c = toInt(values.capacity);
  const b = toInt(values.transientBuffer);
  const q = toInt(values.subscriberQuota);
  switch (error.code) {
    case CAPACITY_ERROR_CODES.capacityRequired:
      return 'Enter the total number of spots.';
    case CAPACITY_ERROR_CODES.capacityInvalid:
      return 'Total spots must be a whole number of 1 or more.';
    case CAPACITY_ERROR_CODES.transientRequired:
      return 'Enter the walk-in limit. Enter 0 to stop walk-ins.';
    case CAPACITY_ERROR_CODES.subscriberRequired:
      return 'Enter the monthly-pass limit. Enter 0 if the lot sells none.';
    case CAPACITY_ERROR_CODES.transientInvalid:
    case CAPACITY_ERROR_CODES.subscriberInvalid:
      return 'Use a whole number of 0 or more.';
    case CAPACITY_ERROR_CODES.transientExceeds:
      return `Walk-in spots (${b}) can't be more than total spots (${c}).`;
    case CAPACITY_ERROR_CODES.subscriberExceeds:
      return `Monthly-pass spots (${q}) can't be more than total spots (${c}).`;
    case CAPACITY_ERROR_CODES.totalExceeds:
      return `Walk-in (${b}) plus monthly-pass (${q}) spots come to ${b + q}, more than the ${c} total spots. Remove at least ${b + q - c}.`;
    case CAPACITY_BELOW_OCCUPANCY:
      return `${parked} ${parked === 1 ? 'vehicle is' : 'vehicles are'} parked right now. Total spots can't go below ${parked}.`;
    default:
      return error.message;
  }
};

export interface FormCheck {
  fieldErrors: Partial<Record<CapacityFieldName, CapacityFieldError>>;
  summary: CapacityFieldError | null;
  /** Chỗ dùng chung khi đủ 3 ô hợp lệ và không vượt tổng; null → không hiển thị. */
  shared: number | null;
  parsed: { capacity: number; transientBuffer: number; subscriberQuota: number } | null;
  isValid: boolean;
}

/** Validate giống server: luật của capacityRules + luật 7a (tổng số chỗ < số xe đang đỗ là lỗi chặn lưu). */
export const checkForm = (values: CapacityTextValues, parked: number): FormCheck => {
  const outcome = validateCapacityConfig(values);
  const fieldErrors = { ...outcome.fieldErrors };
  let summary = outcome.summary;
  if (!fieldErrors.capacity && outcome.parsed && outcome.parsed.capacity < parked) {
    fieldErrors.capacity = { code: CAPACITY_BELOW_OCCUPANCY, message: '' };
    summary = null; // server: lỗi từng ô ưu tiên, ẩn lỗi tổng hợp
  }
  const isValid = Object.keys(fieldErrors).length === 0 && summary === null;
  return {
    fieldErrors,
    summary,
    shared: outcome.shared.state === 'ok' ? outcome.shared.value : null,
    parsed: outcome.parsed,
    isValid,
  };
};

/**
 * Cảnh báo không chặn lưu (server cho lưu kèm warnings tiếng Việt – luật 7b/7c): FE tự sinh câu tiếng Anh cùng điều kiện.
 */
export const capacityWarnings = (parsed: FormCheck['parsed'], live: LotCapacityConfigDto | null): string[] => {
  if (!parsed || !live) return [];
  const warnings: string[] = [];
  if (parsed.transientBuffer < live.transientParked) {
    warnings.push(
      `${live.transientParked} walk-in vehicles are parked now, more than the new limit of ${parsed.transientBuffer}. ` +
        'New walk-ins will be turned away until enough leave; parked vehicles are not affected.'
    );
  }
  if (parsed.subscriberQuota < live.activeSubscribers) {
    warnings.push(
      `${live.activeSubscribers} monthly passes are active, more than the new limit of ${parsed.subscriberQuota}. ` +
        'No new passes can be sold until some expire; current passes stay valid.'
    );
  }
  return warnings;
};

export interface AllocationPart {
  key: 'walkIn' | 'monthlyPass' | 'shared';
  label: string;
  spots: number;
  percent: number;
}

/** Phân bổ tổng số chỗ: walk-in / monthly pass / shared (C − B − Q), % theo tổng. */
export const allocation = (c: number, b: number, q: number): AllocationPart[] => {
  if (c <= 0) return [];
  const shared = Math.max(0, c - b - q);
  const pct = (n: number) => Math.round((n / c) * 1000) / 10;
  return [
    { key: 'walkIn', label: 'Walk-in', spots: b, percent: pct(b) },
    { key: 'monthlyPass', label: 'Monthly pass', spots: q, percent: pct(q) },
    { key: 'shared', label: 'Shared (first come, first served)', spots: shared, percent: pct(shared) },
  ];
};

export const valuesFrom = (dto: LotCapacityConfigDto | null): CapacityTextValues =>
  dto?.configured
    ? { capacity: String(dto.capacity), transientBuffer: String(dto.transientBuffer), subscriberQuota: String(dto.subscriberQuota) }
    : { capacity: '', transientBuffer: '', subscriberQuota: '' };

export interface CapacityApiFailure {
  status: number;
  message: string;
  problem: ProblemDetails | null;
  fieldErrors: Partial<Record<CapacityFieldName, CapacityFieldError>>;
  summary: CapacityFieldError | null;
}

/**
 * Lỗi axios của capacityApi → dạng dùng trong trang (status, ProblemDetails, lỗi theo ô).
 * 400 của GateService: extensions `errors: { field: [{ code, message }] }` + `summary` (LotCapacityErrorHandlingMiddleware).
 */
export const toCapacityFailure = (err: unknown): CapacityApiFailure => {
  const response = (err as { response?: { status?: number; data?: unknown } }).response;
  const status = response?.status ?? 0;
  const data = (response?.data ?? null) as (ProblemDetails & { errors?: unknown; summary?: CapacityFieldError }) | null;
  const fieldErrors: Partial<Record<CapacityFieldName, CapacityFieldError>> = {};
  const raw = data && typeof data.errors === 'object' && data.errors !== null ? (data.errors as Record<string, unknown>) : {};
  for (const [field, value] of Object.entries(raw)) {
    const first = (Array.isArray(value) ? value[0] : value) as CapacityFieldError | undefined;
    if (first && typeof first === 'object' && 'code' in first && (field === 'capacity' || field === 'transientBuffer' || field === 'subscriberQuota')) {
      fieldErrors[field] = first;
    }
  }
  const message =
    data?.detail ?? data?.title ?? (err instanceof Error && status === 0 ? err.message : status ? `HTTP ${status}` : 'Network error');
  return { status, message, problem: data && (data.title || data.detail) ? data : null, fieldErrors, summary: data?.summary ?? null };
};
