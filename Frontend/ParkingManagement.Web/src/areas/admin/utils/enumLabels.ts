/**
 * Nhãn + màu trạng thái lấy từ giá trị enum SharedKernel (src/types/adminEnums.ts).
 * Nhãn = humanise tên enum (PendingApproval → "Pending approval"), trừ vài từ viết tắt.
 * Tone theo mô tả component Shell/Status trên Figma (dot + text, không pill).
 */
export type StatusTone = 'success' | 'warning' | 'danger' | 'info' | 'neutral';

const SPECIAL_LABELS: Record<string, string> = {
  LotOwner: 'Lot owner',
  NoShow: 'No-show',
  InApp: 'In-app',
  Sms: 'SMS',
  Vnpay: 'VNPAY',
  Momo: 'MoMo',
  VietQr: 'VietQR',
  PmsApi: 'PMS API',
  EdgeIot: 'Edge IoT',
  AnprCamera: 'ANPR camera',
  QrScanner: 'QR scanner',
  Qr: 'QR',
  Ocr: 'OCR',
  Suv: 'SUV',
};

/** PendingApproval → "Pending approval"; giữ nguyên chuỗi đã có khoảng trắng. */
export const humanizeEnum = (value: string | null | undefined): string => {
  if (!value) return '—';
  if (SPECIAL_LABELS[value]) return SPECIAL_LABELS[value];
  if (/\s/.test(value) || /Service$/.test(value)) return value;
  const words = value.replace(/([a-z0-9])([A-Z])/g, '$1 $2').replace(/([A-Z]+)([A-Z][a-z])/g, '$1 $2');
  return words.charAt(0).toUpperCase() + words.slice(1).toLowerCase();
};

const SUCCESS = new Set([
  'Active', 'Approved', 'Activated', 'Synced', 'Paid', 'Succeeded', 'Completed', 'Verified', 'Online', 'Effective',
  'Resolved', 'Sent', 'Redeemed', 'Closed', 'Confirmed', 'Visible', 'Published', 'Signed',
]);
const WARNING = new Set([
  'PendingVerification', 'NeedsMoreInfo', 'Scheduled', 'Flagged', 'PendingApproval', 'PendingReview',
  'PendingPayment', 'PendingOwnerApproval', 'Pending', 'AwaitingOwner', 'Escalated', 'Warning', 'Processing', 'Maintenance',
  'Investigating', 'TemporarySuspension', 'RankingPenalty', 'InProgress', 'Leaving', 'Requested', 'Issued', 'Abnormal',
  'PartiallyRefunded',
]);
const DANGER = new Set([
  'Rejected', 'Locked', 'Banned', 'Suspended', 'Failed', 'Cancelled', 'CancelledNoRefund', 'NoShow', 'Expired', 'Offline',
  'Critical', 'PermanentBan', 'Terminated', 'Revoked', 'Dismissed',
]);
const INFO = new Set([
  'Submitted', 'UnderReview', 'CheckedIn', 'Parking', 'CheckedOut', 'AccountIssued', 'Open', 'OwnerResponded', 'Refunded', 'Current',
  'Info',
]);

export const statusTone = (value: string | null | undefined): StatusTone => {
  if (!value) return 'neutral';
  if (SUCCESS.has(value)) return 'success';
  if (WARNING.has(value)) return 'warning';
  if (DANGER.has(value)) return 'danger';
  if (INFO.has(value)) return 'info';
  return 'neutral';
};
