import React, { useState } from 'react';
import { Panel } from '../../../components/Panel';
import { StateBlock } from '../../../components/StateBlock';
import { CellLink, CellMuted, CellStatus, CellTwoLine, DataTable } from '../../../components/DataTable';
import { useAdminQuery } from '../../../hooks/useAdminQuery';
import { humanizeEnum } from '../../../utils/enumLabels';
import { formatDateTimeIct, formatNumber } from '../../../utils/format';
import { isOpenRequest, loadUserDataRequests, requestCode } from './userDataRequests';
import { QueryIssueState } from '../../../components/PageLayout';

const DSR_PAGE = '/admin/access/security-privacy';

/**
 * Panel "Data subject requests" của User detail (US-096 / US-007): yêu cầu xem / xuất / sửa / xóa dữ liệu của người này,
 * mỗi dòng mở đúng yêu cầu ở Security & Privacy (?request=id) để xử lý.
 */
export const UserDataRequestsPanel: React.FC<{ userId: number }> = ({ userId }) => {
  const query = useAdminQuery(() => loadUserDataRequests(userId), [userId], (d) => d.items.length === 0);
  const data = query.data;
  const [now] = useState(() => Date.now());

  const body = (() => {
    switch (query.status) {
      case 'loading':
        return <StateBlock type="loading" bare className="min-h-0" />;
      case 'empty':
        return (
          <StateBlock
            type="empty"
            bare
            className="min-h-0 py-4"
            title="No data requests"
            message={
              data?.truncated
                ? `None among the latest ${formatNumber(data.scanned)} requests.`
                : 'This user has not asked to view, export, correct or delete their data.'
            }
          />
        );
      case 'forbidden':
        return <StateBlock type="noPermission" bare className="min-h-0 py-4" message="Only administrators can see data requests." />;
      case 'notFound':
      case 'offline':
      case 'sessionExpired':
        return <QueryIssueState status={query.status} error={query.error} onRetry={query.refetch} bare className="min-h-0 py-4" />;
      case 'error':
      case 'unavailable':
        return (
          <StateBlock
            type="error"
            bare
            className="min-h-0 py-4"
            title="Could not load data requests"
            message={query.error?.problem ? query.error.message : 'Data requests could not be loaded. Try again.'}
            onRetry={query.refetch}
          />
        );
      default:
        return null;
    }
  })();

  return (
    <Panel title="Data subject requests" action={{ label: 'All requests', to: DSR_PAGE }} padded={!data || query.status !== 'ready'}>
      {body ?? (
        <>
          <DataTable
            bare
            animateRows={false}
            rows={data!.items}
            rowKey={(r) => r.id}
            columns={[
              {
                key: 'request',
                header: 'Request',
                render: (r) => (
                  <CellTwoLine
                    primary={<CellLink to={`${DSR_PAGE}?request=${r.id}`}>{requestCode(r.id)}</CellLink>}
                    secondary={humanizeEnum(r.type)}
                  />
                ),
              },
              {
                key: 'status',
                header: 'Status',
                render: (r) =>
                  isOpenRequest(r) && new Date(r.dueAtUtc).getTime() < now ? (
                    <CellStatus value="Overdue" label="Overdue" />
                  ) : (
                    <CellStatus value={r.status} />
                  ),
              },
              { key: 'due', header: 'Due (ICT)', render: (r) => <CellMuted>{formatDateTimeIct(r.dueAtUtc)}</CellMuted> },
            ]}
          />
          {data!.truncated && (
            <p className="adm-type-body-small m-0 px-3 py-2 text-(--admin-text-secondary)">
              {`Searched the latest ${formatNumber(data!.scanned)} requests; older ones are not shown.`}
            </p>
          )}
        </>
      )}
    </Panel>
  );
};
