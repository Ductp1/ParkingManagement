import React, { useState } from 'react';
import { adminSanctionsApi } from '@/api/adminSanctionsApi';
import { SanctionLevel, SanctionStatus } from '@/types/adminEnums';
import type { SanctionListQuery, SanctionSummaryDto } from '@/types/adminSanctions';
import { ListScreen, QueryState } from '../../../components/PageLayout';
import { FilterColumn, FilterPanel } from '../../../components/FilterColumn';
import { SearchBox, ToolbarRow } from '../../../components/Toolbar';
import { CellMono, CellMuted, CellText, CellTwoLine, DataTable, type DataTableColumn } from '../../../components/DataTable';
import { Pagination } from '../../../components/Pagination';
import { AdminButton } from '../../../components/AdminButton';
import { KpiStrip, KpiTile } from '../../../components/KpiTile';
import { StatusIndicator } from '../../../components/StatusIndicator';
import { useAdminQuery } from '../../../hooks/useAdminQuery';
import { DEFAULT_PAGE_SIZE, useListParams } from '../../../hooks/useListParams';
import { formatDateTime, formatNumber, formatVnd } from '../../../utils/format';
import { IssueSanctionDialog } from './IssueSanctionDialog';
import { SanctionDetailDrawer } from './SanctionDetailDrawer';
import { SYNC_TARGETS, ictDayStartUtc, levelLabel, levelTone, positiveInt, scopeText } from './sanctionFormat';

const STATUS_OPTIONS = [
  { value: '', label: 'All' },
  { value: SanctionStatus.Active, label: 'Active' },
  { value: SanctionStatus.Expired, label: 'Expired' },
  { value: SanctionStatus.Revoked, label: 'Revoked' },
];

const LEVEL_FILTERS = [
  { value: '', label: 'Any' },
  { value: SanctionLevel.Warning, label: 'L1 · Warning' },
  { value: SanctionLevel.RankingPenalty, label: 'L2 · Ranking penalty' },
  { value: SanctionLevel.TemporarySuspension, label: 'L3 · Suspension / lock' },
  { value: SanctionLevel.PermanentBan, label: 'L4 · Termination' },
];

const isStatus = (v: string) => (Object.values(SanctionStatus) as string[]).includes(v);
const isLevel = (v: string) => (Object.values(SanctionLevel) as string[]).includes(v);

/** Ô chọn ngày (giờ ICT). */
const DayBox: React.FC<{ label: string; value: string; onChange: (value: string) => void }> = ({ label, value, onChange }) => (
  <label className="flex h-8 items-center gap-2 rounded-[2px] border border-(--admin-border-default) bg-(--admin-surface-card) px-2.5 text-(--admin-text-secondary) adm-tr-colors focus-within:border-(--admin-brand-navy-900)">
    <span className="adm-type-body-regular-12">{label}</span>
    <input
      type="date"
      aria-label={`Issued ${label.toLowerCase()} (ICT)`}
      value={value}
      onChange={(e) => onChange(e.target.value)}
      className="adm-type-body-regular-12 bg-transparent text-(--admin-text-primary) outline-none"
    />
  </label>
);

/** Đồng bộ từng service gọn trong 1 ô: chỉ hiện service có liên quan. */
const SyncCell: React.FC<{ row: SanctionSummaryDto }> = ({ row }) => {
  const relevant = SYNC_TARGETS.filter((t) => row[t.key] !== 'NotRequired');
  if (relevant.length === 0) return <CellMuted>—</CellMuted>;
  return (
    <span className="flex flex-col gap-0.5">
      {relevant.map((t) => (
        <span key={t.key} className="adm-type-body-small flex items-center gap-1">
          <span className="w-12 text-(--admin-text-secondary)">{t.short}</span>
          <StatusIndicator value={row[t.key]} />
        </span>
      ))}
    </span>
  );
};

const deadlines = (r: SanctionSummaryDto) => (
  <CellTwoLine
    primary={r.correctionDueAtUtc ? `Fix by ${formatDateTime(r.correctionDueAtUtc)}` : '—'}
    secondary={r.appealDeadlineUtc ? `Appeal by ${formatDateTime(r.appealDeadlineUtc)}` : undefined}
  />
);

const columns: DataTableColumn<SanctionSummaryDto>[] = [
  { key: 'id', header: '#', width: 56, render: (r) => <CellMono>{r.id}</CellMono> },
  { key: 'scope', header: 'Owner / scope', width: 130, render: (r) => <CellTwoLine primary={`Owner #${r.ownerProfileId}`} secondary={scopeText(r)} /> },
  {
    key: 'level',
    header: 'Level',
    width: 190,
    render: (r) => <StatusIndicator label={levelLabel(r.level, r.parkingLotId)} tone={levelTone(r.level)} />,
  },
  { key: 'reason', header: 'Reason', render: (r) => <CellText>{r.reason}</CellText> },
  {
    key: 'period',
    header: 'Period (ICT)',
    width: 150,
    render: (r) => <CellTwoLine primary={formatDateTime(r.startsAtUtc)} secondary={r.endsAtUtc ? `→ ${formatDateTime(r.endsAtUtc)}` : '→ no end'} />,
  },
  { key: 'deadlines', header: 'Deadlines', width: 170, render: deadlines },
  { key: 'penalty', header: 'Penalty', width: 110, align: 'right', render: (r) => <CellText>{r.penaltyAmount === null ? '—' : formatVnd(r.penaltyAmount)}</CellText> },
  { key: 'sync', header: 'Sync', width: 140, render: (r) => <SyncCell row={r} /> },
  { key: 'status', header: 'Status', width: 90, render: (r) => <StatusIndicator value={r.status} /> },
];

/**
 * Support & Sanctions / Sanctions (US-097): chế tài SLA 4 cấp – KPI, danh sách lọc trên URL, chi tiết (?sanction=),
 * áp chế tài, gỡ, đồng bộ lại. Nguồn: AdminService /api/v1/admin/sanctions.
 */
export const SanctionsListPage: React.FC = () => {
  const params = useListParams();
  const page = params.page;
  const [issueOpen, setIssueOpen] = useState(false);
  const [version, setVersion] = useState(0);
  const reload = () => setVersion((v) => v + 1);

  const status = isStatus(params.get('status')) ? params.get('status') : '';
  const level = isLevel(params.get('level')) ? params.get('level') : '';
  const fromDay = params.get('from');
  const toDay = params.get('to');
  const filter: SanctionListQuery = {
    status,
    level,
    ownerProfileId: positiveInt(params.get('ownerProfileId')),
    parkingLotId: positiveInt(params.get('parkingLotId')),
    search: params.get('q'),
    from: ictDayStartUtc(fromDay),
    to: ictDayStartUtc(toDay, 1),
    syncFailed: params.get('syncFailed') === 'true',
  };
  const filterKey = JSON.stringify(filter);
  const selectedId = positiveInt(params.get('sanction'));
  const select = (id: number | null) => params.set({ sanction: id, page: page <= 1 ? null : page });

  const stats = useAdminQuery(() => adminSanctionsApi.stats(), [version]);
  const query = useAdminQuery(
    () => adminSanctionsApi.list(filter, page, DEFAULT_PAGE_SIZE),
    [filterKey, page, version],
    (d) => d.items.length === 0
  );
  const s = stats.data;
  const hasFilter = Object.values(filter).some((v) => v !== null && v !== undefined && v !== '' && v !== false);
  const kpi = (label: string, value: number | undefined, note?: string, onClick?: () => void) => (
    <button
      type="button"
      data-testid={`kpi-${label}`}
      onClick={onClick}
      disabled={!onClick}
      aria-label={`${label}: ${value ?? 'loading'}`}
      className="flex min-w-[150px] flex-1 cursor-pointer border-0 bg-transparent p-0 text-left focus-visible:outline-2 focus-visible:outline-(--admin-brand-navy-900) disabled:cursor-default"
    >
      <KpiTile label={label} value={formatNumber(value)} loading={stats.status === 'loading'} count={{ value, format: formatNumber }} delta={note} />
    </button>
  );

  return (
    <ListScreen
      title="Sanctions"
      subtitle={query.data ? `${formatNumber(query.data.totalCount)} sanctions · SLA 4 levels · newest first` : 'SLA sanctions on owners / lots · 4 levels'}
      actions={
        <AdminButton variant="onNavy" icon="plus" onClick={() => setIssueOpen(true)}>
          Issue sanction
        </AdminButton>
      }
      top={
        <KpiStrip>
          {kpi('Active', s?.activeTotal, 'In effect now', () => params.set({ status: SanctionStatus.Active, level: null }))}
          {kpi('L1 Warning', s?.warning, undefined, () => params.set({ status: SanctionStatus.Active, level: SanctionLevel.Warning }))}
          {kpi('L2 Ranking', s?.rankingPenalty, undefined, () => params.set({ status: SanctionStatus.Active, level: SanctionLevel.RankingPenalty }))}
          {kpi('L3 Suspension', s?.temporarySuspension, undefined, () =>
            params.set({ status: SanctionStatus.Active, level: SanctionLevel.TemporarySuspension })
          )}
          {kpi('L4 Termination', s?.permanentBan, undefined, () => params.set({ status: SanctionStatus.Active, level: SanctionLevel.PermanentBan }))}
          {kpi('Sync failed', s?.syncFailed, s ? `${formatNumber(s.syncPending)} pending` : undefined, () => params.set({ syncFailed: 'true' }))}
          {kpi('Ending ≤ 7 days', s?.endingWithin7Days)}
        </KpiStrip>
      }
      filters={
        <FilterColumn>
          <FilterPanel title="Status" value={status} onChange={(v) => params.set({ status: v || null })} options={STATUS_OPTIONS} />
          <FilterPanel title="Level" value={level} onChange={(v) => params.set({ level: v || null })} options={LEVEL_FILTERS} />
        </FilterColumn>
      }
      toolbar={
        <ToolbarRow>
          <SearchBox placeholder="Search #id, owner, lot, reason…" width={300} value={params.get('q')} onSearch={(q) => params.set({ q: q || null })} />
          <DayBox label="From" value={fromDay} onChange={(v) => params.set({ from: v || null })} />
          <DayBox label="To" value={toDay} onChange={(v) => params.set({ to: v || null })} />
          {filter.ownerProfileId !== null && (
            <AdminButton variant="secondary" onClick={() => params.set({ ownerProfileId: null })} aria-label="Remove owner filter">
              Owner #{filter.ownerProfileId} ✕
            </AdminButton>
          )}
          {filter.parkingLotId !== null && (
            <AdminButton variant="secondary" onClick={() => params.set({ parkingLotId: null })} aria-label="Remove lot filter">
              Lot #{filter.parkingLotId} ✕
            </AdminButton>
          )}
          {filter.syncFailed && (
            <AdminButton variant="secondary" onClick={() => params.set({ syncFailed: null })} aria-label="Remove sync failed filter">
              Sync failed ✕
            </AdminButton>
          )}
          {hasFilter && (
            <AdminButton
              variant="ghost"
              className="adm-motion-fade"
              onClick={() => params.set({ status: null, level: null, ownerProfileId: null, parkingLotId: null, q: null, from: null, to: null, syncFailed: null })}
            >
              Clear filters
            </AdminButton>
          )}
        </ToolbarRow>
      }
    >
      <DataTable
        animateRows
        busy={query.isFetching && query.data !== null}
        columns={columns}
        rows={query.data?.items ?? []}
        rowKey={(r) => r.id}
        selectedKey={selectedId}
        onRowClick={(r) => select(r.id)}
        placeholder={
          <QueryState
            status={query.status}
            error={query.error}
            onRetry={query.refetch}
            copy={{
              emptyTitle: 'No sanctions',
              emptyMessage: hasFilter ? 'No sanction matches these filters.' : 'No sanction has been issued yet.',
              errorTitle: 'Could not load sanctions',
              service: 'AdminService',
            }}
          />
        }
        footer={query.data && <Pagination page={page} pageSize={query.data.pageSize} totalCount={query.data.totalCount} onPageChange={params.setPage} />}
      />
      {selectedId !== null && <SanctionDetailDrawer key={selectedId} id={selectedId} onClose={() => select(null)} onChanged={reload} />}
      {issueOpen && (
        <IssueSanctionDialog
          preset={filter.ownerProfileId !== null ? { ownerProfileId: filter.ownerProfileId } : undefined}
          onClose={() => setIssueOpen(false)}
          onIssued={(result) => {
            reload();
            if (result.sanctions.length === 1) select(result.sanctions[0].id);
          }}
        />
      )}
    </ListScreen>
  );
};
