import React, { useMemo } from 'react';
import { useNavigate } from 'react-router-dom';
import { adminLotsApi } from '@/api/adminLotsApi';
import type { AdminLotOccupancyRow, AdminLotSummaryRow } from '@/types/adminLots';
import { ListScreen } from '../../../components/PageLayout';
import { Banner } from '../../../components/Banner';
import { FilterColumn, FilterPanel } from '../../../components/FilterColumn';
import { SearchBox, ToolbarRow, ToolbarSelect } from '../../../components/Toolbar';
import { CellLink, CellText, CellTwoLine, DataTable, type DataTableColumn } from '../../../components/DataTable';
import { StatusIndicator } from '../../../components/StatusIndicator';
import { StateBlock } from '../../../components/StateBlock';
import { Pagination } from '../../../components/Pagination';
import { useAdminQuery } from '../../../hooks/useAdminQuery';
import { DEFAULT_PAGE_SIZE, useListParams } from '../../../hooks/useListParams';
import { humanizeEnum } from '../../../utils/enumLabels';
import { formatNumber, formatPercent, formatTimeIct } from '../../../utils/format';
import { LOT_FLAGS, LOT_STATUSES, countBy, distinct, filterLots, type LotListFilters } from './lotListFilters';

const BASE = '/admin/operations/parking-lots';

/** Xe đang đỗ / sức chứa: sức chứa GateService, chưa cấu hình thì dùng tổng số chỗ của bãi. */
const OccupancyCell: React.FC<{ lot: AdminLotSummaryRow; row: AdminLotOccupancyRow | undefined }> = ({ lot, row }) => {
  const parked = row?.activeSessions ?? 0;
  const capacity = row?.capacity ?? lot.totalSlots;
  return (
    <CellTwoLine
      primary={`${formatNumber(parked)} / ${formatNumber(capacity)}`}
      secondary={capacity > 0 ? `${formatPercent((parked / capacity) * 100)} full` : undefined}
    />
  );
};

const FlagsCell: React.FC<{ lot: AdminLotSummaryRow }> = ({ lot }) => (
  <span className="flex flex-col gap-0.5">
    {lot.isEmergencyStopped && <StatusIndicator tone="danger" label="Emergency stopped" />}
    {lot.instantBookingPaused && <StatusIndicator tone="warning" label="Instant booking paused" />}
  </span>
);

/**
 * Operations / Parking Lots / List (US-081): mọi bãi từ lots-summary (ParkingService) + xe đang đỗ (GateService occupancy).
 * Lọc, tìm, phân trang phía client; bộ lọc giữ trên URL. Nguồn occupancy lỗi → ẩn cột, không hiện số 0.
 */
export const ParkingLotsListPage: React.FC = () => {
  const navigate = useNavigate();
  const params = useListParams();
  const filters: LotListFilters = {
    status: params.get('status'),
    flag: params.get('flag'),
    city: params.get('city'),
    tier: params.get('tier'),
    search: params.get('q'),
  };
  const page = params.page;

  const summary = useAdminQuery(() => adminLotsApi.lotsSummary(), [], (d) => d.lots.length === 0);
  const occupancy = useAdminQuery(() => adminLotsApi.occupancy(), []);

  const lots = useMemo(() => summary.data?.lots ?? [], [summary.data]);
  const occupancyById = useMemo(() => new Map((occupancy.data?.lots ?? []).map((r) => [r.parkingLotId, r])), [occupancy.data]);
  const occupancyFailed = ['error', 'forbidden', 'unavailable'].includes(occupancy.status);

  const filtered = filterLots(lots, filters);
  const rows = filtered.slice((page - 1) * DEFAULT_PAGE_SIZE, page * DEFAULT_PAGE_SIZE);
  const statusCounts = countBy(lots, filters, 'status', LOT_STATUSES);
  const flagCounts = countBy(lots, filters, 'flag', LOT_FLAGS.map((f) => f.value));
  const cities = distinct(lots.map((l) => l.city));
  const tiers = distinct(lots.map((l) => String(l.integrationTier)));
  const ready = summary.status === 'ready';
  const hasFilter = Object.values(filters).some((v) => v !== '');

  const columns: DataTableColumn<AdminLotSummaryRow>[] = [
    // Không cố định độ rộng cột: bảng tự chia theo nội dung. Tên bãi tối thiểu 160px để không xuống dòng từng từ;
    // màn hẹp (sidebar + cột lọc cố định, audit P1-11) thì bảng cuộn ngang bên trong.
    {
      key: 'lot',
      header: 'Lot',
      render: (r) => (
        <span className="inline-block min-w-[160px]">
          <CellLink to={`${BASE}/${r.parkingLotId}`}>{r.name}</CellLink>
        </span>
      ),
    },
    { key: 'area', header: 'City / district', render: (r) => <CellTwoLine primary={r.city} secondary={r.district ?? undefined} /> },
    ...(occupancyFailed
      ? []
      : [
          {
            key: 'occupancy',
            header: 'Parked / capacity',
            render: (r: AdminLotSummaryRow) =>
              occupancy.status === 'ready' ? <OccupancyCell lot={r} row={occupancyById.get(r.parkingLotId)} /> : <CellText>…</CellText>,
          },
        ]),
    { key: 'slots', header: 'Total slots', align: 'right', render: (r) => <CellText>{formatNumber(r.totalSlots)}</CellText> },
    { key: 'tier', header: 'Integration', render: (r) => <CellText>{humanizeEnum(String(r.integrationTier))}</CellText> },
    { key: 'status', header: 'Status', render: (r) => <StatusIndicator value={String(r.status)} /> },
    { key: 'flags', header: 'Flags', render: (r) => <FlagsCell lot={r} /> },
  ];

  const placeholder = (() => {
    switch (summary.status) {
      case 'loading':
        return <StateBlock type="loading" bare />;
      case 'empty':
        return <StateBlock type="empty" title="No parking lots yet" message="Lots appear here after an owner registers them." bare />;
      case 'forbidden':
        return <StateBlock type="noPermission" message="Only administrators can view all parking lots." bare />;
      case 'error':
      case 'unavailable':
        return (
          <StateBlock
            type="error"
            title="Could not load parking lots"
            message={summary.error?.problem ? summary.error.message : 'The lot list could not be loaded. Try again.'}
            onRetry={summary.refetch}
            bare
          />
        );
      default:
        return filtered.length === 0 ? (
          <StateBlock type="empty" title="No lots match these filters" message="Change or clear the filters to see more lots." bare />
        ) : null;
    }
  })();

  const lotCount = summary.data?.lots.length ?? 0;

  return (
    <ListScreen
      title="Parking Lots"
      subtitle={
        ready && summary.data
          ? `${formatNumber(lotCount)} ${lotCount === 1 ? 'lot' : 'lots'} · updated ${formatTimeIct(summary.data.asOfUtc)}`
          : undefined
      }
      top={
        ready && occupancyFailed ? (
          <Banner tone="warning">Live occupancy could not be loaded, so the parked / capacity column is hidden.</Banner>
        ) : undefined
      }
      filters={
        <FilterColumn>
          <FilterPanel
            title="Lot status"
            value={filters.status}
            onChange={(v) => params.set({ status: v || null })}
            options={[
              { value: '', label: 'All', count: ready ? filterLots(lots, filters, 'status').length : null },
              ...LOT_STATUSES.map((s) => ({ value: s, label: humanizeEnum(s), count: ready ? statusCounts[s] : null })),
            ]}
          />
          <FilterPanel
            title="Flags"
            value={filters.flag}
            onChange={(v) => params.set({ flag: v || null })}
            options={[
              { value: '', label: 'Any', count: ready ? filterLots(lots, filters, 'flag').length : null },
              ...LOT_FLAGS.map((f) => ({ value: f.value, label: f.label, count: ready ? flagCounts[f.value] : null })),
            ]}
          />
        </FilterColumn>
      }
      toolbar={
        <ToolbarRow>
          <SearchBox placeholder="Search lot, city or district…" value={filters.search} onSearch={(q) => params.set({ q: q || null })} />
          <ToolbarSelect
            label="City"
            value={filters.city}
            onChange={(v) => params.set({ city: v || null })}
            options={[{ value: '', label: 'All' }, ...cities.map((c) => ({ value: c, label: c }))]}
          />
          <ToolbarSelect
            label="Integration"
            value={filters.tier}
            width={170}
            onChange={(v) => params.set({ tier: v || null })}
            options={[{ value: '', label: 'All' }, ...tiers.map((t) => ({ value: t, label: humanizeEnum(t) }))]}
          />
          {hasFilter && (
            <button
              type="button"
              onClick={() => params.set({ status: null, flag: null, city: null, tier: null, q: null })}
              className="adm-type-link-default adm-tr-colors h-8 px-2 text-(--admin-brand-navy-900) hover:underline"
            >
              Clear filters
            </button>
          )}
        </ToolbarRow>
      }
    >
      <DataTable
        columns={columns}
        rows={ready ? rows : []}
        rowKey={(r) => r.parkingLotId}
        onRowClick={(r) => navigate(`${BASE}/${r.parkingLotId}`)}
        busy={summary.isFetching && lots.length > 0}
        placeholder={placeholder}
        footer={
          ready && filtered.length > 0 && <Pagination page={page} pageSize={DEFAULT_PAGE_SIZE} totalCount={filtered.length} onPageChange={params.setPage} />
        }
      />
    </ListScreen>
  );
};
