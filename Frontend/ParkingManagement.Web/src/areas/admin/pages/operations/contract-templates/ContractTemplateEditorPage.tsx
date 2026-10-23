import React, { useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { adminContractTemplatesApi } from '@/api/adminContractTemplatesApi';
import type {
  ContractPlaceholderDto,
  ContractTemplateDetailDto,
  ContractTemplateKind,
  ContractTemplateSummaryDto,
  PlatformPartyDto,
  TemplateContentDto,
} from '@/types/adminContractTemplates';
import { ActionDialog } from '../../../components/ActionDialog';
import { AdminButton } from '../../../components/AdminButton';
import { DialogSummary } from '../../../components/AdminDialog';
import { Banner } from '../../../components/Banner';
import { DetailList, DetailRow } from '../../../components/DetailRow';
import { RadioField, TextAreaField, TextField, ToggleField } from '../../../components/FormFields';
import { DetailColumns, QueryState } from '../../../components/PageLayout';
import { Panel } from '../../../components/Panel';
import { StatusIndicator } from '../../../components/StatusIndicator';
import { useAdminBreadcrumb } from '../../../hooks/useAdminBreadcrumb';
import { useAdminQuery } from '../../../hooks/useAdminQuery';
import { useAdminToast } from '../../../hooks/useAdminToast';
import { AdminTitleBar } from '../../../layout/AdminTitleBar';
import { formatDateTimeIct, formatNumber } from '../../../utils/format';
import { saveFile } from '../lot-contracts/lotContractFormat';
import {
  cleanContent,
  CONTRACT_TEMPLATE_BASE,
  kindLabel,
  MARKUP_HELP,
  PLATFORM_FIELD_MAX,
  PLATFORM_FIELDS,
  structuredBlockers,
  TEMPLATE_BODY_MAX,
  TEMPLATE_NAME_MAX,
  TEMPLATE_NOTE_MAX,
  unknownFields,
} from './contractTemplateFormat';
import { NewTemplateDialog, RenameTemplateDialog } from './ContractTemplateDialogs';
import { TemplateContentEditor } from './TemplateContentEditor';

const errorText = (err: unknown) => (err instanceof Error ? err.message : String(err));

/**
 * Operations / Contract Templates / Detail (US-116 v11, T1–T6). Mẫu dạng form: soạn từng phần (đầu trang, căn cứ, Bên A, điều / khoản,
 * phụ lục, chữ ký) – không gõ markup. Mẫu nhập từ DOCX: chọn dùng form (AI / bộ tách đã đọc) hoặc file gốc. Mẫu markup cũ: xem trước,
 * "Convert to form"; ô markup thô chỉ nằm trong phần Advanced.
 */
export const ContractTemplateEditorPage: React.FC = () => {
  const { id } = useParams();
  const templateId = Number(id);
  const valid = Number.isInteger(templateId) && templateId > 0;
  const query = useAdminQuery(valid ? () => adminContractTemplatesApi.getById(templateId) : null, [templateId]);
  const placeholders = useAdminQuery(() => adminContractTemplatesApi.placeholders(), []);
  const all = useAdminQuery(() => adminContractTemplatesApi.list(), []);

  useAdminBreadcrumb(query.data ? query.data.name : null);

  if (!query.data) {
    return (
      <>
        <AdminTitleBar title="Contract template" subtitle="—" />
        <QueryState
          status={valid ? query.status : 'empty'}
          error={query.error}
          onRetry={query.refetch}
          bare={false}
          copy={{
            emptyTitle: 'Template not found',
            emptyMessage: 'Check the link or go back to Contract Templates.',
            errorTitle: 'Could not load the contract template',
            service: 'AdminService',
          }}
        />
      </>
    );
  }

  // Khóa theo rowVersion: lưu xong tải lại thì form nhận giá trị mới từ server.
  return (
    <TemplateEditor
      key={`${query.data.id}-${query.data.rowVersion}`}
      template={query.data}
      catalog={placeholders.data ?? []}
      templates={all.data ?? []}
      onSaved={() => {
        query.refetch();
        all.refetch();
      }}
    />
  );
};

type Dialog = 'activate' | 'discard' | 'retire' | 'rename' | 'duplicate' | null;

interface EditorProps {
  template: ContractTemplateDetailDto;
  catalog: ContractPlaceholderDto[];
  templates: ContractTemplateSummaryDto[];
  onSaved: () => void;
}

const TemplateEditor: React.FC<EditorProps> = ({ template, catalog, templates, onSaved }) => {
  const navigate = useNavigate();
  const toast = useAdminToast();
  const isDraft = template.status === 'Draft';
  const readOnly = !isDraft;

  const [name, setName] = useState(template.name);
  const [changeNote, setChangeNote] = useState(template.changeNote ?? '');
  const [kind, setKind] = useState<ContractTemplateKind>(template.kind as ContractTemplateKind);
  const [content, setContent] = useState<TemplateContentDto | null>(template.content);
  const [body, setBody] = useState(template.body);
  const [platform, setPlatform] = useState<PlatformPartyDto>(template.platform);
  const [dirty, setDirty] = useState(false);
  const [busy, setBusy] = useState<'save' | 'preview' | 'convert' | 'download' | null>(null);
  const [apiError, setApiError] = useState<string | null>(null);
  const [previewHtml, setPreviewHtml] = useState<string | null>(null);
  const [dialog, setDialog] = useState<Dialog>(null);
  const [retireSource, setRetireSource] = useState(false);

  const touch = <T,>(setter: (v: T) => void) => (v: T) => {
    setter(v);
    setDirty(true);
    setPreviewHtml(null);
  };

  const blockers =
    kind === 'Structured'
      ? content
        ? structuredBlockers(content, platform, catalog)
        : ['The form is empty.']
      : kind === 'Document'
        ? template.sourceFileName
          ? []
          : ['No DOCX file.']
        : unknownFields([body], catalog).map((k) => `Unknown field {{${k}}}.`);
  const activateBlockers = [...(dirty ? ['Save the draft first.'] : []), ...blockers];

  const run = async (kindOfWork: 'save' | 'preview' | 'convert' | 'download', action: () => Promise<void>) => {
    setBusy(kindOfWork);
    setApiError(null);
    try {
      await action();
    } catch (err: unknown) {
      setApiError(errorText(err));
    } finally {
      setBusy(null);
    }
  };

  const preview = () =>
    run('preview', async () => {
      const file = await adminContractTemplatesApi.preview({
        kind,
        content: kind === 'Structured' && content ? cleanContent(content) : undefined,
        body: kind === 'Markup' ? body : undefined,
        platform,
      });
      setPreviewHtml(await file.blob.text());
    });

  const save = () =>
    run('save', async () => {
      await adminContractTemplatesApi.updateDraft(template.id, {
        name,
        changeNote,
        kind,
        content: content && kind !== 'Markup' ? cleanContent(content) : null,
        body: kind === 'Markup' ? body : undefined,
        platform,
        rowVersion: template.rowVersion,
      });
      toast.show({ type: 'success', title: 'Draft saved', message: name });
      onSaved();
    });

  const convert = () =>
    run('convert', async () => {
      const created = await adminContractTemplatesApi.convert(template.id);
      toast.show({ type: 'success', title: 'Form draft created', message: created.name });
      navigate(`${CONTRACT_TEMPLATE_BASE}/${created.id}`);
    });

  const downloadSource = () =>
    run('download', async () => saveFile(await adminContractTemplatesApi.sourceFile(template.id), template.sourceFileName ?? 'contract.docx'));

  const nav = (variant: 'onNavy' | 'primary', label: string, onClick: () => void, disabled = false) => (
    <AdminButton variant={variant} onClick={onClick} disabled={busy !== null || disabled}>
      {label}
    </AdminButton>
  );

  const actions = (
    <>
      {template.kind === 'Markup' && nav('onNavy', busy === 'convert' ? 'Converting…' : 'Convert to form', convert)}
      {isDraft ? (
        <>
          {nav('onNavy', 'Discard draft', () => setDialog('discard'))}
          {nav('onNavy', busy === 'save' ? 'Saving…' : 'Save draft', save, !dirty)}
          {nav('primary', 'Activate', () => setDialog('activate'), activateBlockers.length > 0)}
        </>
      ) : (
        <>
          {nav('onNavy', 'Rename', () => setDialog('rename'))}
          {nav(template.status === 'Active' ? 'onNavy' : 'primary', 'Duplicate', () => setDialog('duplicate'))}
          {template.status === 'Active' && nav('primary', 'Retire', () => setDialog('retire'))}
        </>
      )}
    </>
  );

  const importNotes = template.importNotes?.split('\n').filter((n) => n.trim() !== '') ?? [];

  return (
    <>
      <AdminTitleBar title={template.name} subtitle={`${kindLabel(kind)} · ${template.status} · #${template.version}`} actions={actions} />
      {apiError && <Banner tone="danger">{apiError}</Banner>}
      {isDraft && importNotes.length > 0 && (
        <Banner tone="warning">
          <span className="flex flex-col gap-0.5">
            <strong>Check after import:</strong>
            {importNotes.map((n) => (
              <span key={n}>• {n}</span>
            ))}
          </span>
        </Banner>
      )}
      {isDraft && blockers.length > 0 && <Banner tone="warning">{`Not ready to activate: ${blockers.join(' ')}`}</Banner>}
      {!isDraft && (
        <Banner tone="info">
          {template.status === 'Active'
            ? 'This contract type can be chosen for new and revised lot contracts. Duplicate it to change the content; rename works any time.'
            : 'This template is retired and cannot be chosen any more. Contracts generated from it keep their files.'}
        </Banner>
      )}
      <DetailColumns
        main={
          <>
            <Panel title="General" description="The name is what admins pick in Create lot contract">
              <div className="flex flex-col gap-4">
                <TextField label="Name" required value={name} onChange={touch(setName)} maxLength={TEMPLATE_NAME_MAX} readOnly={readOnly}
                  helper={readOnly ? 'Use Rename to change the name.' : undefined} />
                {isDraft ? (
                  <TextAreaField label="Change note" value={changeNote} onChange={touch(setChangeNote)} maxLength={TEMPLATE_NOTE_MAX} rows={2}
                    placeholder="What is different from the template it was copied from?" />
                ) : (
                  <TextField label="Change note" value={template.changeNote ?? '—'} readOnly />
                )}
              </div>
            </Panel>

            {template.sourceFileName && (
              <Panel title="Source document" description="Imported DOCX file">
                <div className="flex flex-col gap-3">
                  <div className="flex flex-wrap items-center gap-3">
                    <span className="adm-type-body-default text-(--admin-text-primary)">{template.sourceFileName}</span>
                    <AdminButton variant="secondary" icon="download" onClick={downloadSource} disabled={busy !== null}>
                      {busy === 'download' ? 'Loading…' : 'Download original'}
                    </AdminButton>
                  </div>
                  {isDraft && (
                    <RadioField
                      name="template-kind"
                      value={kind}
                      onChange={(v) => touch(setKind)(v as ContractTemplateKind)}
                      options={[
                        ...(content ? [{ value: 'Structured', label: 'Use the form (edit articles and fields here)' }] : []),
                        { value: 'Document', label: 'Use the original DOCX file (lot contract = copy of the file; only the platform fee is entered)' },
                      ]}
                    />
                  )}
                </div>
              </Panel>
            )}

            <Panel title="Party A (platform)" description="Printed in the header, the Party A block and the signature">
              <div className="grid grid-cols-1 gap-4 md:grid-cols-2">
                {PLATFORM_FIELDS.map((f) => (
                  <TextField key={f.key} label={f.label} required={kind !== 'Document'} value={platform[f.key]}
                    onChange={(v) => touch(setPlatform)({ ...platform, [f.key]: v })} placeholder={f.placeholder}
                    maxLength={PLATFORM_FIELD_MAX} readOnly={readOnly} />
                ))}
              </div>
            </Panel>

            {kind === 'Structured' && content && (
              <TemplateContentEditor content={content} onChange={touch(setContent)} catalog={catalog} readOnly={readOnly} />
            )}
            {kind === 'Document' && (
              <Banner tone="info">
                Lot contracts of this type are a copy of the DOCX file. Write fields such as {'{{BusinessName}}'}, {'{{LotName}}'} or
                {' {{CommissionRate}}'} in the Word file to have them filled in; everything else is printed as written.
              </Banner>
            )}
            {kind === 'Markup' && (
              <Banner tone="info">
                Legacy template written in markup. Use “Convert to form” to edit it with the form; the raw text is under Advanced.
              </Banner>
            )}

            {kind !== 'Document' && (
              <Panel title="Preview" description="Sample lot, Party B and special terms – watermarked, nothing is saved" padded={previewHtml === null}>
                {previewHtml === null ? (
                  <AdminButton variant="secondary" icon="file" onClick={preview} disabled={busy !== null}>
                    {busy === 'preview' ? 'Generating…' : 'Preview with sample data'}
                  </AdminButton>
                ) : (
                  <iframe title="Template preview" sandbox="" srcDoc={previewHtml} className="h-[720px] w-full bg-(--admin-surface-card)" />
                )}
              </Panel>
            )}

            {kind !== 'Document' && (
              <details className="border border-(--admin-border-default) bg-(--admin-surface-card)">
                <summary className="adm-type-panel-title cursor-pointer px-3 py-2 uppercase text-(--admin-text-secondary)">
                  Advanced – {kind === 'Markup' ? 'raw markup' : 'generated markup (read-only)'}
                </summary>
                <div className="flex flex-col gap-3 px-3 pb-3">
                  <textarea
                    aria-label="Template markup"
                    value={kind === 'Markup' ? body : template.body}
                    readOnly={kind !== 'Markup' || readOnly}
                    maxLength={TEMPLATE_BODY_MAX}
                    spellCheck={false}
                    onChange={(e) => touch(setBody)(e.target.value)}
                    className="h-[420px] w-full resize-y rounded-[2px] border border-(--admin-border-default) bg-(--admin-surface-card) px-2.5 py-2 font-mono text-xs leading-5 text-(--admin-text-primary) outline-none read-only:bg-(--admin-surface-page)"
                  />
                  {kind === 'Structured' && (
                    <span className="adm-type-body-small text-(--admin-text-secondary)">Shows the last saved version; it is rebuilt from the form on save.</span>
                  )}
                  {kind === 'Markup' && (
                    <dl className="m-0 grid grid-cols-1 gap-1 md:grid-cols-2">
                      {MARKUP_HELP.map((m) => (
                        <div key={m.syntax}>
                          <dt className="font-mono text-xs text-(--admin-text-primary)">{m.syntax}</dt>
                          <dd className="adm-type-body-small m-0 text-(--admin-text-secondary)">{m.meaning}</dd>
                        </div>
                      ))}
                    </dl>
                  )}
                </div>
              </details>
            )}
          </>
        }
        side={
          <>
            <Panel title="Details">
              <DetailList>
                <DetailRow label="Status">
                  <StatusIndicator value={template.status} />
                </DetailRow>
                <DetailRow label="Type">{kindLabel(template.kind)}</DetailRow>
                {template.sourceTemplateName && <DetailRow label="Copied from">{template.sourceTemplateName}</DetailRow>}
                <DetailRow label="Activated">{formatDateTimeIct(template.activatedAtUtc)}</DetailRow>
                <DetailRow label="Used by">{`${formatNumber(template.usedByVersions)} contract files`}</DetailRow>
                <DetailRow label="Created">
                  {`${formatDateTimeIct(template.createdAtUtc)}${template.createdByAdminId ? ` · Admin #${template.createdByAdminId}` : ' · seed'}`}
                </DetailRow>
                <DetailRow label="Updated">{formatDateTimeIct(template.updatedAtUtc)}</DetailRow>
              </DetailList>
            </Panel>
            <Panel title="Fields" description="Filled in when a lot contract is generated · * required to activate">
              <div className="flex max-h-[640px] flex-col gap-3 overflow-y-auto">
                {[...new Set(catalog.map((p) => p.group))].map((group) => (
                  <div key={group} className="flex flex-col gap-1">
                    <span className="adm-type-body-strong text-(--admin-text-secondary)">{group}</span>
                    {catalog
                      .filter((p) => p.group === group)
                      .map((p) => (
                        <div key={p.key} className="flex flex-col" title={`Sample: ${p.sample}`}>
                          <span className="adm-type-body-default text-(--admin-text-primary)">
                            {p.description}
                            {p.required && <span className="text-(--admin-status-danger)"> *</span>}
                          </span>
                          <span className="font-mono text-xs text-(--admin-text-secondary)">{`{{${p.key}}}`}</span>
                        </div>
                      ))}
                  </div>
                ))}
              </div>
            </Panel>
          </>
        }
      />

      <ActionDialog
        open={dialog === 'activate'}
        title={`Activate “${template.name}”`}
        description="Admins can choose this contract type for new and revised lot contracts. Other active types stay available."
        confirmLabel="Activate"
        onClose={() => setDialog(null)}
        onSubmit={async () => {
          await adminContractTemplatesApi.activate(template.id, template.rowVersion, retireSource);
          toast.show({ type: 'success', title: 'Template activated', message: template.name });
          onSaved();
        }}
      >
        <DialogSummary title={template.name} subtitle={template.changeNote ?? undefined} />
        {template.sourceTemplateName && (
          <ToggleField label={`Retire “${template.sourceTemplateName}” (this draft replaces it)`} checked={retireSource} onChange={setRetireSource} />
        )}
      </ActionDialog>
      <ActionDialog
        open={dialog === 'retire'}
        title={`Retire “${template.name}”`}
        description="It can no longer be chosen for new or revised lot contracts. Contracts already generated keep their files."
        confirmLabel="Retire"
        destructive
        onClose={() => setDialog(null)}
        onSubmit={async () => {
          await adminContractTemplatesApi.retire(template.id, template.rowVersion);
          toast.show({ type: 'success', title: 'Template retired', message: template.name });
          onSaved();
        }}
      >
        <DialogSummary title={template.name} />
      </ActionDialog>
      <ActionDialog
        open={dialog === 'discard'}
        title={`Discard draft “${template.name}”`}
        description="The draft is deleted. Active templates are not affected."
        confirmLabel="Discard draft"
        cancelLabel="Keep draft"
        destructive
        onClose={() => setDialog(null)}
        onSubmit={async () => {
          await adminContractTemplatesApi.discard(template.id);
          toast.show({ type: 'success', title: 'Draft discarded', message: template.name });
          navigate(CONTRACT_TEMPLATE_BASE);
        }}
      >
        <DialogSummary title={template.name} />
      </ActionDialog>
      {dialog === 'rename' && (
        <RenameTemplateDialog
          template={template}
          onClose={() => setDialog(null)}
          onRenamed={(t) => {
            toast.show({ type: 'success', title: 'Template renamed', message: t.name });
            setDialog(null);
            onSaved();
          }}
        />
      )}
      {dialog === 'duplicate' && (
        <NewTemplateDialog
          templates={templates.length > 0 ? templates : [template]}
          sourceId={template.id}
          onClose={() => setDialog(null)}
          onCreated={(t) => {
            toast.show({ type: 'success', title: 'Draft created', message: t.name });
            setDialog(null);
            navigate(`${CONTRACT_TEMPLATE_BASE}/${t.id}`);
          }}
        />
      )}
    </>
  );
};
