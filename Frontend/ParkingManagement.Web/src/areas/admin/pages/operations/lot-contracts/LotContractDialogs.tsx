import React, { useState } from 'react';
import { adminContractTemplatesApi } from '@/api/adminContractTemplatesApi';
import { adminLotContractsApi } from '@/api/adminLotContractsApi';
import { LotContractVersionStatus } from '@/types/adminEnums';
import {
  SPECIAL_TERMS_MAX_LENGTH,
  SPECIAL_TERMS_MAX_LINES,
  type LotContractDetailDto,
  type LotContractTermsRequest,
} from '@/types/adminLotContracts';
import { AdminButton } from '../../../components/AdminButton';
import { AdminDialog, DialogSummary } from '../../../components/AdminDialog';
import { Banner } from '../../../components/Banner';
import { DetailList, DetailRow } from '../../../components/DetailRow';
import { DateTimeField, SelectField, TextAreaField, TextField, ToggleField, UploadZone } from '../../../components/FormFields';
import { useAdminQuery } from '../../../hooks/useAdminQuery';
import { percentText, saveFile, templateKindLabel, termsErrors, termText } from './lotContractFormat';

const errorText = (err: unknown) => (err instanceof Error ? err.message : String(err));

const TERM_OPTIONS = [
  { value: '12', label: '12 months' },
  { value: '24', label: '24 months' },
  { value: '36', label: '36 months' },
];

/** Lập mới cho một hồ sơ KYB đang thẩm định (L1) hoặc sửa hợp đồng đang chờ ký (L2). */
export type ContractTermsTarget =
  | { mode: 'create'; kybApplicationId: number; lotName: string; ownerLabel: string }
  | { mode: 'revise'; contract: LotContractDetailDto };

interface TermsDialogProps {
  target: ContractTermsTarget;
  onClose: () => void;
  /** Gọi với hợp đồng sau khi lưu. */
  onDone: (contract: LotContractDetailDto, title: string) => void;
}

/**
 * Lập / sửa hợp đồng bãi: Admin chọn loại hợp đồng (mẫu Active), nhập phí nền tảng, ngày hiệu lực, thời hạn, thỏa thuận riêng
 * → Preview (HTML trong iframe sandbox, không chạy script) → lưu. Loại "DOCX file" chỉ cần phí nền tảng (ngày, thời hạn nằm trong file);
 * xem trước loại này tải file DOCX về. Sửa hợp đồng điền sẵn điều khoản + loại hợp đồng của phiên bản đang chờ ký và bắt buộc lý do.
 */
export const ContractTermsDialog: React.FC<TermsDialogProps> = ({ target, onClose, onDone }) => {
  const current =
    target.mode === 'revise' ? target.contract.versions.find((v) => v.status === LotContractVersionStatus.Current) : undefined;
  const templates = useAdminQuery(() => adminContractTemplatesApi.list(), []);
  const active = (templates.data ?? []).filter((t) => t.status === 'Active');
  const [templateId, setTemplateId] = useState(current?.templateId ? String(current.templateId) : '');
  const chosenId = templateId || (active.length === 1 ? String(active[0].id) : '');
  const chosen = active.find((t) => String(t.id) === chosenId);
  const isDocument = chosen?.kind === 'Document';
  const [rate, setRate] = useState(current ? String(current.commissionRate) : '');
  const [effectiveFrom, setEffectiveFrom] = useState(current?.effectiveFrom ?? '');
  const [termMonths, setTermMonths] = useState(current?.termMonths ? String(current.termMonths) : '12');
  const [specialTerms, setSpecialTerms] = useState(current?.specialTerms ?? '');
  const [reason, setReason] = useState('');
  const [submitted, setSubmitted] = useState(false);
  const [busy, setBusy] = useState<'preview' | 'save' | null>(null);
  const [apiError, setApiError] = useState<string | null>(null);
  const [previewHtml, setPreviewHtml] = useState<string | null>(null);

  const allErrors = termsErrors({ rate, effectiveFrom, termMonths });
  // Loại DOCX: chỉ kiểm tra phí nền tảng.
  const errors = isDocument ? { rate: allErrors.rate, effectiveFrom: null, termMonths: null } : allErrors;
  const templateError = chosenId ? null : 'Choose a contract type.';
  const reasonError = target.mode === 'revise' && reason.trim() === '' ? 'A reason is required.' : null;
  const specialLines = specialTerms.trim() === '' ? 0 : specialTerms.trim().split(/\r?\n/).length;
  const specialError = !isDocument && specialLines > SPECIAL_TERMS_MAX_LINES ? `At most ${SPECIAL_TERMS_MAX_LINES} lines.` : null;
  const invalid = Object.values(errors).some(Boolean) || !!specialError || !!templateError;
  const show = (message: string | null) => (submitted ? message : null);

  const terms = (): LotContractTermsRequest =>
    isDocument
      ? { commissionRate: Number(rate), templateId: Number(chosenId) }
      : {
          commissionRate: Number(rate),
          effectiveFrom,
          termMonths: Number(termMonths),
          specialTerms: specialTerms.trim() === '' ? undefined : specialTerms,
          templateId: Number(chosenId),
        };

  // Đổi điều khoản / loại hợp đồng thì bản xem trước cũ không còn đúng.
  const edit = (setter: (v: string) => void) => (v: string) => {
    setter(v);
    setPreviewHtml(null);
  };

  const preview = async () => {
    setSubmitted(true);
    if (invalid) return;
    setBusy('preview');
    setApiError(null);
    try {
      const file =
        target.mode === 'create'
          ? await adminLotContractsApi.previewNew({ ...terms(), kybApplicationId: target.kybApplicationId })
          : await adminLotContractsApi.previewRevision(target.contract.id, terms());
      if (file.blob.type.includes('wordprocessingml')) saveFile(file, 'contract-preview.docx');
      else setPreviewHtml(await file.blob.text());
    } catch (err: unknown) {
      setApiError(errorText(err));
    } finally {
      setBusy(null);
    }
  };

  const confirm = async () => {
    setSubmitted(true);
    if (invalid || reasonError) return;
    setBusy('save');
    setApiError(null);
    try {
      const saved =
        target.mode === 'create'
          ? await adminLotContractsApi.create({ ...terms(), kybApplicationId: target.kybApplicationId })
          : await adminLotContractsApi.revise(target.contract.id, { ...terms(), reason: reason.trim() });
      onDone(saved, target.mode === 'create' ? 'Lot contract created' : 'Contract revised');
      onClose();
    } catch (err: unknown) {
      setApiError(errorText(err));
    } finally {
      setBusy(null);
    }
  };

  const summary =
    target.mode === 'create' ? (
      <DialogSummary title={target.lotName} subtitle={`KYB #${target.kybApplicationId} · ${target.ownerLabel}`} />
    ) : (
      <DialogSummary
        title={`${target.contract.contractNumber} · ${target.contract.lotName}`}
        subtitle={`${target.contract.businessName} · current v${current?.version ?? '—'}`}
      />
    );

  const templateOptions = active.map((t) => ({ value: String(t.id), label: `${t.name} · ${templateKindLabel(t.kind)}` }));

  return (
    <AdminDialog
      open
      size={960}
      title={target.mode === 'create' ? 'Create lot contract' : 'Revise lot contract'}
      description={
        target.mode === 'create'
          ? 'Generates contract v1 for this lot from the chosen contract type. Send it to the owner; uploading the signed contract approves the KYB.'
          : `Generates v${(current?.version ?? 0) + 1} with the same contract number; the current version becomes Superseded.`
      }
      confirmLabel={target.mode === 'create' ? 'Create contract' : 'Save revision'}
      onClose={onClose}
      onConfirm={confirm}
      submitting={busy === 'save'}
      error={apiError ?? (templates.error ? `Could not load contract types: ${templates.error.message}` : null)}
      footerNote="Email to the owner is not automated yet – download the file from the versions table."
    >
      {summary}
      <SelectField
        label="Contract type"
        required
        value={chosenId}
        onChange={edit(setTemplateId)}
        options={templateOptions}
        placeholder={templates.data ? (active.length === 0 ? 'No active contract type' : 'Choose…') : 'Loading…'}
        helper="Active contract templates (Operations → Contract Templates)."
        error={show(templateError)}
      />
      <div className="grid grid-cols-3 gap-4">
        <TextField
          label="Platform fee (%)"
          type="number"
          required
          value={rate}
          onChange={edit(setRate)}
          helper="Percent of GMV per settlement period, e.g. 7.5"
          error={show(errors.rate)}
        />
        {!isDocument && (
          <>
            <DateTimeField label="Effective from" mode="date" required value={effectiveFrom} onChange={edit(setEffectiveFrom)} error={show(errors.effectiveFrom)} />
            <SelectField label="Term" required value={termMonths} onChange={edit(setTermMonths)} options={TERM_OPTIONS} error={show(errors.termMonths)} />
          </>
        )}
      </div>
      {isDocument ? (
        <Banner tone="info">
          This contract type is an uploaded DOCX file: effective date, term and other clauses are in the document. Fields such as
          {' {{BusinessName}} '}in the file are filled in automatically; the platform fee is still needed for settlements.
        </Banner>
      ) : (
        <TextAreaField
          label="Special terms"
          value={specialTerms}
          onChange={edit(setSpecialTerms)}
          maxLength={SPECIAL_TERMS_MAX_LENGTH}
          rows={4}
          placeholder={'The owner pays no platform fee for the first month.\n…'}
          helper="Optional, only for this lot. One clause per line; printed in the template's special-terms article. Leave blank if none."
          error={show(specialError)}
        />
      )}
      {target.mode === 'revise' && (
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
          {busy === 'preview' ? 'Generating…' : isDocument ? 'Download filled DOCX' : 'Preview contract'}
        </AdminButton>
        <span className="adm-type-body-small text-(--admin-text-secondary)">
          {isDocument ? 'Nothing is saved.' : 'Preview is watermarked and nothing is saved.'}
        </span>
      </div>
      {previewHtml !== null && (
        <iframe
          title="Contract preview"
          sandbox=""
          srcDoc={previewHtml}
          className="h-[520px] w-full border border-(--admin-border-default) bg-(--admin-surface-card)"
        />
      )}
    </AdminDialog>
  );
};

interface SignDialogProps {
  contract: LotContractDetailDto;
  /** Điều kiện duyệt KYB còn thiếu (từ ParkingService) – hiện cảnh báo, server vẫn kiểm tra lại. */
  blockers: string[];
  onClose: () => void;
  onDone: (contract: LotContractDetailDto, title: string) => void;
}

/**
 * Ký hợp đồng = duyệt KYB (L3, design v9): tải bản scan đã ký và xác nhận đúng phiên bản đang chờ ký. AdminService gọi ParkingService
 * duyệt KYB + kích hoạt bãi – điều kiện duyệt chưa đạt (409) hoặc lỗi 503 thì hợp đồng vẫn chờ ký, thử lại an toàn.
 */
export const SignContractDialog: React.FC<SignDialogProps> = ({ contract, blockers, onClose, onDone }) => {
  const current = contract.versions.find((v) => v.status === LotContractVersionStatus.Current);
  const [file, setFile] = useState<File | null>(null);
  const [confirmed, setConfirmed] = useState(false);
  const [note, setNote] = useState('');
  const [submitted, setSubmitted] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [apiError, setApiError] = useState<string | null>(null);

  const confirm = async () => {
    setSubmitted(true);
    if (!current || !file || !confirmed) return;
    setSubmitting(true);
    setApiError(null);
    try {
      const saved = await adminLotContractsApi.sign(contract.id, {
        confirmedVersion: current.version,
        signedFile: file,
        note: note.trim() || undefined,
      });
      onDone(saved, 'Contract signed – KYB approved, lot is Active');
      onClose();
    } catch (err: unknown) {
      setApiError(errorText(err));
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <AdminDialog
      open
      size={640}
      title="Upload signed contract & approve KYB"
      description="Contract Pending → Signed, KYB Under review → Approved, lot Pending approval → Active (bookable)."
      confirmLabel="Sign & approve"
      onClose={onClose}
      onConfirm={confirm}
      confirmDisabled={!current}
      submitting={submitting}
      error={apiError}
    >
      <DialogSummary title={`${contract.contractNumber} · ${contract.lotName}`} subtitle={`${contract.businessName} · MST ${contract.taxCode}`} />
      {current ? (
        <DetailList>
          <DetailRow label="Version">{`v${current.version}`}</DetailRow>
          <DetailRow label="Platform fee">{percentText(current.commissionRate)}</DetailRow>
          <DetailRow label="Term">{termText(current)}</DetailRow>
        </DetailList>
      ) : (
        <Banner tone="warning">No version is waiting for signature.</Banner>
      )}
      {blockers.length > 0 && <Banner tone="warning">KYB not ready to approve: {blockers.join(' ')}</Banner>}
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
      <TextAreaField
        label="Approval note"
        value={note}
        onChange={setNote}
        maxLength={1000}
        placeholder="Anything the owner should know?"
        helper="Saved to KybApplications.ReviewNote and both audit logs."
      />
    </AdminDialog>
  );
};
