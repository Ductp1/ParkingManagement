import React from 'react';
import { useNavigate } from 'react-router-dom';
import { adminUsersApi } from '@/api/adminUsersApi';
import { UserRoleType, UserStatus } from '@/types/adminEnums';
import type { AdminUserSummaryDto } from '@/types/adminUsers';
import { ListScreen, QueryState } from '../../../components/PageLayout';
import { FilterColumn, FilterPanel } from '../../../components/FilterColumn';
import { SearchBox, ToolbarRow } from '../../../components/Toolbar';
import { CellMono, CellStatus, CellTwoLine, CellView, DataTable, type DataTableColumn } from '../../../components/DataTable';
import { Pagination } from '../../../components/Pagination';
import { useAdminQuery } from '../../../hooks/useAdminQuery';
import { DEFAULT_PAGE_SIZE, useListParams } from '../../../hooks/useListParams';
import { humanizeEnum } from '../../../utils/enumLabels';
import { formatNumber } from '../../../utils/format';

const BASE = '/admin/operations/users';
const ROLES = Object.values(UserRoleType);
const isRole = (v: string): v is UserRoleType => (ROLES as string[]).includes(v);

const columns: DataTableColumn<AdminUserSummaryDto>[] = [
  { key: 'id', header: 'ID', width: 80, render: (r) => <CellMono>{r.id}</CellMono> },
  { key: 'user', header: 'User', render: (r) => <CellTwoLine primary={r.fullName} secondary={r.email ?? '—'} /> },
  {
    key: 'roles',
    header: 'Roles',
    width: 220,
    render: (r) => (
      <CellTwoLine primary={r.roles.map(humanizeEnum).join(', ') || '—'} secondary={r.businessName ?? undefined} />
    ),
  },
  { key: 'status', header: 'Status', width: 170, render: (r) => <CellStatus value={r.status} /> },
  { key: 'actions', header: '', width: 74, render: (r) => <CellView to={`${BASE}/${r.id}`} /> },
];

/** Operations / Users / List (US-096): lọc theo vai trò + phân trang qua GET /api/v1/admin/users. */
export const UsersListPage: React.FC = () => {
  const navigate = useNavigate();
  const params = useListParams();
  const roleParam = params.get('role');
  const role = isRole(roleParam) ? roleParam : undefined;
  const page = params.page;

  const query = useAdminQuery(
    () => adminUsersApi.list({ role, page, pageSize: DEFAULT_PAGE_SIZE }),
    [role, page],
    (d) => d.items.length === 0
  );
  const total = query.data?.totalCount ?? null;

  return (
    <ListScreen
      title="Users"
      subtitle={total === null ? '—' : `${formatNumber(total)} users · Driver, Lot owner, Staff, Admin (UserRoles)`}
      filters={
        <FilterColumn>
          <FilterPanel
            title="Role"
            value={role ?? ''}
            onChange={(v) => params.set({ role: v || null })}
            options={[
              { value: '', label: 'All', count: !role ? total : undefined },
              ...ROLES.map((r) => ({ value: r, label: humanizeEnum(r), count: role === r ? total : undefined })),
            ]}
          />
          <FilterPanel
            title="Account status"
            value=""
            disabled
            disabledReason="GET /api/v1/admin/users does not filter by status yet"
            options={[{ value: '', label: 'Any' }, ...Object.values(UserStatus).map((s) => ({ value: s, label: humanizeEnum(s) }))]}
          />
        </FilterColumn>
      }
      toolbar={
        <ToolbarRow>
          <SearchBox
            placeholder="Search name, email, phone, ID…"
            value=""
            onSearch={() => undefined}
            width={360}
            disabled
            disabledReason="GET /api/v1/admin/users has no search parameter yet"
          />
        </ToolbarRow>
      }
    >
      <DataTable
        columns={columns}
        rows={query.status === 'ready' ? query.data?.items ?? [] : []}
        rowKey={(r) => r.id}
        onRowClick={(r) => navigate(`${BASE}/${r.id}`)}
        placeholder={
          <QueryState
            status={query.status}
            error={query.error}
            onRetry={query.refetch}
            copy={{
              emptyTitle: 'No users found',
              emptyMessage: 'Try another role or status filter.',
              errorTitle: 'Could not load users',
              service: 'UserService',
            }}
          />
        }
        footer={
          query.status === 'ready' &&
          query.data && <Pagination page={page} pageSize={query.data.pageSize} totalCount={query.data.totalCount} onPageChange={params.setPage} />
        }
      />
    </ListScreen>
  );
};
