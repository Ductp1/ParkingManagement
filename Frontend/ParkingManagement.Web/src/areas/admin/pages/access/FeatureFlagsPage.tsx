import React, { useState } from 'react';
import { adminSettingsApi } from '@/api/adminSettingsApi';
import type { FeatureFlagDto } from '@/types/adminSettings';
import { ListScreen, QueryState } from '../../components/PageLayout';
import { CellMono, CellMuted, DataTable } from '../../components/DataTable';
import { TextAreaField, ToggleField } from '../../components/FormFields';
import { AdminDialog } from '../../components/AdminDialog';
import { DetailRow } from '../../components/DetailRow';
import { useAdminQuery } from '../../hooks/useAdminQuery';
import { useAdminToast } from '../../hooks/useAdminToast';
import { formatDateTimeIct, formatNumber } from '../../utils/format';

/** Dialog xác nhận bật/tắt: bắt buộc nhập lý do (ghi vào AuditLogs); lỗi API (409 khi Admin khác vừa đổi) hiện ngay trong dialog. */
const ToggleFlagDialog: React.FC<{
  flag: FeatureFlagDto;
  onClose: () => void;
  onDone: (flag: FeatureFlagDto) => void;
  onFailed: () => void;
}> = ({ flag, onClose, onDone, onFailed }) => {
  const [reason, setReason] = useState('');
  const [submitted, setSubmitted] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [apiError, setApiError] = useState<string | null>(null);
  const reasonError = reason.trim() === '' ? 'A reason is required.' : null;

  const confirm = async () => {
    setSubmitted(true);
    if (reasonError) return;
    setSubmitting(true);
    setApiError(null);
    try {
      onDone(await adminSettingsApi.updateFeatureFlag(flag.key, { isEnabled: !flag.isEnabled, reason: reason.trim() }));
    } catch (err: unknown) {
      setApiError(err instanceof Error ? err.message : String(err));
      setSubmitting(false);
      onFailed();   // 409: Admin khác vừa đổi flag → tải lại để danh sách phía sau hiện đúng giá trị
    }
  };

  return (
    <AdminDialog
      open
      title={`${flag.isEnabled ? 'Disable' : 'Enable'} ${flag.key}?`}
      description="Takes effect immediately for every service that reads this flag. Recorded in AuditLogs."
      onClose={onClose}
      onConfirm={confirm}
      confirmLabel={flag.isEnabled ? 'Disable flag' : 'Enable flag'}
      destructive={flag.isEnabled}
      submitting={submitting}
      error={apiError}
    >
      <div className="rounded-[2px] border border-(--admin-border-default)">
        <DetailRow label="Flag">{flag.key}</DetailRow>
        <DetailRow label="Current">{flag.isEnabled ? 'On' : 'Off'}</DetailRow>
        <DetailRow label="New">{flag.isEnabled ? 'Off' : 'On'}</DetailRow>
      </div>
      <TextAreaField label="Reason" required value={reason} onChange={setReason} maxLength={1000} error={submitted ? reasonError : null} />
    </AdminDialog>
  );
};

/** Access & Configuration / Feature Flags (US-099) – đọc từ GET /api/v1/admin/settings, bật/tắt qua PUT .../feature-flags/{key}. */
export const FeatureFlagsPage: React.FC = () => {
  const toast = useAdminToast();
  const query = useAdminQuery(() => adminSettingsApi.getAll(), [], (d) => d.featureFlags.length === 0);
  const [target, setTarget] = useState<FeatureFlagDto | null>(null);
  const flags = query.data?.featureFlags ?? [];

  return (
    <ListScreen title="Feature Flags" subtitle={query.data ? `FeatureFlags · ${formatNumber(flags.length)} flags (US-099)` : '—'}>
      <DataTable
        rows={query.status === 'ready' ? flags : []}
        rowKey={(r) => r.key}
        columns={[
          { key: 'key', header: 'Key', width: 280, render: (r) => <CellMono>{r.key}</CellMono> },
          { key: 'desc', header: 'Description', render: (r) => <CellMuted>{r.description ?? '—'}</CellMuted> },
          {
            key: 'enabled',
            header: 'Enabled',
            width: 120,
            render: (r) => <ToggleField label={r.isEnabled ? 'On' : 'Off'} checked={r.isEnabled} onChange={() => setTarget(r)} />,
          },
          {
            key: 'by',
            header: 'Updated by',
            width: 170,
            render: (r) =>
              r.updatedByUserId != null ? (
                <CellMuted>
                  User #{r.updatedByUserId}
                  <br />
                  {formatDateTimeIct(r.updatedAtUtc)}
                </CellMuted>
              ) : (
                <CellMuted>—</CellMuted>
              ),
          },
        ]}
        placeholder={
          <QueryState
            status={query.status}
            error={query.error}
            onRetry={query.refetch}
            copy={{ emptyTitle: 'No feature flags', emptyMessage: 'Seeded flags appear here.', errorTitle: 'Could not load feature flags', service: 'AdminService' }}
          />
        }
      />
      {target && (
        <ToggleFlagDialog
          key={target.key}
          flag={target}
          onClose={() => setTarget(null)}
          onFailed={query.refetch}
          onDone={(flag) => {
            setTarget(null);
            toast.show({ type: 'success', title: 'Feature flag updated', message: `${flag.key} is now ${flag.isEnabled ? 'On' : 'Off'}.` });
            query.refetch();
          }}
        />
      )}
    </ListScreen>
  );
};
