import React, { useState } from 'react';
import { adminContractTemplatesApi } from '@/api/adminContractTemplatesApi';
import type { ContractTemplateDetailDto, ContractTemplateSummaryDto } from '@/types/adminContractTemplates';
import { AdminDialog } from '../../../components/AdminDialog';
import { Banner } from '../../../components/Banner';
import { RadioField, SelectField, TextField, ToggleField, UploadZone } from '../../../components/FormFields';
import { DOCX_MAX_BYTES, kindLabel, TEMPLATE_NAME_MAX } from './contractTemplateFormat';

const errorText = (err: unknown) => (err instanceof Error ? err.message : String(err));

interface NewProps {
  templates: ContractTemplateSummaryDto[];
  /** Mẫu nguồn chọn sẵn (nút Duplicate trên trang chi tiết). */
  sourceId?: number;
  onClose: () => void;
  onCreated: (template: ContractTemplateDetailDto) => void;
}

/** T2: mẫu mới có tên – khung form mặc định hoặc bản sao một mẫu có sẵn. */
export const NewTemplateDialog: React.FC<NewProps> = ({ templates, sourceId, onClose, onCreated }) => {
  const [name, setName] = useState('');
  const [mode, setMode] = useState<'Blank' | 'Duplicate'>(sourceId ? 'Duplicate' : 'Blank');
  const [source, setSource] = useState(sourceId ? String(sourceId) : String(templates.find((t) => t.status === 'Active')?.id ?? ''));
  const [submitted, setSubmitted] = useState(false);
  const [busy, setBusy] = useState(false);
  const [apiError, setApiError] = useState<string | null>(null);

  const nameError = name.trim() === '' ? 'A name is required.' : null;
  const sourceError = mode === 'Duplicate' && !source ? 'Choose a template to copy.' : null;

  const confirm = async () => {
    setSubmitted(true);
    if (nameError || sourceError) return;
    setBusy(true);
    setApiError(null);
    try {
      onCreated(await adminContractTemplatesApi.create({ name: name.trim(), mode, sourceTemplateId: mode === 'Duplicate' ? Number(source) : undefined }));
    } catch (err: unknown) {
      setApiError(errorText(err));
    } finally {
      setBusy(false);
    }
  };

  return (
    <AdminDialog
      open
      size={640}
      title="New contract template"
      description="Each template is a contract type admins can pick when creating a lot contract. It starts as a draft."
      confirmLabel="Create draft"
      onClose={onClose}
      onConfirm={confirm}
      submitting={busy}
      error={apiError}
    >
      <TextField label="Name" required value={name} onChange={setName} maxLength={TEMPLATE_NAME_MAX}
        placeholder="e.g. Hợp đồng bãi trung tâm thương mại" error={submitted ? nameError : null} />
      <RadioField
        name="new-template-mode"
        value={mode}
        onChange={(v) => setMode(v as 'Blank' | 'Duplicate')}
        options={[
          { value: 'Blank', label: 'Start from a short form (term, platform fee, special terms, lot and payout appendices)' },
          { value: 'Duplicate', label: 'Copy an existing template' },
        ]}
      />
      {mode === 'Duplicate' && (
        <SelectField label="Template to copy" required value={source} onChange={setSource} error={submitted ? sourceError : null}
          options={templates.map((t) => ({ value: String(t.id), label: `${t.name} · ${kindLabel(t.kind)} · ${t.status}` }))} />
      )}
    </AdminDialog>
  );
};

interface ImportProps {
  onClose: () => void;
  onCreated: (template: ContractTemplateDetailDto) => void;
}

/** T2i: nhập hợp đồng từ file .docx; AI (tùy chọn) đọc và chuyển sang form. */
export const ImportDocxDialog: React.FC<ImportProps> = ({ onClose, onCreated }) => {
  const [file, setFile] = useState<File | null>(null);
  const [name, setName] = useState('');
  const [useAi, setUseAi] = useState(true);
  const [submitted, setSubmitted] = useState(false);
  const [busy, setBusy] = useState(false);
  const [apiError, setApiError] = useState<string | null>(null);

  const fileError = !file ? 'Choose a .docx file.' : !file.name.toLowerCase().endsWith('.docx') ? 'Only .docx files are accepted.' : null;

  const confirm = async () => {
    setSubmitted(true);
    if (fileError || !file) return;
    setBusy(true);
    setApiError(null);
    try {
      onCreated(await adminContractTemplatesApi.importDocx({ file, name: name.trim() || undefined, useAi }));
    } catch (err: unknown) {
      setApiError(errorText(err));
    } finally {
      setBusy(false);
    }
  };

  return (
    <AdminDialog
      open
      size={640}
      title="Import contract from DOCX"
      description="The document is read and turned into the form when possible; otherwise the original DOCX is used as the contract file."
      confirmLabel={busy ? 'Analysing…' : 'Import'}
      onClose={onClose}
      onConfirm={confirm}
      submitting={busy}
      error={apiError}
      footerNote=".doc files and Google Docs: export to .docx first (File → Download → Microsoft Word)."
    >
      <UploadZone label="Contract file (.docx, max 10 MB)" required accept=".docx" maxBytes={DOCX_MAX_BYTES} file={file} onChange={setFile}
        error={submitted ? fileError : null} />
      <TextField label="Name" value={name} onChange={setName} maxLength={TEMPLATE_NAME_MAX} helper="Leave blank to use the file name." />
      <ToggleField label="Analyze with AI (Claude)" checked={useAi} onChange={setUseAi} />
      {useAi ? (
        <Banner tone="warning">
          The text of the document is sent to Anthropic (Claude) to detect articles, clauses and fields. Turn this off for documents that
          must not leave the system – a simpler built-in reader is used instead.
        </Banner>
      ) : (
        <Banner tone="info">The built-in reader splits “Điều N.” / “n.n.” lines; partner, lot and fee values stay as plain text to replace with fields.</Banner>
      )}
    </AdminDialog>
  );
};

interface RenameProps {
  template: ContractTemplateDetailDto | (ContractTemplateSummaryDto & { rowVersion?: string });
  onClose: () => void;
  onRenamed: (template: ContractTemplateDetailDto) => void;
}

/** T3n: đổi tên mẫu (mọi trạng thái). Từ danh sách thì tải rowVersion trước khi lưu. */
export const RenameTemplateDialog: React.FC<RenameProps> = ({ template, onClose, onRenamed }) => {
  const [name, setName] = useState(template.name);
  const [busy, setBusy] = useState(false);
  const [apiError, setApiError] = useState<string | null>(null);
  const error = name.trim() === '' ? 'A name is required.' : null;

  const confirm = async () => {
    if (error) return;
    setBusy(true);
    setApiError(null);
    try {
      const rowVersion = template.rowVersion ?? (await adminContractTemplatesApi.getById(template.id)).rowVersion;
      onRenamed(await adminContractTemplatesApi.rename(template.id, name.trim(), rowVersion));
    } catch (err: unknown) {
      setApiError(errorText(err));
    } finally {
      setBusy(false);
    }
  };

  return (
    <AdminDialog open size={520} title="Rename contract template" description="Only the name changes; contracts already generated keep their files."
      confirmLabel="Rename" onClose={onClose} onConfirm={confirm} submitting={busy} error={apiError}>
      <TextField label="Name" required value={name} onChange={setName} maxLength={TEMPLATE_NAME_MAX} error={error} />
    </AdminDialog>
  );
};
