import React, { useState } from 'react';
import { adminSettlementsApi } from '@/api/adminSettlementsApi';
import type {
  OwnerPayoutAccountDto,
  SettlementGenerationResultDto,
  SettlementPeriodDto,
  SettlementSummaryDto,
} from '@/types/adminSettlements';
import { AdminDialog } from '../../../components/AdminDialog';
import { DetailRow } from '../../../components/DetailRow';
import { DateTimeField, SelectField, TextField } from '../../../components/FormFields';
import { formatVnd, localInputToUtcIso, nowLocalInputValue } from '../../../utils/format';
import { actionError, bankLabel, formatPeriod, ownerLabel, periodOptionLabel } from './settlementFormat';

// ===================== Generate =====================

/**
 * Admin sinh kỳ (hoặc tính lại kỳ Draft) cho một kỳ đã kết thúc – mọi chủ bãi có phát sinh hoặc một chủ bãi.
 * Khoản giữ tranh chấp được nạp bằng JWT Admin nên kỳ không còn bị chặn "HoldsNotLoaded".
 */
export const GenerateSettlementsDialog: React.FC<{
  periods: SettlementPeriodDto[];
  onClose: () => void;
  onDone: (result: SettlementGenerationResultDto) => void;
}> = ({ periods, onClose, onDone }) => {
  const ended = periods.filter((p) => p.hasEnded);
  const [periodStart, setPeriodStart] = useState(ended[0]?.periodStart ?? '');
  const [owner, setOwner] = useState('');
  const [submitted, setSubmitted] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [apiError, setApiError] = useState<string | null>(null);

  const ownerId = owner.trim() === '' ? null : Number(owner);
  const ownerError = ownerId !== null && (!Number.isInteger(ownerId) || ownerId <= 0) ? 'Owner profile ID must be a positive whole number.' : null;

  const confirm = async () => {
    setSubmitted(true);
    if (ownerError) return;
    setSubmitting(true);
    setApiError(null);
    try {
      onDone(await adminSettlementsApi.generate(periodStart || null, ownerId));
    } catch (err: unknown) {
      setApiError(actionError(err).message);
      setSubmitting(false);
    }
  };

  return (
    <AdminDialog
      open
      title="Generate settlements"
      description="Creates Draft settlements for an ended period, or recalculates existing Drafts. Dispute holds are loaded with your session."
      onClose={onClose}
      onConfirm={confirm}
      confirmLabel="Generate"
      submitting={submitting}
      error={apiError}
      footerNote="Submitted / approved settlements are never changed."
    >
      <SelectField
        label="Period"
        value={periodStart}
        onChange={setPeriodStart}
        options={ended.map((p) => ({ value: p.periodStart, label: periodOptionLabel(p) }))}
        placeholder={ended.length === 0 ? 'Latest ended period' : undefined}
        helper={ended.length === 0 ? 'The calendar is created on the first run – leave empty to use the latest ended period.' : undefined}
      />
      <TextField
        label="Owner profile ID (optional)"
        type="number"
        value={owner}
        onChange={setOwner}
        placeholder="All owners with activity"
        error={submitted ? ownerError : null}
      />
    </AdminDialog>
  );
};

// ===================== Mark paid =====================

/** Approved → Paid sau khi chuyển khoản: mã giao dịch ngân hàng bắt buộc, ngày chuyển không ở tương lai. */
export const MarkPaidDialog: React.FC<{
  settlement: SettlementSummaryDto;
  account: OwnerPayoutAccountDto | undefined;
  onClose: () => void;
  onDone: (updated: SettlementSummaryDto) => void;
  onConflict: () => void;
}> = ({ settlement, account, onClose, onDone, onConflict }) => {
  const [reference, setReference] = useState('');
  const [paidAt, setPaidAt] = useState(nowLocalInputValue);
  // Mốc so sánh "tương lai" chốt lúc mở dialog (render phải thuần – không gọi Date.now mỗi lần render); server vẫn kiểm tra lại.
  const [openedAt] = useState(() => Date.now());
  const [submitted, setSubmitted] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [apiError, setApiError] = useState<string | null>(null);

  const paidAtIso = localInputToUtcIso(paidAt);
  const referenceError = reference.trim() === '' ? 'A payout reference is required.' : reference.trim().length > 100 ? 'Max 100 characters.' : null;
  const dateError = !paidAtIso ? 'Transfer date is required.' : new Date(paidAtIso).getTime() > openedAt + 5 * 60_000 ? 'Transfer date cannot be in the future.' : null;

  const confirm = async () => {
    setSubmitted(true);
    if (referenceError || dateError || !paidAtIso) return;
    setSubmitting(true);
    setApiError(null);
    try {
      onDone(await adminSettlementsApi.markPaid(settlement.id, settlement.rowVersion, reference.trim(), paidAtIso));
    } catch (err: unknown) {
      const e = actionError(err);
      setApiError(e.message);
      setSubmitting(false);
      if (e.conflict) onConflict();
    }
  };

  return (
    <AdminDialog
      open
      title={`Mark ${settlement.code} as paid`}
      description="Record the bank transfer of the net payout. This cannot be undone."
      onClose={onClose}
      onConfirm={confirm}
      confirmLabel="Mark as paid"
      submitting={submitting}
      error={apiError}
    >
      <div className="rounded-[2px] border border-(--admin-border-default)">
        <DetailRow label="Owner">{ownerLabel(settlement.ownerProfileId, account)}</DetailRow>
        <DetailRow label="Period">{formatPeriod(settlement.periodStart, settlement.periodEnd)}</DetailRow>
        <DetailRow label="Bank account">{bankLabel(account) ?? 'No payout account on file'}</DetailRow>
        <DetailRow label="Net payout">
          <strong>{formatVnd(settlement.netPayout)}</strong>
        </DetailRow>
      </div>
      <TextField
        label="Payout reference"
        required
        value={reference}
        onChange={setReference}
        maxLength={100}
        placeholder="Bank transaction ID, e.g. FT26314XYZ"
        error={submitted ? referenceError : null}
      />
      <DateTimeField label="Transferred at" required value={paidAt} onChange={setPaidAt} helper="Your local time" error={submitted ? dateError : null} />
    </AdminDialog>
  );
};
