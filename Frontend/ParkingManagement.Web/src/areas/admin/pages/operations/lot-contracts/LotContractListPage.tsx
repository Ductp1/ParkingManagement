import React from 'react';
import { useNavigate } from 'react-router-dom';
import { adminLotContractsApi } from '@/api/adminLotContractsApi';
import { LotContractStatus } from '@/types/adminEnums';
import type { LotContractSummaryDto } from '@/types/adminLotContracts';
import { ListScreen, QueryState } from '../../../components/PageLayout';
import { FilterColumn, FilterPanel } from '../../../components/FilterColumn';
import { SearchBox, ToolbarRow } from '../../../components/Toolbar';
import { CellMono, CellMuted, CellStatus, CellText, CellTwoLine, CellView, DataTable, type DataTableColumn } from '../../../components/DataTable';
import { Pagination } from '../../../components/Pagination';
import { useAdminQuery } from '../../../hooks/useAdminQuery';
import { DEFAULT_PAGE_SIZE, useListParams } from '../../../hooks/useListParams';
import { humanizeEnum } from '../../../utils/enumLabels';
import { formatDate, formatDateTime, formatNumber } from '../../../utils/format';
import { KYB_BASE } from '../kyb/kybFormat';
import { percentText } from './lotContractFormat';

const STATUSES = Object.values(LotContractStatus);
const isStatus = (v: string): v is LotContractStatus => (STATUSES as string[]).includes(v);

const columns: DataTableColumn<LotContractSummaryDto>[] = [
  { key: 'number', header: 'Contract', width: 150, render: (r) => <CellMono>{r.contractNumber}</CellMono> },
  { key: 'lot', header: 'Lot', render: (r) => <CellTwoLine primary={r.lotName} secondary={`Lot #${r.parkingLotId} · ${r.lotCity}`} /> },
  { key: 'owner', header: 'Owner', render: (r) => <CellTwoLine primary={r.businessName} secondary={`OwnerProfile #${r.ownerProfileId}`} /> },
  {
    key: 'terms',
    header: 'Fee / term',
    width: 190,
    render: (r) => (
      <CellTwoLine
        primary={`${percentText(r.commissionRate)} · v${r.latestVersion ?? '—'}`}
        secondary={r.effectiveFrom ? `${formatDate(r.effectiveFrom)} – ${formatDate(r.effectiveTo)}` : undefined}
      />
    ),
  },
  { key: 'status', header: 'Status', width: 110, render: (r) => <CellStatus value={r.status} /> },
  { key: 'updated', header: 'Updated', width: 123, render: (r) => <CellMuted>{formatDateTime(r.updatedAtUtc ?? r.createdAtUtc)}</CellMuted> },
  { key: 'kyb', header: 'KYB', width: 70, render: (r) => <CellText>{`#${r.kybApplicationId}`}</CellText> },
  { key: 'actions', header: '', width: 96, render: (r) => <CellView to={`${KYB_BASE}/${r.kybApplicationId}`} label="Open KYB" /> },
];

/**
 * Operations / Lot Contracts / List (US-116, L0) – tra cứu hợp đồng theo từng bãi. Lập, sửa, ký (= duyệt KYB), hủy đều làm
 * trong trang KYB Review của bãi (design v9), nên mỗi dòng mở hồ sơ KYB.
 */
export const LotContractListPage: React.FC = () => {
  const navigate = useNavigate();
  const params = useListParams();
  const statusParam = params.get('status');
  const status = isStatus(statusParam) ? statusParam : undefined;
  const search = params.get('q');
  const page = params.page;

  const query = useAdminQuery(
    () => adminLotContractsApi.list({ status, search: search || undefined, page, pageSize: DEFAULT_PAGE_SIZE }),
    [status, search, page],
    (d) => d.items.length === 0
  );
  const total = query.data?.totalCount ?? null;

  return (
    <ListScreen
      title="Lot Contracts"
      subtitle={total === null ? '—' : `${formatNumber(total)} contracts`}
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
          <SearchBox placeholder="Search contract number, lot, business, tax code…" width={360} value={search} onSearch={(q) => params.set({ q: q || null })} />
        </ToolbarRow>
      }
    >
      <DataTable
        columns={columns}
        rows={query.status === 'ready' ? query.data?.items ?? [] : []}
        rowKey={(r) => r.id}
        onRowClick={(r) => navigate(`${KYB_BASE}/${r.kybApplicationId}`)}
        placeholder={
          <QueryState
            status={query.status}
            error={query.error}
            onRetry={query.refetch}
            copy={{
              emptyTitle: 'No lot contracts',
              emptyMessage: 'Contracts are created from KYB Review while a lot’s KYB is under review.',
              errorTitle: 'Could not load lot contracts',
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
