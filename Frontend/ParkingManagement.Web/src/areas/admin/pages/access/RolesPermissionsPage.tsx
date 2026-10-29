import React from 'react';
import { adminUsersApi } from '@/api/adminUsersApi';
import { UserRoleType } from '@/types/adminEnums';
import { AdminTitleBar } from '../../layout/AdminTitleBar';
import { KpiStrip, KpiTile } from '../../components/KpiTile';
import { DetailColumns, QueryState } from '../../components/PageLayout';
import { Panel } from '../../components/Panel';
import { DetailList, DetailRow } from '../../components/DetailRow';
import { CellLink, CellStatus, CellText, CellTwoLine, DataTable } from '../../components/DataTable';
import { FutureTag } from '../../components/FutureTag';
import { useAdminQuery } from '../../hooks/useAdminQuery';
import { humanizeEnum } from '../../utils/enumLabels';
import { formatNumber } from '../../utils/format';

const ROLE_NOTE: Record<UserRoleType, string> = {
  Driver: 'Mobile app',
  LotOwner: 'Owner portal · tenant = OwnerProfileId',
  Staff: 'Gate console',
  Admin: 'Admin portal',
};
const ROLES = Object.values(UserRoleType);

/** Access & Configuration / Roles & Permissions: số người mỗi vai trò = totalCount của GET /api/v1/admin/users?role=. */
export const RolesPermissionsPage: React.FC = () => {
  const counts = useAdminQuery(
    async () => {
      const pages = await Promise.all(ROLES.map((role) => adminUsersApi.list({ role, page: 1, pageSize: 1 })));
      return Object.fromEntries(ROLES.map((r, i) => [r, pages[i].totalCount])) as Record<UserRoleType, number>;
    },
    []
  );
  const admins = useAdminQuery(() => adminUsersApi.list({ role: UserRoleType.Admin, page: 1, pageSize: 20 }), [], (d) => d.items.length === 0);

  return (
    <>
      <AdminTitleBar title="Roles & Permissions" subtitle="UserRoles · roles Driver, Lot owner, Staff, Admin (UserRoleType)" />
      {counts.status === 'ready' || counts.status === 'loading' ? (
        <KpiStrip>
          {ROLES.map((r) => (
            <KpiTile
              key={r}
              label={humanizeEnum(r)}
              value={formatNumber(counts.data?.[r])}
              count={{ value: counts.data?.[r], format: formatNumber }}
              delta={ROLE_NOTE[r]}
              loading={counts.status === 'loading'}
            />
          ))}
        </KpiStrip>
      ) : (
        <QueryState
          status={counts.status}
          error={counts.error}
          onRetry={counts.refetch}
          bare={false}
          copy={{ emptyTitle: '', emptyMessage: '', errorTitle: 'Could not load role counts', service: 'UserService' }}
        />
      )}
      <DetailColumns
        main={
          <Panel title="Role members (Admin)" description="UserRoles (UserId, Role)" padded={false}>
            <DataTable
              bare
              rows={admins.status === 'ready' ? admins.data?.items ?? [] : []}
              rowKey={(r) => r.id}
              columns={[
                {
                  key: 'user',
                  header: 'User',
                  render: (r) => <CellTwoLine primary={<CellLink to={`/admin/operations/users/${r.id}`}>{r.fullName}</CellLink>} secondary={r.email ?? '—'} />,
                },
                { key: 'roles', header: 'Roles', render: (r) => <CellText>{r.roles.map(humanizeEnum).join(', ')}</CellText> },
                { key: 'status', header: 'Status', render: (r) => <CellStatus value={r.status} /> },
              ]}
              placeholder={
                <QueryState
                  status={admins.status}
                  error={admins.error}
                  onRetry={admins.refetch}
                  copy={{ emptyTitle: 'No admins', emptyMessage: 'No user has the Admin role.', errorTitle: 'Could not load admins', service: 'UserService' }}
                />
              }
            />
          </Panel>
        }
        side={
          <Panel title="Permission matrix" description="Future — RBAC is role-based only (US-102); no permission table">
            <div className="mb-3 flex items-center gap-2">
              <FutureTag />
              <span className="adm-type-body-regular-12 text-(--admin-text-secondary)">Fine-grained permissions per action</span>
            </div>
            <DetailList>
              {['Approve KYB', 'Lock owner', 'Approve settlement', 'Edit system settings'].map((a) => (
                <DetailRow key={a} label={a} future>
                  Admin
                </DetailRow>
              ))}
            </DetailList>
          </Panel>
        }
      />
    </>
  );
};
