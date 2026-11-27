import type { ShiftView } from '@/api/shiftApi';

export const isPendingHandover = (s: ShiftView) =>
  s.isCashHolder && s.handoverAcceptedAtUtc === null && (s.status === 'HandoverPending' || s.status === 'AutoClosed');

/**
 * R19: số phải có = tiền đầu ca + tiền thu trong ca. end = người giao đếm − số phải có;
 * handover = người nhận đếm − người giao đếm (ca tự đóng không có số của người giao → so với số phải có). null = chưa đủ dữ liệu.
 */
export function cashGaps(s: ShiftView, openingCash: number | undefined) {
  const expected = s.cashSystem !== null && openingCash !== undefined ? openingCash + s.cashSystem : null;
  const end = s.cashCounted !== null && expected !== null ? s.cashCounted - expected : null;
  const base = s.cashCounted ?? expected;
  const handover = s.cashReceived !== null && base !== null ? s.cashReceived - base : null;
  return { expected, end, handover };
}
