import React from 'react';
import { useParams } from 'react-router-dom';
import { adminAuditLogsApi } from '@/api/adminAuditLogsApi';
import { AdminTitleBar } from '../../layout/AdminTitleBar';
import { DetailColumns, QueryState } from '../../components/PageLayout';
import { Panel } from '../../components/Panel';
import { useAdminQuery } from '../../hooks/useAdminQuery';
import { useAdminBreadcrumb } from '../../hooks/useAdminBreadcrumb';
import { formatDateTimeIct } from '../../utils/format';
import { AuditChangesTable, AuditEntryList, AuditJsonBlock, AuditOriginNote, AuditRelatedLinks } from './AuditLogDetailParts';

/** Access & Configuration / Audit Log / Detail (US-100): GET /api/v1/admin/audit-logs/{id} – chỉ đọc. */
export const AuditLogDetailPage: React.FC = () => {
  const { id = '' } = useParams();
  const auditId = /^[1-9]\d*$/.test(id) ? Number(id) : null;
  const query = useAdminQuery(auditId !== null ? () => adminAuditLogsApi.getById(auditId) : null, [auditId]);
  useAdminBreadcrumb(auditId !== null ? `#${auditId}` : null);

  const log = query.data;
  if (!log) {
    return (
      <>
        <AdminTitleBar title={auditId !== null ? `Audit log #${auditId}` : 'Audit Log'} subtitle="—" />
        <QueryState
          status={auditId === null ? 'empty' : query.status}
          error={query.error}
          onRetry={query.refetch}
          bare={false}
          copy={{ emptyTitle: 'Audit entry not found', emptyMessage: 'Check the audit log ID.', errorTitle: 'Could not load the audit entry', service: 'AdminService' }}
        />
      </>
    );
  }

  return (
    <div className="adm-motion-fade flex flex-col gap-4">
      <AdminTitleBar title={`Audit log #${log.id}`} subtitle={`${log.action} · ${log.sourceService} · ${formatDateTimeIct(log.createdAtUtc)}`} />
      <AuditOriginNote log={log} />
      <DetailColumns
        main={
          <>
            <Panel title="Changed fields" description="Old → new value of each field · sensitive values masked" padded={false}>
              <AuditChangesTable changes={log.changes} />
            </Panel>
            <Panel title="Old values" description="OldValuesJson (masked)">
              <AuditJsonBlock value={log.oldValues} />
            </Panel>
            <Panel title="New values" description="NewValuesJson (masked)">
              <AuditJsonBlock value={log.newValues} />
            </Panel>
          </>
        }
        side={
          <>
            <Panel title="Entry" description="AuditLogs">
              <AuditEntryList log={log} />
            </Panel>
            <Panel title="Related entries">
              <AuditRelatedLinks log={log} />
            </Panel>
          </>
        }
      />
    </div>
  );
};
