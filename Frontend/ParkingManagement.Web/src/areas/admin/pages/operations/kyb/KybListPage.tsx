import React from 'react';
import { useNavigate } from 'react-router-dom';
import { adminKybApi } from '@/api/adminKybApi';
import { KybStatus } from '@/types/adminEnums';
import type { AdminKybApplicationSummaryDto } from '@/types/adminKyb';
import { ListScreen, QueryState } from '../../../components/PageLayout';
import { FilterColumn, FilterPanel } from '../../../components/FilterColumn';
import { SearchBox, ToolbarRow } from '../../../components/Toolbar';
import { CellMono, CellMuted, CellStatus, CellText, CellTwoLine, CellView, DataTable, type DataTableColumn } from '../../../components/DataTable';
import { Pagination } from '../../../components/Pagination';
import { useAdminQuery } from '../../../hooks/useAdminQuery';
import { DEFAULT_PAGE_SIZE, useListParams } from '../../../hooks/useListParams';
import { humanizeEnum } from '../../../utils/enumLabels';
import { formatDateTime, formatNumber } from '../../../utils/format';
import { KYB_BASE as BASE, kybCode, surveyText } from './kybFormat';

/** Thứ tự theo Figma: All, Draft, Submitted, Under review, Needs more info, Approved, Rejected. */
const STATUS_ORDER: KybStatus[] = [
  KybStatus.Draft,
  KybStatus.Submitted,
  KybStatus.UnderReview,
  KybStatus.NeedsMoreInfo,
  KybStatus.Approved,
  KybStatus.Rejected,
];

const isKybStatus = (v: string): v is KybStatus => (STATUS_ORDER as string[]).includes(v);

const columns: DataTableColumn<AdminKybApplicationSummaryDto>[] = [
  { key: 'id', header: 'KYB ID', width: 105, render: (r) => <CellMono>{kybCode(r.id)}</CellMono> },
  {
    key: 'lot',
    header: 'Lot / owner',
    render: (r) => <CellTwoLine primary={r.parkingLotName} secondary={r.ownerBusinessName ?? `OwnerProfile #${r.ownerProfileId}`} />,
  },
  { key: 'address', header: 'City', width: 160, render: (r) => <CellText>{r.parkingLotAddress}</CellText> },
  { key: 'submitted', header: 'Submitted', width: 123, render: (r) => <CellMuted>{formatDateTime(r.submittedAtUtc)}</CellMuted> },
  { key: 'survey', header: 'Field survey', width: 114, render: (r) => <CellText>{surveyText(r.fieldSurveyRequired, r.fieldSurveyAtUtc)}</CellText> },
  {
    key: 'reviewer',
    header: 'Reviewer',
    width: 90,
    render: (r) => <CellText>{r.reviewedByUserId ? `Admin #${r.reviewedByUserId}` : '—'}</CellText>,
  },
  { key: 'status', header: 'Status', width: 123, render: (r) => <CellStatus value={r.status} /> },
  { key: 'actions', header: '', width: 74, render: (r) => <CellView to={`${BASE}/${r.id}`} /> },
];

/** Operations / KYB Review / List (US-055) – lọc trạng thái, tìm kiếm, phân trang qua API. */
export const KybListPage: React.FC = () => {
  const navigate = useNavigate();
  const params = useListParams();
  const statusParam = params.get('status');
  const status = isKybStatus(statusParam) ? statusParam : undefined;
  const search = params.get('q');
  const page = params.page;

  const query = useAdminQuery(
    () => adminKybApi.list({ status, search: search || undefined, page, pageSize: DEFAULT_PAGE_SIZE }),
    [status, search, page],
    (d) => d.items.length === 0
  );
  const total = query.data?.totalCount ?? null;

  const subtitle =
    total === null ? '—' : status ? `${formatNumber(total)} ${humanizeEnum(status).toLowerCase()}` : `${formatNumber(total)} lot verifications`;

  return (
    <ListScreen
      title="KYB Review"
      subtitle={subtitle}
      filters={
        <FilterColumn>
          <FilterPanel
            title="KYB status"
            value={status ?? ''}
            onChange={(v) => params.set({ status: v || null })}
            options={[
              { value: '', label: 'All', count: !status ? total : undefined },
              ...STATUS_ORDER.map((s) => ({ value: s, label: humanizeEnum(s), count: status === s ? total : undefined })),
            ]}
          />
        </FilterColumn>
      }
      toolbar={
        <ToolbarRow>
          <SearchBox placeholder="Search KYB ID, lot, owner…" value={search} onSearch={(q) => params.set({ q: q || null })} />
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
              emptyTitle: 'No KYB applications',
              emptyMessage: 'Applications appear when an owner submits a lot for verification (US-054).',
              errorTitle: 'Could not load KYB applications',
              service: 'ParkingService',
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
