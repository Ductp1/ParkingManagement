import React, { useMemo } from 'react';
import { Link, useLocation } from 'react-router-dom';
import type { AdminQueryStatus } from '@/types/adminCommon';
import type { AdminCommissionTotalsDto, CommissionRateSource } from '@/types/adminCommission';
import { AdminTitleBar } from '../../../layout/AdminTitleBar';
import { AdminButton } from '../../../components/AdminButton';
import { Banner } from '../../../components/Banner';
import { CellAmount, CellLink, CellMuted, CellText, CellTwoLine, DataTable, type DataTableColumn } from '../../../components/DataTable';
import { KpiTile } from '../../../components/KpiTile';
import { Pagination } from '../../../components/Pagination';
import { Panel } from '../../../components/Panel';
import { QueryState } from '../../../components/PageLayout';
import { StateBlock } from '../../../components/StateBlock';
import { SubTabs } from '../../../components/SubTabs';
import { SearchBox, ToolbarRow, ToolbarSelect } from '../../../components/Toolbar';
import { DEFAULT_PAGE_SIZE, useListParams } from '../../../hooks/useListParams';
import { EMPTY, formatDateTimeIct, formatNumber, formatVnd } from '../../../utils/format';
import { CommissionDailyChart } from './CommissionDailyChart';
import { downloadCsv, formatRate, lotContractLink, lotCsv, ownerCsv, partnerLink, sourceCsv } from './commissionFormat';
import { displayDate, MAX_RANGE_DAYS, PERIOD_OPTIONS, resolveRange, type CommissionRange } from './commissionPeriod';
import {
  buildLotRows,
  buildOwnerRows,
  buildSourceRows,
  isLotSort,
  isRateSource,
  LOT_SORT_OPTIONS,
  REASON_LABEL,
  sortLotRows,
  sortOwnerRows,
  SOURCE_LABEL,
  type CommissionLotRow,
  type CommissionOwnerRow,
  type CommissionSourceRow,
} from './commissionRows';
import { useCommissionData, type SourceQuery } from './useCommissionData';
import { isQueryFailure } from '../../../utils/queryStatus';

export type CommissionTab = 'lots' | 'owners' | 'sources';

export const COMMISSION_BASE = '/admin/finance/commission';
const TAB_PATH: Record<CommissionTab, string> = { lots: '', owners: '/owners', sources: '/sources' };
const TAB_LABEL: Record<CommissionTab, string> = { lots: 'By lot', owners: 'By owner', sources: 'By rate source' };
const SETTINGS_LINK = '/admin/access/system-settings/DEFAULT_COMMISSION_RATE';
const PENDING_HINT = 'Waiting to be calculated – not zero';

// ===== Ô nhỏ =====

const SourceBadge: React.FC<{ source: CommissionRateSource }> = ({ source }) => (
  <span
    className={`adm-type-body-small inline-block rounded-[2px] border px-1.5 ${
      source === 'LotContract' ? 'border-(--admin-brand-navy-800) text-(--admin-brand-navy-900)' : 'border-(--admin-border-default) text-(--admin-text-secondary)'
    }`}
  >
    {SOURCE_LABEL[source]}
  </span>
);

const Pending: React.FC<{ count: number | null | undefined }> = ({ count }) =>
  count === null || count === undefined ? (
    <CellMuted>{EMPTY}</CellMuted>
  ) : count > 0 ? (
    <span className="adm-type-body-strong whitespace-nowrap text-(--admin-status-warning-text)" title={PENDING_HINT}>
      {formatNumber(count)} pending
    </span>
  ) : (
    <CellMuted>0</CellMuted>
  );

const Money: React.FC<{ value: number | null | undefined }> = ({ value }) =>
  value === null || value === undefined ? <CellMuted>{EMPTY}</CellMuted> : <CellAmount>{formatVnd(value)}</CellAmount>;

// ===== Cột bảng =====

const lotColumns = (ratesLoaded: boolean): DataTableColumn<CommissionLotRow>[] => [
  {
    key: 'lot',
    header: 'Lot',
    render: (r) => (
      <CellTwoLine
        primary={r.lotName}
        secondary={r.known ? [r.district, `#${r.parkingLotId}`].filter(Boolean).join(' · ') : ratesLoaded ? 'Not in the rate list' : `#${r.parkingLotId}`}
      />
    ),
  },
  {
    key: 'owner',
    header: 'Owner',
    render: (r) => (
      <CellTwoLine primary={r.ownerName ?? EMPTY} secondary={r.ownerProfileId !== null ? `Owner #${r.ownerProfileId}` : undefined} />
    ),
  },
  {
    key: 'rate',
    header: 'Current rate',
    width: 190,
    render: (r) => (
      <span className="flex flex-col gap-0.5">
        <span className="flex items-center gap-1.5">
          <span className="adm-type-body-strong text-(--admin-text-primary)">{formatRate(r.currentRate)}</span>
          {r.source && <SourceBadge source={r.source} />}
        </span>
        {r.reason && <span className="adm-type-body-small text-(--admin-text-secondary)">{REASON_LABEL[r.reason]}</span>}
      </span>
    ),
  },
  {
    key: 'contract',
    header: 'Contract',
    width: 170,
    render: (r) =>
      r.contractNumber ? (
        <CellLink to={lotContractLink(r.contractNumber)}>
          {r.contractNumber}
          {r.contractVersion !== null && ` · v${r.contractVersion}`}
        </CellLink>
      ) : (
        <CellMuted>{EMPTY}</CellMuted>
      ),
  },
  { key: 'gross', header: 'GMV', width: 120, align: 'right', render: (r) => <Money value={r.money?.gross} /> },
  { key: 'net', header: 'Net', width: 120, align: 'right', render: (r) => <Money value={r.money?.net} /> },
  { key: 'commission', header: 'Commission', width: 120, align: 'right', render: (r) => <Money value={r.money?.commission} /> },
  { key: 'effective', header: 'Effective rate', width: 110, align: 'right', render: (r) => <CellMuted>{formatRate(r.money?.effectiveRate)}</CellMuted> },
  { key: 'pending', header: 'Pending', width: 100, align: 'right', render: (r) => <Pending count={r.money?.pendingCount} /> },
];

const ownerColumns: DataTableColumn<CommissionOwnerRow>[] = [
  {
    key: 'owner',
    header: 'Owner',
    render: (r) => {
      const link = partnerLink(r.ownerName);
      return (
        <CellTwoLine
          primary={link ? <CellLink to={link}>{r.ownerName}</CellLink> : r.ownerName ?? EMPTY}
          secondary={`Owner #${r.ownerProfileId}`}
        />
      );
    },
  },
  { key: 'lots', header: 'Lots', width: 80, align: 'right', render: (r) => <CellMuted>{formatNumber(r.lotCount)}</CellMuted> },
  { key: 'gross', header: 'GMV', width: 130, align: 'right', render: (r) => <Money value={r.money?.gross} /> },
  { key: 'net', header: 'Net', width: 130, align: 'right', render: (r) => <Money value={r.money?.net} /> },
  { key: 'commission', header: 'Commission', width: 130, align: 'right', render: (r) => <Money value={r.money?.commission} /> },
  { key: 'effective', header: 'Effective rate', width: 110, align: 'right', render: (r) => <CellMuted>{formatRate(r.money?.effectiveRate)}</CellMuted> },
  { key: 'pending', header: 'Pending', width: 100, align: 'right', render: (r) => <Pending count={r.money?.pendingCount} /> },
];

const sourceColumns: DataTableColumn<CommissionSourceRow>[] = [
  { key: 'source', header: 'Rate source', render: (r) => <CellText>{SOURCE_LABEL[r.source]}</CellText> },
  { key: 'lots', header: 'Lots using it', width: 130, align: 'right', render: (r) => <CellMuted>{formatNumber(r.lotCount)}</CellMuted> },
  { key: 'tx', header: 'Payments calculated', width: 150, align: 'right', render: (r) => <CellMuted>{formatNumber(r.stats?.transactionCount)}</CellMuted> },
  { key: 'base', header: 'Base amount', width: 130, align: 'right', render: (r) => <Money value={r.stats?.baseAmount} /> },
  { key: 'refunded', header: 'Refunded', width: 120, align: 'right', render: (r) => <Money value={r.stats?.refundedAmount} /> },
  { key: 'commission', header: 'Commission', width: 130, align: 'right', render: (r) => <Money value={r.stats?.commissionAmount} /> },
  { key: 'effective', header: 'Effective rate', width: 110, align: 'right', render: (r) => <CellMuted>{formatRate(r.stats?.effectiveRate)}</CellMuted> },
];

// ===== KPI =====

const KpiGrid: React.FC<{ totals: AdminCommissionTotalsDto | null; status: SourceQuery<unknown>['status'] }> = ({ totals, status }) => {
  const loading = status === 'loading';
  const muted = totals === null && !loading;
  const tile = (label: string, value: number | null | undefined, format: (n: number) => string, note?: React.ReactNode) => (
    <KpiTile
      label={label}
      value={value === null || value === undefined ? EMPTY : format(value)}
      count={value === null || value === undefined ? undefined : { value, format }}
      loading={loading}
      muted={muted}
      note={note}
    />
  );
  const pending = totals?.pendingCount ?? 0;
  const pendingNote = pending > 0 && (
    <span className="adm-type-kpi-delta text-(--admin-status-warning-text)" title={PENDING_HINT}>
      {PENDING_HINT}
    </span>
  );

  // 6 tile: 2 cột (< 768) → 3 cột (≥ 768 md) → 6 cột (≥ 1280 xl); lần đầu hiện so le (T-801).
  return (
    <section aria-label="Commission figures for the period" className="adm-stagger-children grid grid-cols-2 gap-2 md:grid-cols-3 xl:grid-cols-6">
      {tile('GMV', totals?.grossAmount, formatVnd)}
      {tile('Refunded', totals?.refundedAmount, formatVnd)}
      {tile('Net revenue', totals?.netAmount, formatVnd)}
      {tile('Commission', totals?.commissionAmount, formatVnd, pending > 0 && (
        <span className="adm-type-kpi-delta text-(--admin-status-warning-text)" title={PENDING_HINT}>
          Incomplete · {formatNumber(pending)} payments still pending
        </span>
      ))}
      {tile('Effective rate', totals?.effectiveRate, formatRate)}
      <div
        data-testid="kpi-pending"
        data-tone={pending > 0 ? 'warning' : 'default'}
        title={pending > 0 ? PENDING_HINT : undefined}
        className={`flex min-w-0 ${pending > 0 ? 'outline-2 -outline-offset-2 outline-(--admin-status-warning) outline' : ''}`}
      >
        {tile('Pending payments', totals?.pendingCount, formatNumber, pendingNote)}
      </div>
    </section>
  );
};

// ===== Trang =====

const sourceStatus = (q: SourceQuery<unknown>): AdminQueryStatus => (q.status === 'idle' ? 'empty' : q.status);

/**
 * Finance / Commission (US-079): hoa hồng nền tảng do hệ thống tự tính. Hai nguồn độc lập ghép theo parkingLotId:
 * tỷ lệ hiện tại của từng bãi (AdminService) và số hoa hồng trong kỳ (PaymentService). Không có số mẫu: mọi số từ API.
 * Bộ lọc (kỳ, tìm kiếm, nguồn tỷ lệ, sắp xếp, trang) nằm trên URL để chia sẻ / F5 giữ nguyên.
 */
export const CommissionPage: React.FC<{ tab: CommissionTab }> = ({ tab }) => {
  const params = useListParams();
  const location = useLocation();
  const periodParam = params.get('period');
  const fromParam = params.get('from');
  const toParam = params.get('to');
  const rangeResult = useMemo(() => resolveRange(periodParam, fromParam, toParam), [periodParam, fromParam, toParam]);
  const range: CommissionRange | null = rangeResult.ok ? rangeResult.range : null;
  const period = rangeResult.ok ? rangeResult.range.period : rangeResult.period;

  const search = params.get('q');
  const sourceParam = params.get('source');
  const source = isRateSource(sourceParam) ? sourceParam : null;
  const sortParam = params.get('sort');
  const sort = isLotSort(sortParam) ? sortParam : 'commission';

  const { summary, rates } = useCommissionData(range, search, source);
  const filter = useMemo(() => ({ search, source }), [search, source]);

  const lotRows = useMemo(() => sortLotRows(buildLotRows(rates.data, summary.data, filter), sort), [rates.data, summary.data, filter, sort]);
  const ownerRows = useMemo(() => sortOwnerRows(buildOwnerRows(rates.data, summary.data, lotRows, filter)), [rates.data, summary.data, lotRows, filter]);
  const sourceRows = useMemo(() => buildSourceRows(rates.data, summary.data), [rates.data, summary.data]);

  const rowCount = tab === 'lots' ? lotRows.length : tab === 'owners' ? ownerRows.length : sourceRows.length;
  const pageCount = Math.max(1, Math.ceil(rowCount / DEFAULT_PAGE_SIZE));
  const page = Math.min(params.page, pageCount);
  const slice = <T,>(rows: T[]) => rows.slice((page - 1) * DEFAULT_PAGE_SIZE, page * DEFAULT_PAGE_SIZE);

  const bothLoading = summary.status === 'loading' && rates.status === 'loading';
  const bothForbidden = summary.status === 'forbidden' && rates.status === 'forbidden';
  const busy = summary.isFetching || rates.isFetching;
  const noTransactions = summary.data !== null && summary.data.totals.transactionCount === 0 && summary.data.totals.pendingCount === 0;

  const onPeriod = (value: string) => {
    if (value === 'custom' && range) params.set({ period: 'custom', from: range.from, to: range.to });
    else params.set({ period: value === 'month' ? null : value, from: null, to: null });
  };

  const exportCsv = () => {
    if (!range) return;
    const csv = tab === 'lots' ? lotCsv(lotRows) : tab === 'owners' ? ownerCsv(ownerRows) : sourceCsv(sourceRows);
    downloadCsv(`commission_${range.from}_${range.to}.csv`, csv);
  };

  const tabs = (['lots', 'owners', 'sources'] as const).map((t) => ({ label: TAB_LABEL[t], to: `${COMMISSION_BASE}${TAB_PATH[t]}${location.search}` }));
  const defaults = rates.data;

  if (bothForbidden) {
    return (
      <>
        <AdminTitleBar title="Commission" subtitle="Platform commission" />
        <StateBlock type="noPermission" message={summary.error?.message} />
      </>
    );
  }

  return (
    <>
      <AdminTitleBar
        title="Commission"
        subtitle={range ? `Platform commission · ${displayDate(range.from)} – ${displayDate(range.to)} (ICT)` : 'Platform commission'}
        actions={
          <AdminButton variant="onNavy" icon="download" onClick={exportCsv} disabled={!range || rowCount === 0} aria-label="Export the totals shown as CSV">
            Export CSV
          </AdminButton>
        }
      />

      <ToolbarRow>
        <ToolbarSelect label="Period" value={period} options={PERIOD_OPTIONS} onChange={onPeriod} width={170} />
        {period === 'custom' && (
          <>
            <label className="adm-type-body-regular-12 flex items-center gap-1 text-(--admin-text-secondary)">
              From
              <input
                type="date"
                aria-label="From date (ICT)"
                value={fromParam}
                onChange={(e) => params.set({ from: e.target.value || null })}
                className="h-8 rounded-[2px] border border-(--admin-border-default) bg-(--admin-surface-card) px-2 text-(--admin-text-primary) focus-visible:outline-2 focus-visible:outline-(--admin-brand-navy-900)"
              />
            </label>
            <label className="adm-type-body-regular-12 flex items-center gap-1 text-(--admin-text-secondary)">
              To
              <input
                type="date"
                aria-label="To date (ICT)"
                value={toParam}
                onChange={(e) => params.set({ to: e.target.value || null })}
                className="h-8 rounded-[2px] border border-(--admin-border-default) bg-(--admin-surface-card) px-2 text-(--admin-text-primary) focus-visible:outline-2 focus-visible:outline-(--admin-brand-navy-900)"
              />
            </label>
          </>
        )}
        <SearchBox placeholder="Search lot, owner, contract number…" width={300} value={search} onSearch={(q) => params.set({ q: q || null })} />
        <ToolbarSelect
          label="Rate source"
          value={source ?? ''}
          options={[{ value: '', label: 'All' }, { value: 'LotContract', label: 'Contract' }, { value: 'DefaultConfig', label: 'Default' }]}
          onChange={(v) => params.set({ source: v || null })}
          width={180}
        />
        {tab === 'lots' && (
          <ToolbarSelect label="Sort by" value={sort} options={LOT_SORT_OPTIONS} onChange={(v) => params.set({ sort: v === 'commission' ? null : v })} width={190} />
        )}
      </ToolbarRow>

      {!rangeResult.ok && <Banner tone="danger">{rangeResult.error} (up to {MAX_RANGE_DAYS} days, ICT dates)</Banner>}

      <Banner
        tone="info"
        action={
          <Link to={SETTINGS_LINK} className="adm-type-link-small whitespace-nowrap text-(--admin-brand-navy-900) hover:underline focus-visible:outline-2 focus-visible:outline-(--admin-brand-navy-900)">
            Change default rate
          </Link>
        }
      >
        Current default rate: <strong>{defaults ? formatRate(defaults.defaultRate) : EMPTY}</strong>
        {defaults?.defaultNextChange && (
          <>
            {' '}· <strong>{formatRate(defaults.defaultNextChange.rate)}</strong> scheduled from {formatDateTimeIct(defaults.defaultNextChange.effectiveFromUtc)}
          </>
        )}
        . Commission is calculated automatically for every collected payment, at the lot&apos;s contract rate on the payment date (the default rate
        when no contract is in effect). Refunds reduce the commission accordingly.
      </Banner>

      {rates.data?.partial && (
        <Banner tone="warning">
          Some lot details could not be loaded – only lots with a signed contract are shown.
        </Banner>
      )}
      {rates.status !== 'idle' && isQueryFailure(rates.status) && (
        <Banner tone="danger" action={<AdminButton variant="secondary" onClick={rates.retry}>Retry</AdminButton>}>
          Couldn&apos;t load commission rates.{rates.error?.problem ? ` ${rates.error.message}` : ''}
        </Banner>
      )}
      {summary.status !== 'idle' && isQueryFailure(summary.status) && (
        <Banner tone="danger" action={<AdminButton variant="secondary" onClick={summary.retry}>Retry</AdminButton>}>
          Couldn&apos;t load commission figures.{summary.error?.problem ? ` ${summary.error.message}` : ''}
        </Banner>
      )}

      <KpiGrid totals={summary.data?.totals ?? null} status={summary.status} />

      <Panel title="Commission by day" description="By payment date (ICT) · amber mark = day with payments still pending">
        {summary.status === 'loading' ? (
          <StateBlock type="loading" bare />
        ) : summary.status === 'idle' ? (
          <StateBlock type="empty" title="Choose a valid period" message="Pick a date range to see the chart." bare />
        ) : summary.data === null ? (
          <QueryState
            status={sourceStatus(summary)}
            error={summary.error}
            onRetry={summary.retry}
            copy={{ emptyTitle: '', emptyMessage: '', errorTitle: "Couldn't load commission figures" }}
          />
        ) : noTransactions ? (
          <StateBlock type="empty" title="No payments in this period" message="No collected payments in the selected dates." bare />
        ) : (
          <CommissionDailyChart days={summary.data.series} />
        )}
      </Panel>

      <SubTabs tabs={tabs} />

      {tab === 'lots' && (
        <DataTable
          columns={lotColumns(rates.status === 'ready')}
          rows={slice(lotRows)}
          rowKey={(r) => r.parkingLotId}
          busy={busy && !bothLoading}
          placeholder={
            bothLoading ? (
              <StateBlock type="loading" bare />
            ) : (
              <StateBlock type="empty" title="No lots" message="No lots match these filters in this period." bare />
            )
          }
          footer={lotRows.length > DEFAULT_PAGE_SIZE && <Pagination page={page} pageSize={DEFAULT_PAGE_SIZE} totalCount={lotRows.length} onPageChange={params.setPage} />}
        />
      )}
      {tab === 'owners' && (
        <DataTable
          columns={ownerColumns}
          rows={slice(ownerRows)}
          rowKey={(r) => r.ownerProfileId}
          busy={busy && !bothLoading}
          placeholder={
            bothLoading ? (
              <StateBlock type="loading" bare />
            ) : (
              <StateBlock type="empty" title="No owners" message="No owners match these filters in this period." bare />
            )
          }
          footer={ownerRows.length > DEFAULT_PAGE_SIZE && <Pagination page={page} pageSize={DEFAULT_PAGE_SIZE} totalCount={ownerRows.length} onPageChange={params.setPage} />}
        />
      )}
      {tab === 'sources' && (
        <DataTable
          columns={sourceColumns}
          rows={bothLoading ? [] : sourceRows}
          rowKey={(r) => r.source}
          busy={busy && !bothLoading}
          placeholder={<StateBlock type="loading" bare />}
        />
      )}
    </>
  );
};
