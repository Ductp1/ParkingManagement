/**
 * US-066 – Validate thuần phía client, MIRROR đúng luật + giọng văn + mã lỗi của backend:
 * C > 0; B >= 0; Q >= 0; B <= C; Q <= C; B + Q <= C.
 * Input là TEXT của ô nhập (phân biệt TRỐNG / SAI / HỢP LỆ). Mỗi ô chỉ giữ MỘT lỗi
 * theo ưu tiên: thiếu → sai định dạng → vượt tổng. Lỗi tổng hợp (B + Q > C) trả riêng,
 * không gắn vào ô nào — form hiển thị trong khối riêng phía trên nút Lưu.
 */
export interface CapacityTextValues {
  capacity: string;
  transientBuffer: string;
  subscriberQuota: string;
}

export interface CapacityFieldError {
  code: string;
  message: string;
}

export const CAPACITY_ERROR_CODES = {
  capacityRequired: 'CAPACITY_REQUIRED',
  capacityInvalid: 'CAPACITY_INVALID',
  transientRequired: 'TRANSIENT_REQUIRED',
  transientInvalid: 'TRANSIENT_INVALID',
  transientExceeds: 'TRANSIENT_EXCEEDS_CAPACITY',
  subscriberRequired: 'SUBSCRIBER_REQUIRED',
  subscriberInvalid: 'SUBSCRIBER_INVALID',
  subscriberExceeds: 'SUBSCRIBER_EXCEEDS_CAPACITY',
  totalExceeds: 'TOTAL_EXCEEDS_CAPACITY',
} as const;

export type CapacityFieldName = keyof CapacityTextValues;

export type SharedZoneState = 'incomplete' | 'invalid' | 'ok' | 'overflow';

export interface SharedZone {
  /** incomplete/invalid → hiện "—" kèm gợi ý; ok → hiện số; overflow → cảnh báo đỏ thiếu chỗ. */
  state: SharedZoneState;
  value: number | null;
  deficit: number | null;
}

export interface CapacityValidationOutcome {
  fieldErrors: Partial<Record<CapacityFieldName, CapacityFieldError>>;
  summary: CapacityFieldError | null;
  shared: SharedZone;
  /** true khi không còn lỗi nào ở bất kỳ đâu. */
  isValid: boolean;
  parsed: { capacity: number; transientBuffer: number; subscriberQuota: number } | null;
}

type ParseResult = { kind: 'empty' } | { kind: 'invalid' } | { kind: 'ok'; value: number };

function parseNonNegativeInt(text: string): ParseResult {
  const trimmed = text.trim();
  if (trimmed === '') return { kind: 'empty' };
  if (!/^\d+$/.test(trimmed)) return { kind: 'invalid' }; // chữ, dấu phẩy, số âm, số lẻ…
  return { kind: 'ok', value: Number.parseInt(trimmed, 10) };
}

export function validateCapacityConfig(v: CapacityTextValues): CapacityValidationOutcome
{
  const parsedCapacity = parseNonNegativeInt(v.capacity);
  const parsedBuffer = parseNonNegativeInt(v.transientBuffer);
  const parsedQuota = parseNonNegativeInt(v.subscriberQuota);

  const fieldErrors: Partial<Record<CapacityFieldName, CapacityFieldError>> = {};

  // 1) Thiếu (ô trống) → REQUIRED.
  if (parsedCapacity.kind === 'empty')
    fieldErrors.capacity = { code: CAPACITY_ERROR_CODES.capacityRequired, message: 'Hãy nhập tổng số chỗ của bãi (ví dụ: 100).' };
  if (parsedBuffer.kind === 'empty')
    fieldErrors.transientBuffer = { code: CAPACITY_ERROR_CODES.transientRequired, message: 'Hãy nhập số chỗ cho xe vãng lai. Nhập 0 nếu không nhận.' };
  if (parsedQuota.kind === 'empty')
    fieldErrors.subscriberQuota = { code: CAPACITY_ERROR_CODES.subscriberRequired, message: 'Hãy nhập số chỗ cho khách đăng ký. Nhập 0 nếu không nhận.' };

  // 2) Sai giá trị (0/âm/số lẻ) → INVALID.
  if (parsedCapacity.kind === 'invalid' || (parsedCapacity.kind === 'ok' && parsedCapacity.value <= 0))
    fieldErrors.capacity = { code: CAPACITY_ERROR_CODES.capacityInvalid, message: 'Tổng số chỗ phải là số nguyên từ 1 trở lên.' };
  if (parsedBuffer.kind === 'invalid')
    fieldErrors.transientBuffer = { code: CAPACITY_ERROR_CODES.transientInvalid, message: 'Số chỗ không được âm hoặc có dấu phẩy. Nhập 0 nếu không nhận.' };
  if (parsedQuota.kind === 'invalid')
    fieldErrors.subscriberQuota = { code: CAPACITY_ERROR_CODES.subscriberInvalid, message: 'Số chỗ không được âm hoặc có dấu phẩy. Nhập 0 nếu không nhận.' };

  const allFieldsValid = Object.keys(fieldErrors).length === 0
    && parsedCapacity.kind === 'ok' && parsedBuffer.kind === 'ok' && parsedQuota.kind === 'ok';

  // 3) Vượt tổng → gắn vào ô tương ứng (chỉ khi giá trị đang hợp lệ).
  let parsedValues: CapacityValidationOutcome['parsed'] = null;
  let summary: CapacityFieldError | null = null;
  let shared: SharedZone;

  if (allFieldsValid)
  {
    const c = parsedCapacity.value;
    const b = parsedBuffer.value;
    const q = parsedQuota.value;
    parsedValues = { capacity: c, transientBuffer: b, subscriberQuota: q };

    if (b > c)
      fieldErrors.transientBuffer = { code: CAPACITY_ERROR_CODES.transientExceeds, message: `Chỗ cho xe vãng lai (${b}) nhiều hơn tổng số chỗ (${c}). Hãy giảm xuống tối đa ${c}.` };
    if (q > c)
      fieldErrors.subscriberQuota = { code: CAPACITY_ERROR_CODES.subscriberExceeds, message: `Chỗ cho khách đăng ký (${q}) nhiều hơn tổng số chỗ (${c}). Hãy giảm xuống tối đa ${c}.` };

    const sum = b + q;
    if (sum > c)
    {
      summary = { code: CAPACITY_ERROR_CODES.totalExceeds, message: `Vãng lai (${b}) + đăng ký (${q}) = ${sum}, vượt tổng số chỗ ${c}. Hãy giảm bớt ít nhất ${sum - c} chỗ.` };
      shared = { state: 'overflow', value: null, deficit: sum - c };
    }
    else
    {
      shared = { state: 'ok', value: c - b - q, deficit: null };
    }
  }
  else
  {
    const anyInvalid = [parsedCapacity, parsedBuffer, parsedQuota].some((p) => p.kind === 'invalid');
    shared = { state: anyInvalid ? 'invalid' : 'incomplete', value: null, deficit: null };
  }

  return { fieldErrors, summary, shared, isValid: Object.keys(fieldErrors).length === 0 && summary === null, parsed: parsedValues };
}