import React, { useState } from 'react';
import { adminPartnerOnboardingApi } from '@/api/adminPartnerOnboardingApi';
import type { ContractDraftDto, PartnerApplicationDetailDto, PartnerContractTermsRequest } from '@/types/adminPartnerOnboarding';
import { AdminButton } from '../../../components/AdminButton';
import { AdminDialog, DialogSummary } from '../../../components/AdminDialog';
import { Banner } from '../../../components/Banner';
import { DetailList, DetailRow } from '../../../components/DetailRow';
import { DateTimeField, SelectField, TextAreaField, TextField, ToggleField, UploadZone } from '../../../components/FormFields';
import { bankText, percentText, termText, termsErrors } from './partnerFormat';

const errorText = (err: unknown) => (err instanceof Error ? err.message : String(err));

const TERM_OPTIONS = [
  { value: '12', label: '12 months' },
  { value: '24', label: '24 months' },
  { value: '36', label: '36 months' },
];

interface TermsDialogProps {
  app: PartnerApplicationDetailDto;
  /** send = A6 (T5, v1); revise = A7 (T5b, v+1 + lý do). */
  mode: 'send' | 'revise';
  open: boolean;
  onClose: () => void;
  onDone: (title: string) => void;
}

/**
 * Send / Revise contract: Admin nhập điều khoản → Preview (A5, HTML trong iframe sandbox, không chạy script) → gửi.
 * Sửa hợp đồng điền sẵn điều khoản của phiên bản đang gửi.
 */
export const ContractTermsDialog: React.FC<TermsDialogProps> = ({ app, mode, open, onClose, onDone }) => {
  const current: ContractDraftDto | undefined = app.contractDrafts.find((d) => d.status === 'Current');
  const [rate, setRate] = useState(current ? String(current.commissionRate) : '');
  const [effectiveFrom, setEffectiveFrom] = useState(current?.effectiveFrom ?? '');
  const [termMonths, setTermMonths] = useState(current ? String(current.termMonths) : '12');
  const [bin, setBin] = useState(current?.bankBin ?? '');
  const [bankName, setBankName] = useState(current?.bankName ?? '');
  const [accountNumber, setAccountNumber] = useState(current?.bankAccountNumber ?? '');
  const [accountName, setAccountName] = useState(current?.bankAccountName ?? '');
  const [reason, setReason] = useState('');
  const [submitted, setSubmitted] = useState(false);
  const [busy, setBusy] = useState<'preview' | 'send' | null>(null);
  const [apiError, setApiError] = useState<string | null>(null);
  const [previewHtml, setPreviewHtml] = useState<string | null>(null);

  const errors = termsErrors({ rate, effectiveFrom, termMonths, bin, bankName, accountNumber, accountName });
  const reasonError = mode === 'revise' && reason.trim() === '' ? 'A reason is required.' : null;
  const invalid = Object.values(errors).some(Boolean) || !!reasonError;
  const show = (message: string | null) => (submitted ? message : null);

  const terms = (): PartnerContractTermsRequest => ({
    commissionRate: Number(rate),
    effectiveFrom,
    termMonths: Number(termMonths),
    bank: { bin: bin.trim(), name: bankName.trim(), accountNumber: accountNumber.trim(), accountName: accountName.trim() },
  });

  // Đổi điều khoản thì bản xem trước cũ không còn đúng.
  const edit = (setter: (v: string) => void) => (v: string) => {
    setter(v);
    setPreviewHtml(null);
  };

  const preview = async () => {
    setSubmitted(true);
    if (Object.values(errors).some(Boolean)) return;
    setBusy('preview');
    setApiError(null);
    try {
      const file = await adminPartnerOnboardingApi.previewContract(app.id, terms());
      setPreviewHtml(await file.blob.text());
    } catch (err: unknown) {
      setApiError(errorText(err));
    } finally {
      setBusy(null);
    }
  };

  const confirm = async () => {
    setSubmitted(true);
    if (invalid) return;
    setBusy('send');
    setApiError(null);
    try {
      if (mode === 'send') await adminPartnerOnboardingApi.sendContract(app.id, terms());
      else await adminPartnerOnboardingApi.reviseContract(app.id, { ...terms(), reason: reason.trim() });
      onDone(mode === 'send' ? 'Contract sent' : 'Contract revised');
      onClose();
    } catch (err: unknown) {
      setApiError(errorText(err));
    } finally {
      setBusy(null);
    }
  };

  return (
    <AdminDialog
      open={open}
      size={960}
      title={mode === 'send' ? 'Send contract' : 'Revise contract'}
      description={
        mode === 'send'
          ? 'T5 · Under review → Contract pending. Generates contract v1 from the active template and assigns the contract number.'
          : `T5b · Generates v${(current?.version ?? 0) + 1} with the same contract number; the current version becomes Superseded.`
      }
      confirmLabel={mode === 'send' ? 'Send contract' : 'Send revised contract'}
      onClose={onClose}
      onConfirm={confirm}
      submitting={busy === 'send'}
      error={apiError}
      footerNote="Email to the partner is not automated yet – download the file from the Contract versions panel."
    >
      <DialogSummary title={`${app.code} · ${app.businessName}`} subtitle={`${app.legalRepresentativeName} · ${app.email}`} />
      <div className="grid grid-cols-2 gap-4">
        <TextField
          label="Commission rate (%)"
          type="number"
          required
          value={rate}
          onChange={edit(setRate)}
          helper="Platform commission per completed booking, e.g. 8.5"
          error={show(errors.rate)}
        />
        <DateTimeField label="Effective from" mode="date" required value={effectiveFrom} onChange={edit(setEffectiveFrom)} error={show(errors.effectiveFrom)} />
        <SelectField label="Term" required value={termMonths} onChange={edit(setTermMonths)} options={TERM_OPTIONS} error={show(errors.termMonths)} />
        <TextField label="Bank BIN" required value={bin} onChange={edit(setBin)} maxLength={6} error={show(errors.bin)} />
        <TextField label="Bank name" required value={bankName} onChange={edit(setBankName)} maxLength={100} error={show(errors.bankName)} />
        <TextField label="Account number" required value={accountNumber} onChange={edit(setAccountNumber)} maxLength={30} error={show(errors.accountNumber)} />
        <TextField label="Account name" required value={accountName} onChange={edit(setAccountName)} maxLength={200} error={show(errors.accountName)} />
      </div>
      {mode === 'revise' && (
        <TextAreaField
          label="Reason for revision"
          required
          value={reason}
          onChange={setReason}
          maxLength={1000}
          placeholder="What changed and why?"
          error={show(reasonError)}
        />
      )}
      <div className="flex items-center gap-2">
        <AdminButton variant="secondary" icon="file" onClick={preview} disabled={busy !== null}>
          {busy === 'preview' ? 'Generating…' : 'Preview contract'}
        </AdminButton>
        <span className="adm-type-body-small text-(--admin-text-secondary)">Nothing is saved until you send.</span>
      </div>
      {previewHtml !== null && (
        <iframe
          title="Contract preview"
          sandbox=""
          srcDoc={previewHtml}
          className="h-[420px] w-full border border-(--admin-border-default) bg-(--admin-surface-card)"
        />
      )}
    </AdminDialog>
  );
};

interface IssueAccountDialogProps {
  app: PartnerApplicationDetailDto;
  open: boolean;
  onClose: () => void;
  onDone: (title: string) => void;
}

/**
 * Create owner account (A9, T6): điều khoản lấy từ phiên bản đang gửi (chỉ đọc); Admin tải bản scan đã ký
 * và xác nhận đúng phiên bản. AdminService gọi UserService – lỗi 503 thì hồ sơ giữ nguyên, thử lại an toàn.
 */
export const IssueAccountDialog: React.FC<IssueAccountDialogProps> = ({ app, open, onClose, onDone }) => {
  const current = app.contractDrafts.find((d) => d.status === 'Current');
  const [file, setFile] = useState<File | null>(null);
  const [confirmed, setConfirmed] = useState(false);
  const [submitted, setSubmitted] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [apiError, setApiError] = useState<string | null>(null);

  const confirm = async () => {
    setSubmitted(true);
    if (!current || !file || !confirmed) return;
    setSubmitting(true);
    setApiError(null);
    try {
      await adminPartnerOnboardingApi.issueAccount(app.id, { confirmedDraftVersion: current.version, signedFile: file });
      onDone('Owner account issued');
      onClose();
    } catch (err: unknown) {
      setApiError(errorText(err));
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <AdminDialog
      open={open}
      size={640}
      title="Create owner account"
      description="T6 · Contract pending → Account issued. UserService creates the owner and emails the activation link."
      confirmLabel="Create owner account"
      onClose={onClose}
      onConfirm={confirm}
      confirmDisabled={!current}
      submitting={submitting}
      error={apiError}
    >
      <DialogSummary title={`${app.code} · ${app.businessName}`} subtitle={`MST ${app.taxCode} · ${app.email}`} />
      {current ? (
        <DetailList>
          <DetailRow label="Contract">{`${current.contractNumber} · v${current.version}`}</DetailRow>
          <DetailRow label="Commission">{percentText(current.commissionRate)}</DetailRow>
          <DetailRow label="Term">{termText(current)}</DetailRow>
          <DetailRow label="Bank">{bankText(current)}</DetailRow>
        </DetailList>
      ) : (
        <Banner tone="warning">No current contract version – revise the contract first.</Banner>
      )}
      <UploadZone
        label="Signed contract scan"
        required
        file={file}
        onChange={setFile}
        error={submitted && !file ? 'Upload the signed contract (PDF, JPG or PNG).' : null}
      />
      <ToggleField
        label={current ? `The signed scan matches contract v${current.version}` : 'The signed scan matches the current version'}
        checked={confirmed}
        onChange={setConfirmed}
      />
      {submitted && !confirmed && <span className="adm-type-body-small text-(--admin-status-danger)">Confirm the signed version.</span>}
    </AdminDialog>
  );
};
