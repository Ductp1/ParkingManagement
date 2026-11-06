import React, { useState } from 'react';
import { adminSettingsApi } from '@/api/adminSettingsApi';
import type { FeatureFlagDto } from '@/types/adminSettings';
import { ListScreen, QueryState } from '../../components/PageLayout';
import { CellMono, CellMuted, DataTable } from '../../components/DataTable';
import { ToggleField } from '../../components/FormFields';
import { AdminDialog } from '../../components/AdminDialog';
import { DetailRow } from '../../components/DetailRow';
import { useAdminQuery } from '../../hooks/useAdminQuery';
import { formatNumber } from '../../utils/format';

/**
 * Access & Configuration / Feature Flags – danh sách đọc từ GET /api/v1/admin/settings (featureFlags).
 * Logic bật/tắt thuộc US-099 (TV9): chỉ dựng dialog theo Figma, nút xác nhận bị khóa.
 */
export const FeatureFlagsPage: React.FC = () => {
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
          { key: 'by', header: 'Updated by', width: 120, render: () => <CellMuted>—</CellMuted> },
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
        <AdminDialog
          open
          title={`${target.isEnabled ? 'Disable' : 'Enable'} ${target.key}?`}
          description="Applies to all drivers and owners immediately. Recorded in AuditLogs."
          onClose={() => setTarget(null)}
          onConfirm={() => undefined}
          confirmLabel={target.isEnabled ? 'Disable flag' : 'Enable flag'}
          confirmDisabled
          footerNote="Toggle logic belongs to US-099 (TV9) — not wired here."
        >
          <div className="rounded-[2px] border border-(--admin-border-default)">
            <DetailRow label="Flag">{target.key}</DetailRow>
            <DetailRow label="Current">{target.isEnabled ? 'On' : 'Off'}</DetailRow>
            <DetailRow label="New">{target.isEnabled ? 'Off' : 'On'}</DetailRow>
          </div>
        </AdminDialog>
      )}
    </ListScreen>
  );
};
