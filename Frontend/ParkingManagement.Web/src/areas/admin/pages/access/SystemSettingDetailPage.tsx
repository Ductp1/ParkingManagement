import React, { useState } from 'react';
import { useParams } from 'react-router-dom';
import { adminSettingsApi } from '@/api/adminSettingsApi';
import { SystemConfigChangeStatus } from '@/types/adminEnums';
import type { SystemConfigChangeDto, SystemConfigDetailDto } from '@/types/adminSettings';
import { AdminTitleBar } from '../../layout/AdminTitleBar';
import { AdminButton } from '../../components/AdminButton';
import { AuditTrailLink } from '../../components/AuditTrailLink';
import { auditLogListLink } from './auditLogFormat';
import { Banner } from '../../components/Banner';
import { DetailColumns, QueryState } from '../../components/PageLayout';
import { Panel } from '../../components/Panel';
import { DetailList, DetailRow } from '../../components/DetailRow';
import { AdminDialog } from '../../components/AdminDialog';
import { ActionDialog } from '../../components/ActionDialog';
import { DateTimeField, SelectField, TextAreaField, TextField } from '../../components/FormFields';
import { CellMono, CellMuted, CellStatus, CellText, DataTable } from '../../components/DataTable';
import { Pagination } from '../../components/Pagination';
import { useAdminQuery } from '../../hooks/useAdminQuery';
import { useAdminBreadcrumb } from '../../hooks/useAdminBreadcrumb';
import { useAdminToast } from '../../hooks/useAdminToast';
import { formatDate, formatDateTime, formatDateTimeIct, localInputToUtcIso } from '../../utils/format';

const allowedText = (c: SystemConfigDetailDto) =>
  c.allowedValues && c.allowedValues.length > 0
    ? c.allowedValues.join(' / ')
    : c.minValue !== null || c.maxValue !== null
      ? `${c.minValue ?? '−∞'} – ${c.maxValue ?? '∞'}`
      : '—';

/** Kiểm tra giá trị mới theo DataType/Min/Max/AllowedValues (Figma "Value must be between 1 and 120."). */
const validateValue = (c: SystemConfigDetailDto, raw: string): string | null => {
  const v = raw.trim();
  if (v === '') return 'A value is required.';
  if (c.allowedValues && c.allowedValues.length > 0) return c.allowedValues.includes(v) ? null : `Value must be one of ${c.allowedValues.join(', ')}.`;
  const numeric = ['int', 'decimal', 'number', 'double'].includes(c.dataType.toLowerCase());
  if (numeric) {
    const n = Number(v);
    if (Number.isNaN(n) || (c.dataType.toLowerCase() === 'int' && !Number.isInteger(n))) return `Value must be a valid ${c.dataType}.`;
    const min = c.minValue !== null ? Number(c.minValue) : null;
    const max = c.maxValue !== null ? Number(c.maxValue) : null;
    if ((min !== null && n < min) || (max !== null && n > max)) return `Value must be between ${c.minValue ?? '−∞'} and ${c.maxValue ?? '∞'}.`;
  }
  if (v === c.value) return 'The new value equals the value in effect.';
  return null;
};

const ChangeValueDialog: React.FC<{ open: boolean; config: SystemConfigDetailDto; onClose: () => void; onDone: (change: SystemConfigChangeDto) => void }> = ({
  open,
  config,
  onClose,
  onDone,
}) => {
  const [value, setValue] = useState('');
  const [effective, setEffective] = useState('');
  const [reason, setReason] = useState('');
  const [submitted, setSubmitted] = useState(false);
  const [dateError, setDateError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const [apiError, setApiError] = useState<string | null>(null);

  const valueError = validateValue(config, value);
  const reasonError = reason.trim() === '' ? 'A reason is required.' : null;

  const close = () => {
    setValue('');
    setEffective('');
    setReason('');
    setSubmitted(false);
    setDateError(null);
    setApiError(null);
    onClose();
  };

  const confirm = async () => {
    setSubmitted(true);
    const iso = localInputToUtcIso(effective);
    let dErr: string | null = null;
    if (iso) {
      const t = new Date(iso).getTime();
      if (t < Date.now()) dErr = 'Effective date cannot be in the past.';
      else if (t > Date.now() + 365 * 24 * 3600 * 1000) dErr = 'Effective date must be within 365 days.';
    }
    setDateError(dErr);
    if (valueError || reasonError || dErr) return;
    setSubmitting(true);
    setApiError(null);
    try {
      const change = await adminSettingsApi.update(config.key, { value: value.trim(), reason: reason.trim(), effectiveFromUtc: iso });
      onDone(change);
      close();
    } catch (err: unknown) {
      setApiError(err instanceof Error ? err.message : String(err));
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <AdminDialog
      open={open}
      title="Change value"
      description="US-098 · appends a SystemConfigChanges row; the default value is never overwritten."
      onClose={close}
      onConfirm={confirm}
      confirmLabel="Schedule change"
      submitting={submitting}
      error={apiError}
    >
      <div className="rounded-[2px] border border-(--admin-border-default)">
        <DetailRow label="Key">
          {config.key} · {config.dataType}
        </DetailRow>
        <DetailRow label="Value in effect">{config.value}</DetailRow>
        <DetailRow label="Allowed">{allowedText(config)}</DetailRow>
      </div>
      {config.allowedValues && config.allowedValues.length > 0 ? (
        <SelectField
          label="New value"
          required
          value={value}
          onChange={setValue}
          options={config.allowedValues.map((v) => ({ value: v, label: v }))}
          error={submitted ? valueError : null}
        />
      ) : (
        <TextField label="New value" required value={value} onChange={setValue} error={submitted ? valueError : null} />
      )}
      <DateTimeField
        label="Effective from"
        value={effective}
        onChange={(v) => {
          setEffective(v);
          setDateError(null);
        }}
        helper="Empty = immediately · not in the past · ≤ 365 days ahead"
        error={dateError}
      />
      <TextAreaField label="Reason" required value={reason} onChange={setReason} maxLength={1000} error={submitted ? reasonError : null} />
    </AdminDialog>
  );
};

/** Access & Configuration / System Settings / Detail (US-098): tham số + lịch sử thay đổi + đổi/hủy giá trị. */
export const SystemSettingDetailPage: React.FC = () => {
  const { key = '' } = useParams();
  const toast = useAdminToast();
  const [historyPage, setHistoryPage] = useState(1);
  const [changeOpen, setChangeOpen] = useState(false);
  const [cancelTarget, setCancelTarget] = useState<SystemConfigChangeDto | null>(null);

  const query = useAdminQuery(key ? () => adminSettingsApi.getByKey(key) : null, [key]);
  const history = useAdminQuery(key ? () => adminSettingsApi.getHistory(key, historyPage, 20) : null, [key, historyPage], (d) => d.items.length === 0);
  useAdminBreadcrumb(key || null);

  const config = query.data;
  if (!config) {
    return (
      <>
        <AdminTitleBar title={key || 'System Settings'} subtitle="—" />
        <QueryState
          status={query.status}
          error={query.error}
          onRetry={query.refetch}
          bare={false}
          copy={{ emptyTitle: 'Parameter not found', emptyMessage: 'Check the key.', errorTitle: 'Could not load the parameter', service: 'AdminService' }}
        />
      </>
    );
  }

  const scheduled = history.data?.items.filter((c) => c.status === SystemConfigChangeStatus.Scheduled) ?? [];
  const nextChange = scheduled.sort((a, b) => a.effectiveFromUtc.localeCompare(b.effectiveFromUtc))[0];
  const refresh = () => {
    query.refetch();
    history.refetch();
  };

  return (
    <>
      <AdminTitleBar
        title={config.key}
        subtitle={`${config.dataType} · in effect ${config.value}${nextChange ? ` · next: ${nextChange.value} from ${formatDate(nextChange.effectiveFromUtc)}` : ''}`}
        actions={
          <>
            <AuditTrailLink to={auditLogListLink({ entity: 'SystemConfig', entityId: config.key })} />
            <AdminButton onClick={() => setChangeOpen(true)}>Change value</AdminButton>
          </>
        }
      />
      <DetailColumns
        main={
          <Panel title="Change history" description="SystemConfigChanges · Scheduled / Effective / Superseded / Cancelled" padded={false}>
            <DataTable
              bare
              rows={history.status === 'ready' ? history.data?.items ?? [] : []}
              rowKey={(r) => r.id}
              columns={[
                { key: 'id', header: '#', width: 50, render: (r) => <CellMono>{r.id}</CellMono> },
                { key: 'value', header: 'Value', render: (r) => <CellText>{r.value}</CellText> },
                { key: 'prev', header: 'Previous', render: (r) => <CellText>{r.previousValue}</CellText> },
                { key: 'from', header: 'Effective from', render: (r) => <CellMuted>{formatDateTime(r.effectiveFromUtc)}</CellMuted> },
                { key: 'status', header: 'Status', render: (r) => <CellStatus value={r.status} /> },
                { key: 'reason', header: 'Reason', render: (r) => <CellText>{r.cancelReason ?? r.reason}</CellText> },
                { key: 'by', header: 'By', render: (r) => <CellText>A#{r.createdByUserId}</CellText> },
                {
                  key: 'actions',
                  header: '',
                  width: 80,
                  render: (r) =>
                    r.status === SystemConfigChangeStatus.Scheduled && (
                      <AdminButton variant="ghost" onClick={() => setCancelTarget(r)}>
                        Cancel
                      </AdminButton>
                    ),
                },
              ]}
              placeholder={
                <QueryState
                  status={history.status}
                  error={history.error}
                  onRetry={history.refetch}
                  copy={{ emptyTitle: 'No changes yet', emptyMessage: 'The default value is in effect.', errorTitle: 'Could not load history', service: 'AdminService' }}
                />
              }
              footer={
                history.status === 'ready' &&
                history.data && <Pagination page={historyPage} pageSize={history.data.pageSize} totalCount={history.data.totalCount} onPageChange={setHistoryPage} />
              }
            />
          </Panel>
        }
        side={
          <>
            <Panel title="Parameter" description="SystemConfigs">
              <DetailList>
                <DetailRow label="Key">{config.key}</DetailRow>
                <DetailRow label="Value in effect">{config.value}</DetailRow>
                <DetailRow label="Default value">{config.defaultValue}</DetailRow>
                <DetailRow label="Data type">{config.dataType}</DetailRow>
                <DetailRow label="Allowed (code rule)">{allowedText(config)}</DetailRow>
                <DetailRow label="Description">{config.description ?? '—'}</DetailRow>
                <DetailRow label="Effective since">{formatDateTimeIct(config.effectiveFromUtc)}</DetailRow>
                <DetailRow label="Group" future>
                  —
                </DetailRow>
              </DetailList>
            </Panel>
            <Banner tone="info">
              Bookings pin parameters to their creation time; a scheduled change only affects bookings created after its effective date.
            </Banner>
          </>
        }
      />

      <ChangeValueDialog
        open={changeOpen}
        config={config}
        onClose={() => setChangeOpen(false)}
        onDone={(change) => {
          toast.show({ type: 'success', title: 'Change scheduled', message: `${config.key} → ${change.value} from ${formatDateTimeIct(change.effectiveFromUtc)}.` });
          refresh();
        }}
      />
      <ActionDialog
        open={cancelTarget !== null}
        title="Cancel scheduled change"
        description="Only changes not yet in effect can be cancelled. The row stays in history as Cancelled."
        confirmLabel="Cancel change"
        cancelLabel="Keep"
        destructive
        field={{ label: 'Cancel reason', required: true, requiredMessage: 'A reason is required.', maxLength: 1000 }}
        onClose={() => setCancelTarget(null)}
        onSubmit={async (reason) => {
          if (!cancelTarget) return;
          await adminSettingsApi.cancelChange(config.key, cancelTarget.id, { reason });
          toast.show({ type: 'success', title: 'Change cancelled', message: `Change #${cancelTarget.id} of ${config.key} was cancelled.` });
          refresh();
        }}
      >
        {cancelTarget && (
          <div className="rounded-[2px] border border-(--admin-border-default)">
            <DetailRow label="Change">
              #{cancelTarget.id} · {cancelTarget.previousValue} → {cancelTarget.value} from {formatDateTimeIct(cancelTarget.effectiveFromUtc)}
            </DetailRow>
            <DetailRow label="Reason">{cancelTarget.reason}</DetailRow>
          </div>
        )}
      </ActionDialog>
    </>
  );
};
