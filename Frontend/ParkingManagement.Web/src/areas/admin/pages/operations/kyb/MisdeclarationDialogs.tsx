import React, { useState } from 'react';
import { Link } from 'react-router-dom';
import { adminKybApi } from '@/api/adminKybApi';
import {
  MisdeclarationCaseStatus,
  MisdeclarationCategory,
  MisdeclarationSeverity,
  type LotMisdeclarationCaseDto,
  type LotMisdeclarationDto,
  type OwnerBanResultDto,
} from '@/types/adminKyb';
import { ActionDialog } from '../../../components/ActionDialog';
import { AdminDialog, DialogSummary } from '../../../components/AdminDialog';
import { Banner } from '../../../components/Banner';
import { SelectField, TextAreaField, TextField, type SelectOption } from '../../../components/FormFields';
import { humanizeEnum } from '../../../utils/enumLabels';
import { formatDateTimeIct } from '../../../utils/format';
import { ownerLabel } from './kybFormat';

export type MisdeclarationDialogType = 'report' | 'escalate' | 'resolve' | 'ban';

const MIN_DAYS = 1;
const MAX_DAYS = 30;
const USERS_PATH = '/admin/operations/users';

const SEVERITY_OPTIONS: SelectOption[] = [
  { value: MisdeclarationSeverity.Minor, label: 'Minor – warn and set a fix deadline' },
  { value: MisdeclarationSeverity.Suspected, label: 'Suspected – suspend lot, 24 h to explain' },
];

const CATEGORY_OPTIONS: SelectOption[] = Object.values(MisdeclarationCategory).map((c) => ({ value: c, label: humanizeEnum(c) }));

/** US-056: booking đã xác nhận không bị hủy tự động khi bãi bị tạm khóa / cấm (BookingService xử lý sau – handoff). */
const BookingsWarning: React.FC = () => <Banner tone="warning">Confirmed bookings will not be cancelled automatically.</Banner>;

interface ReportDialogProps {
  open: boolean;
  view: LotMisdeclarationDto;
  summary: React.ReactNode;
  onClose: () => void;
  onDone: (title: string, message: string) => void;
}

/** Report misdeclaration: Minor (cảnh báo, hạn sửa 1–30 ngày) hoặc Suspected (tạm khóa, hạn giải trình 24h cố định). */
const ReportDialog: React.FC<ReportDialogProps> = ({ open, view, summary, onClose, onDone }) => {
  const [severity, setSeverity] = useState('');
  const [category, setCategory] = useState('');
  const [finding, setFinding] = useState('');
  const [days, setDays] = useState('');
  const [submitted, setSubmitted] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [apiError, setApiError] = useState<string | null>(null);

  const minor = severity === MisdeclarationSeverity.Minor;
  const suspected = severity === MisdeclarationSeverity.Suspected;
  const daysValue = days.trim() === '' ? null : Number(days);
  const daysError =
    minor && daysValue !== null && !(Number.isInteger(daysValue) && daysValue >= MIN_DAYS && daysValue <= MAX_DAYS)
      ? `Enter a whole number from ${MIN_DAYS} to ${MAX_DAYS}.`
      : null;
  const errors = {
    severity: severity ? null : 'Choose a severity.',
    category: category ? null : 'Choose what was misdeclared.',
    finding: finding.trim() ? null : 'Describe what was misdeclared.',
    days: daysError,
  };

  const reset = () => {
    setSeverity('');
    setCategory('');
    setFinding('');
    setDays('');
    setSubmitted(false);
    setApiError(null);
  };

  const close = () => {
    reset();
    onClose();
  };

  const confirm = async () => {
    setSubmitted(true);
    if (Object.values(errors).some(Boolean)) return;
    setSubmitting(true);
    setApiError(null);
    try {
      const result = await adminKybApi.openMisdeclarationCase(view.parkingLot.id, {
        severity: severity as MisdeclarationSeverity,
        category: category as MisdeclarationCategory,
        finding: finding.trim(),
        correctionDays: minor ? daysValue : null,
      });
      reset();
      onClose();
      const due = formatDateTimeIct(result.openCase?.dueAtUtc);
      onDone(
        suspected ? 'Lot suspended' : 'Warning sent',
        suspected ? `${view.parkingLot.name} is hidden from search; explanation due ${due}.` : `Fix deadline ${due}.`
      );
    } catch (err: unknown) {
      setApiError(err instanceof Error ? err.message : String(err));
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <AdminDialog
      open={open}
      title="Report misdeclaration"
      description="Opens a LotMisdeclarationCase for this Active lot. One open case per lot."
      onClose={close}
      onConfirm={confirm}
      confirmLabel={suspected ? 'Suspend lot' : 'Send warning'}
      destructive={suspected}
      submitting={submitting}
      error={apiError}
    >
      {summary}
      <SelectField
        label="Severity"
        required
        value={severity}
        onChange={setSeverity}
        options={SEVERITY_OPTIONS}
        error={submitted ? errors.severity : null}
      />
      <SelectField
        label="What was misdeclared"
        required
        value={category}
        onChange={setCategory}
        options={CATEGORY_OPTIONS}
        error={submitted ? errors.category : null}
      />
      <TextAreaField
        label="Finding"
        required
        value={finding}
        onChange={setFinding}
        maxLength={1000}
        placeholder="What differs from the declaration, and the evidence."
        helper="Sent to the owner and saved in the audit log."
        error={submitted ? errors.finding : null}
      />
      {minor && (
        <TextField
          label="Fix deadline"
          type="number"
          value={days}
          onChange={setDays}
          suffix="days"
          placeholder={String(view.minorCorrectionDays)}
          helper={`Empty = ${view.minorCorrectionDays} days (default). ${MIN_DAYS}–${MAX_DAYS} days.`}
          error={submitted ? errors.days : null}
        />
      )}
      {suspected && (
        <>
          <Banner tone="danger">
            Lot status Active → Suspended; the lot is hidden from search. The owner has {view.responseWindowHours} h to explain.
          </Banner>
          <BookingsWarning />
        </>
      )}
    </AdminDialog>
  );
};

interface MisdeclarationDialogsProps {
  view: LotMisdeclarationDto;
  open: MisdeclarationDialogType | null;
  onClose: () => void;
  /** Gọi sau khi API thành công: bật toast và tải lại panel + hồ sơ KYB (trạng thái bãi đổi). */
  onDone: (title: string, message: string) => void;
}

const banMessage = (r: OwnerBanResultDto) =>
  `${r.suspendedLotIds.length} lot(s) suspended permanently, ${r.rejectedKybApplicationIds.length} KYB application(s) rejected.`;

/**
 * Dialog của panel Misdeclaration (US-056): Report / Escalate / Resolve / Ban owner.
 * Lỗi 400 / 409 từ ParkingService (thông điệp tiếng Việt) hiện ngay trên dialog.
 */
export const MisdeclarationDialogs: React.FC<MisdeclarationDialogsProps> = ({ view, open, onClose, onDone }) => {
  const lot = view.parkingLot;
  const current: LotMisdeclarationCaseDto | null = view.openCase;
  const summary = <DialogSummary title={`${lot.name} · Lot #${lot.id}`} subtitle={`${ownerLabel(lot.ownerProfileId)} · ${lot.address}`} />;
  const caseSummary = current && (
    <DialogSummary
      title={`Case #${current.id} · ${humanizeEnum(current.category)}`}
      subtitle={`${humanizeEnum(current.status)} · due ${formatDateTimeIct(current.dueAtUtc)}${current.isOverdue ? ' · Overdue' : ''}`}
    />
  );

  return (
    <>
      <ReportDialog open={open === 'report'} view={view} summary={summary} onClose={onClose} onDone={onDone} />

      {current && (
        <>
          <ActionDialog
            open={open === 'escalate'}
            title="Escalate to suspension"
            description={`Case Warned → Suspended; lot Active → Suspended; the owner gets ${view.responseWindowHours} h to explain.`}
            confirmLabel="Suspend lot"
            destructive
            field={{
              label: 'Reason',
              required: true,
              requiredMessage: 'A reason is required.',
              placeholder: 'For example: the fix deadline passed without changes.',
              helper: 'Saved to LotMisdeclarationCases.EscalationNote and the audit log.',
              maxLength: 1000,
            }}
            onClose={onClose}
            onSubmit={async (note) => {
              await adminKybApi.escalateMisdeclarationCase(lot.id, current.id, { note });
              onDone('Lot suspended', `${lot.name} is hidden from search.`);
            }}
          >
            {caseSummary}
            <BookingsWarning />
          </ActionDialog>

          <ActionDialog
            open={open === 'resolve'}
            title={current.status === MisdeclarationCaseStatus.Suspended ? 'Resolve and reinstate lot' : 'Resolve warning'}
            description={
              current.status === MisdeclarationCaseStatus.Suspended
                ? 'Case → Resolved; lot Suspended → Active and visible in search again.'
                : 'Case → Resolved; the lot stays Active.'
            }
            confirmLabel={current.status === MisdeclarationCaseStatus.Suspended ? 'Reinstate lot' : 'Resolve'}
            field={{
              label: 'Conclusion',
              required: true,
              requiredMessage: 'A conclusion is required.',
              placeholder: 'What the owner corrected or how they explained it.',
              helper: 'Saved to LotMisdeclarationCases.ResolutionNote and the audit log.',
              maxLength: 1000,
            }}
            onClose={onClose}
            onSubmit={async (note) => {
              const reinstate = current.status === MisdeclarationCaseStatus.Suspended;
              await adminKybApi.resolveMisdeclarationCase(lot.id, current.id, { note });
              onDone(reinstate ? 'Lot reinstated' : 'Case resolved', reinstate ? `${lot.name} is Active again.` : `Case #${current.id} closed.`);
            }}
          >
            {caseSummary}
          </ActionDialog>

          <ActionDialog
            open={open === 'ban'}
            title="Ban owner for fraud"
            description="Confirms deliberate fraud. Cannot be undone from this screen."
            confirmLabel="Ban owner"
            destructive
            field={{
              label: 'Reason',
              required: true,
              requiredMessage: 'A reason is required.',
              placeholder: 'For example: forged business licence, deliberately inflated capacity.',
              helper: 'Saved on every closed case and in the audit log.',
              maxLength: 1000,
            }}
            onClose={onClose}
            onSubmit={async (reason) => {
              const result = await adminKybApi.banLotOwner(lot.id, current.id, { reason });
              onDone('Owner banned', banMessage(result));
            }}
          >
            {caseSummary}
            <Banner tone="danger">
              Every Active or Pending approval lot of {ownerLabel(lot.ownerProfileId)} becomes Suspended permanently, open KYB applications are
              rejected and other open cases are closed as Banned. Data is kept (no deletion).
            </Banner>
            <BookingsWarning />
            <Banner tone="info">
              The owner account is not locked here. Lock it in{' '}
              <Link to={USERS_PATH} className="underline">
                Users
              </Link>{' '}
              → {ownerLabel(lot.ownerProfileId)} → Lock owner (no end date = permanent ban).
            </Banner>
          </ActionDialog>
        </>
      )}
    </>
  );
};
