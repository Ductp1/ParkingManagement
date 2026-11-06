import { SanctionLevel } from '@/types/adminEnums';
import type { SanctionSummaryDto } from '@/types/adminSanctions';
import type { StatusTone } from '../../../utils/enumLabels';

export const SANCTIONS_BASE = '/admin/support/sanctions';

/** Giới hạn khớp server (SanctionRules / Sanction). */
export const REASON_MAX = 1000;
export const EVIDENCE_MAX = 2000;
export const RANKING_PENALTY_MAX_DAYS = 30;
export const MAX_LOTS_PER_REQUEST = 50;
/** numeric(18,2) của server: phần nguyên tối đa 16 chữ số. */
const PENALTY_LIMIT = 1e16;
const DAY_MS = 24 * 60 * 60 * 1000;

/** Nhãn cấp – cấp 3 không chọn bãi là khóa tài khoản có thời hạn (luồng US-096), không phải tạm ngừng bãi. */
export const levelLabel = (level: string, parkingLotId: number | null = null): string => {
  switch (level) {
    case SanctionLevel.Warning:
      return 'L1 · Warning';
    case SanctionLevel.RankingPenalty:
      return 'L2 · Ranking penalty';
    case SanctionLevel.TemporarySuspension:
      return parkingLotId === null ? 'L3 · Temporary account lock' : 'L3 · Listing suspension';
    case SanctionLevel.PermanentBan:
      return 'L4 · Termination';
    default:
      return level;
  }
};

/** Màu badge theo mức nặng. */
export const levelTone = (level: string): StatusTone => {
  switch (level) {
    case SanctionLevel.Warning:
      return 'info';
    case SanctionLevel.RankingPenalty:
      return 'warning';
    default:
      return 'danger';
  }
};

export const LEVEL_OPTIONS = [
  { value: SanctionLevel.Warning, label: 'L1 · Warning (correction deadline)' },
  { value: SanctionLevel.RankingPenalty, label: 'L2 · Ranking penalty (max 30 days)' },
  { value: SanctionLevel.TemporarySuspension, label: 'L3 · Listing suspension / temporary account lock' },
  { value: SanctionLevel.PermanentBan, label: 'L4 · Termination (permanent ban, whole owner)' },
];

export const scopeText = (s: Pick<SanctionSummaryDto, 'parkingLotId'>): string =>
  s.parkingLotId === null ? 'Whole owner' : `Lot #${s.parkingLotId}`;

/** Giá trị datetime-local hiểu theo giờ ICT (UTC+7, không DST) → ISO UTC. */
export const ictInputToUtcIso = (value: string): string | null => {
  if (!/^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}$/.test(value)) return null;
  const d = new Date(`${value}:00+07:00`);
  return Number.isNaN(d.getTime()) ? null : d.toISOString();
};

/** Mốc UTC → giá trị datetime-local theo giờ ICT. */
export const utcToIctInput = (ms: number): string => new Date(ms + 7 * 60 * 60 * 1000).toISOString().slice(0, 16);

/** Ngày (YYYY-MM-DD) theo giờ ICT → ISO UTC 00:00 ICT; addDays = 1 cho mốc "to" (không bao gồm). */
export const ictDayStartUtc = (day: string, addDays = 0): string | null => {
  if (!/^\d{4}-\d{2}-\d{2}$/.test(day)) return null;
  const d = new Date(`${day}T00:00:00+07:00`);
  if (Number.isNaN(d.getTime())) return null;
  d.setUTCDate(d.getUTCDate() + addDays);
  return d.toISOString();
};

export const positiveInt = (value: string): number | null => (/^[1-9]\d*$/.test(value) ? Number(value) : null);

export interface IssueForm {
  ownerProfileId: number | null;
  ownerUserId: number | null;
  level: string;
  lotIds: number[];
  reason: string;
  /** datetime-local theo giờ ICT. */
  endsAt: string;
  correctionDue: string;
  penalty: string;
  evidence: string;
  /** Cấp 4: gõ lại mã chủ bãi. */
  confirmOwner: string;
}

export type IssueFormErrors = Partial<Record<'owner' | 'level' | 'lots' | 'reason' | 'endsAt' | 'correctionDue' | 'penalty' | 'confirmOwner', string>>;

/** Trường hiển thị theo cấp (cùng quy tắc với SanctionRules.ValidatePeriod). */
export const fieldsForLevel = (level: string) => ({
  correctionDue: level === SanctionLevel.Warning,
  endsAt: level === SanctionLevel.RankingPenalty || level === SanctionLevel.TemporarySuspension,
  lots: level !== SanctionLevel.PermanentBan,
  confirmOwner: level === SanctionLevel.PermanentBan,
});

/** Kiểm tra phía client khớp server; nowMs truyền vào để test cố định thời gian. */
export const validateIssueForm = (form: IssueForm, nowMs: number): IssueFormErrors => {
  const errors: IssueFormErrors = {};
  const show = fieldsForLevel(form.level);
  if (form.ownerProfileId === null || form.ownerUserId === null) errors.owner = 'Select an owner.';
  if (!form.level) errors.level = 'Select a level.';
  if (form.lotIds.length > MAX_LOTS_PER_REQUEST) errors.lots = `At most ${MAX_LOTS_PER_REQUEST} lots per request.`;

  const reason = form.reason.trim();
  if (reason === '') errors.reason = 'A reason is required.';
  else if (reason.length > REASON_MAX) errors.reason = `At most ${REASON_MAX.toLocaleString('en-US')} characters.`;

  if (show.correctionDue) {
    const due = ictInputToUtcIso(form.correctionDue);
    if (!due) errors.correctionDue = 'A correction deadline is required.';
    else if (new Date(due).getTime() <= nowMs) errors.correctionDue = 'Must be after the current time.';
  }
  if (show.endsAt) {
    const ends = ictInputToUtcIso(form.endsAt);
    if (!ends) errors.endsAt = 'An end time is required.';
    else if (new Date(ends).getTime() <= nowMs) errors.endsAt = 'Must be after the current time.';
    else if (form.level === SanctionLevel.RankingPenalty && new Date(ends).getTime() > nowMs + RANKING_PENALTY_MAX_DAYS * DAY_MS)
      errors.endsAt = `A ranking penalty lasts at most ${RANKING_PENALTY_MAX_DAYS} days.`;
  }

  if (form.penalty.trim() !== '') {
    const text = form.penalty.trim();
    const amount = Number(text);
    if (!/^\d+(\.\d{1,2})?$/.test(text) || !Number.isFinite(amount)) errors.penalty = 'Enter a non-negative amount (max 2 decimals).';
    else if (amount >= PENALTY_LIMIT) errors.penalty = 'Amount is too large.';
  }

  if (show.confirmOwner && form.ownerProfileId !== null && form.confirmOwner.trim() !== String(form.ownerProfileId))
    errors.confirmOwner = `Type ${form.ownerProfileId} to confirm the permanent ban.`;
  return errors;
};

/** Body POST /admin/sanctions từ form đã hợp lệ. */
export const toIssueRequest = (form: IssueForm) => {
  const show = fieldsForLevel(form.level);
  const evidence = form.evidence.trim();
  return {
    ownerProfileId: form.ownerProfileId!,
    ownerUserId: form.ownerUserId!,
    level: form.level as SanctionLevel,
    parkingLotIds: show.lots ? form.lotIds : [],
    reason: form.reason.trim(),
    evidenceJson: evidence === '' ? null : JSON.stringify({ note: evidence }),
    endsAtUtc: show.endsAt ? ictInputToUtcIso(form.endsAt) : null,
    correctionDueAtUtc: show.correctionDue ? ictInputToUtcIso(form.correctionDue) : null,
    penaltyAmount: form.penalty.trim() === '' ? null : Number(form.penalty.trim()),
  };
};

export const SYNC_TARGETS = [
  { key: 'userServiceSyncStatus', label: 'UserService', short: 'User' },
  { key: 'parkingSyncStatus', label: 'ParkingService', short: 'Parking' },
  { key: 'notificationSyncStatus', label: 'Notification', short: 'Notify' },
] as const;

export const hasSyncProblem = (s: SanctionSummaryDto) =>
  SYNC_TARGETS.some((t) => s[t.key] === 'Pending' || s[t.key] === 'Failed');

/** Nhãn hành động audit trên timeline. */
export const TIMELINE_LABEL: Record<string, string> = {
  'Sanction.Issued': 'Sanction issued',
  'Sanction.Revoked': 'Sanction revoked',
  'Sanction.Expired': 'Sanction expired',
  'Sanction.SyncRetried': 'Sync retried',
  'Owner.Locked': 'Owner account locked',
  'Owner.Unlocked': 'Owner account unlocked',
};
