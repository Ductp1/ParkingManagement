import React from 'react';
import { useNavigate } from 'react-router-dom';
import { adminPartnerOnboardingApi } from '@/api/adminPartnerOnboardingApi';
import { PartnerApplicationStatus } from '@/types/adminEnums';
import type { PartnerApplicationSummaryDto } from '@/types/adminPartnerOnboarding';
import { ListScreen, QueryState } from '../../../components/PageLayout';
import { FilterColumn, FilterPanel } from '../../../components/FilterColumn';
import { SearchBox, ToolbarRow } from '../../../components/Toolbar';
import { CellMono, CellMuted, CellStatus, CellText, CellTwoLine, CellView, DataTable, type DataTableColumn } from '../../../components/DataTable';
import { Pagination } from '../../../components/Pagination';
import { useAdminQuery } from '../../../hooks/useAdminQuery';
import { DEFAULT_PAGE_SIZE, useListParams } from '../../../hooks/useListParams';
import { humanizeEnum } from '../../../utils/enumLabels';
import { formatDateTime, formatNumber } from '../../../utils/format';
import { PARTNER_BASE as BASE } from './partnerFormat';

const STATUSES = Object.values(PartnerApplicationStatus);
const isStatus = (v: string): v is PartnerApplicationStatus => (STATUSES as string[]).includes(v);

const columns: DataTableColumn<PartnerApplicationSummaryDto>[] = [
  { key: 'code', header: 'Code', width: 120, render: (r) => <CellMono>{r.code}</CellMono> },
  { key: 'business', header: 'Business', render: (r) => <CellTwoLine primary={r.businessName} secondary={`MST ${r.taxCode}`} /> },
  { key: 'contact', header: 'Contact', render: (r) => <CellTwoLine primary={r.fullName} secondary={r.phone} /> },
  { key: 'lots', header: 'Lots', width: 60, render: (r) => <CellText>{r.lotCount}</CellText> },
  {
    key: 'status',
    header: 'Status',
    width: 150,
    render: (r) => <CellTwoLine primary={<CellStatus value={r.status} />} secondary={r.createdOwnerProfileId ? `OwnerProfile #${r.createdOwnerProfileId}` : undefined} />,
  },
  { key: 'reviewer', header: 'Reviewer', width: 90, render: (r) => <CellText>{r.reviewerAdminId ? `Admin #${r.reviewerAdminId}` : '—'}</CellText> },
  { key: 'updated', header: 'Updated', width: 123, render: (r) => <CellMuted>{formatDateTime(r.updatedAtUtc ?? r.createdAtUtc)}</CellMuted> },
  { key: 'actions', header: '', width: 74, render: (r) => <CellView to={`${BASE}/${r.id}`} /> },
];

/** Operations / Partner Onboarding / List (US-116, A1) – lọc trạng thái, tìm kiếm, phân trang qua AdminService. */
export const PartnerListPage: React.FC = () => {
  const navigate = useNavigate();
  const params = useListParams();
  const statusParam = params.get('status');
  const status = isStatus(statusParam) ? statusParam : undefined;
  const search = params.get('q');
  const page = params.page;

  const query = useAdminQuery(
    () => adminPartnerOnboardingApi.list({ status, search: search || undefined, page, pageSize: DEFAULT_PAGE_SIZE }),
    [status, search, page],
    (d) => d.items.length === 0
  );
  const total = query.data?.totalCount ?? null;

  return (
    <ListScreen
      title="Partner Onboarding"
      subtitle={total === null ? '—' : `${formatNumber(total)} applications`}
      filters={
        <FilterColumn>
          <FilterPanel
            title="Status"
            value={status ?? ''}
            onChange={(v) => params.set({ status: v || null })}
            options={[
              { value: '', label: 'All', count: !status ? total : undefined },
              ...STATUSES.map((s) => ({ value: s, label: humanizeEnum(s), count: status === s ? total : undefined })),
            ]}
          />
        </FilterColumn>
      }
      toolbar={
        <ToolbarRow>
          <SearchBox placeholder="Search code, business name, tax code, email, phone…" width={360} value={search} onSearch={(q) => params.set({ q: q || null })} />
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
              emptyTitle: 'No partner applications',
              emptyMessage: 'New applications from the public “Become a partner” form appear here.',
              errorTitle: 'Could not load applications',
              service: 'AdminService',
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
