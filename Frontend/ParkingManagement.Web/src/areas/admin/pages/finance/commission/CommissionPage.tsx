import React, { useMemo } from 'react';
import { Link, useLocation } from 'react-router-dom';
import type { AdminQueryStatus } from '@/types/adminCommon';
import type { AdminCommissionTotalsDto, CommissionRateSource } from '@/types/adminCommission';
import { AdminTitleBar } from '../../../layout/AdminTitleBar';
import { AdminButton } from '../../../components/AdminButton';
import { Banner } from '../../../components/Banner';
import { CellAmount, CellLink, CellMuted, CellTwoLine, DataTable, type DataTableColumn } from '../../../components/DataTable';
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

export type CommissionTab = 'lots' | 'owners' | 'sources';

export const COMMISSION_BASE = '/admin/finance/commission';
const TAB_PATH: Record<CommissionTab, string> = { lots: '', owners: '/owners', sources: '/sources' };
const TAB_LABEL: Record<CommissionTab, string> = { lots: 'Theo bãi', owners: 'Theo chủ bãi', sources: 'Theo nguồn tỷ lệ' };
const SETTINGS_LINK = '/admin/access/system-settings/DEFAULT_COMMISSION_RATE';
const PENDING_HINT = 'Đang chờ job tính, không phải 0';

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
    <span className="adm-type-body-strong whitespace-nowrap text-(--admin-status-warning)" title={PENDING_HINT}>
      {formatNumber(count)} chờ tính
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
    header: 'Bãi',
    render: (r) => (
      <CellTwoLine
        primary={r.lotName}
        secondary={r.known ? [r.district, `#${r.parkingLotId}`].filter(Boolean).join(' · ') : ratesLoaded ? 'Không có trong danh sách tỷ lệ' : `#${r.parkingLotId}`}
      />
    ),
  },
  {
    key: 'owner',
    header: 'Chủ bãi',
    render: (r) => (
      <CellTwoLine primary={r.ownerName ?? EMPTY} secondary={r.ownerProfileId !== null ? `OwnerProfile #${r.ownerProfileId}` : undefined} />
    ),
  },
  {
    key: 'rate',
    header: 'Tỷ lệ hiện tại',
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
    header: 'Hợp đồng',
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
  { key: 'net', header: 'Thuần', width: 120, align: 'right', render: (r) => <Money value={r.money?.net} /> },
  { key: 'commission', header: 'Hoa hồng', width: 120, align: 'right', render: (r) => <Money value={r.money?.commission} /> },
  { key: 'effective', header: 'Tỷ lệ hiệu dụng', width: 110, align: 'right', render: (r) => <CellMuted>{formatRate(r.money?.effectiveRate)}</CellMuted> },
  { key: 'pending', header: 'Chờ tính', width: 100, align: 'right', render: (r) => <Pending count={r.money?.pendingCount} /> },
];

const ownerColumns: DataTableColumn<CommissionOwnerRow>[] = [
  {
    key: 'owner',
    header: 'Chủ bãi',
    render: (r) => {
      const link = partnerLink(r.ownerName);
      return (
        <CellTwoLine
          primary={link ? <CellLink to={link}>{r.ownerName}</CellLink> : r.ownerName ?? EMPTY}
          secondary={`OwnerProfile #${r.ownerProfileId}`}
        />
      );
    },
  },
  { key: 'lots', header: 'Số bãi', width: 80, align: 'right', render: (r) => <CellMuted>{formatNumber(r.lotCount)}</CellMuted> },
  { key: 'gross', header: 'GMV', width: 130, align: 'right', render: (r) => <Money value={r.money?.gross} /> },
  { key: 'net', header: 'Thuần', width: 130, align: 'right', render: (r) => <Money value={r.money?.net} /> },
  { key: 'commission', header: 'Hoa hồng', width: 130, align: 'right', render: (r) => <Money value={r.money?.commission} /> },
  { key: 'effective', header: 'Tỷ lệ hiệu dụng', width: 110, align: 'right', render: (r) => <CellMuted>{formatRate(r.money?.effectiveRate)}</CellMuted> },
  { key: 'pending', header: 'Chờ tính', width: 100, align: 'right', render: (r) => <Pending count={r.money?.pendingCount} /> },
];

const sourceColumns: DataTableColumn<CommissionSourceRow>[] = [
  { key: 'source', header: 'Nguồn tỷ lệ', render: (r) => <CellTwoLine primary={SOURCE_LABEL[r.source]} secondary={r.source} /> },
  { key: 'lots', header: 'Bãi đang áp dụng', width: 130, align: 'right', render: (r) => <CellMuted>{formatNumber(r.lotCount)}</CellMuted> },
  { key: 'tx', header: 'Giao dịch đã tính', width: 130, align: 'right', render: (r) => <CellMuted>{formatNumber(r.stats?.transactionCount)}</CellMuted> },
  { key: 'base', header: 'Cơ sở tính', width: 130, align: 'right', render: (r) => <Money value={r.stats?.baseAmount} /> },
  { key: 'refunded', header: 'Đã hoàn', width: 120, align: 'right', render: (r) => <Money value={r.stats?.refundedAmount} /> },
  { key: 'commission', header: 'Hoa hồng', width: 130, align: 'right', render: (r) => <Money value={r.stats?.commissionAmount} /> },
  { key: 'effective', header: 'Tỷ lệ hiệu dụng', width: 110, align: 'right', render: (r) => <CellMuted>{formatRate(r.stats?.effectiveRate)}</CellMuted> },
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
    <span className="adm-type-kpi-delta text-(--admin-status-warning)" title={PENDING_HINT}>
      {PENDING_HINT}
    </span>
  );

  // 6 tile: 2 cột (< 768) → 3 cột (≥ 768 md) → 6 cột (≥ 1280 xl); lần đầu hiện so le (T-801).
  return (
    <section aria-label="Chỉ số hoa hồng trong kỳ" className="adm-stagger-children grid grid-cols-2 gap-2 md:grid-cols-3 xl:grid-cols-6">
      {tile('GMV', totals?.grossAmount, formatVnd)}
      {tile('Đã hoàn', totals?.refundedAmount, formatVnd)}
      {tile('Doanh thu thuần', totals?.netAmount, formatVnd)}
      {tile('Hoa hồng', totals?.commissionAmount, formatVnd, pending > 0 && (
        <span className="adm-type-kpi-delta text-(--admin-status-warning)" title={PENDING_HINT}>
          Chưa đủ · còn {formatNumber(pending)} giao dịch chờ tính
        </span>
      ))}
      {tile('Tỷ lệ hiệu dụng', totals?.effectiveRate, formatRate)}
      <div
        data-testid="kpi-pending"
        data-tone={pending > 0 ? 'warning' : 'default'}
        title={pending > 0 ? PENDING_HINT : undefined}
        className={`flex min-w-0 ${pending > 0 ? 'outline-2 -outline-offset-2 outline-(--admin-status-warning) outline' : ''}`}
      >
        {tile('Giao dịch chờ tính', totals?.pendingCount, formatNumber, pendingNote)}
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
        <AdminTitleBar title="Commission" subtitle="Hoa hồng nền tảng" />
        <StateBlock type="noPermission" message={summary.error?.message} />
      </>
    );
  }

  return (
    <>
      <AdminTitleBar
        title="Commission"
        subtitle={range ? `Hoa hồng nền tảng · ${displayDate(range.from)} – ${displayDate(range.to)} (ICT)` : 'Hoa hồng nền tảng'}
        actions={
          <AdminButton variant="onNavy" icon="download" onClick={exportCsv} disabled={!range || rowCount === 0} aria-label="Xuất CSV dữ liệu tổng hợp đang hiển thị">
            Xuất CSV
          </AdminButton>
        }
      />

      <ToolbarRow>
        <ToolbarSelect label="Kỳ" value={period} options={PERIOD_OPTIONS} onChange={onPeriod} width={170} />
        {period === 'custom' && (
          <>
            <label className="adm-type-body-regular-12 flex items-center gap-1 text-(--admin-text-secondary)">
              Từ
              <input
                type="date"
                aria-label="Từ ngày (ICT)"
                value={fromParam}
                onChange={(e) => params.set({ from: e.target.value || null })}
                className="h-8 rounded-[2px] border border-(--admin-border-default) bg-(--admin-surface-card) px-2 text-(--admin-text-primary) focus-visible:outline-2 focus-visible:outline-(--admin-brand-navy-900)"
              />
            </label>
            <label className="adm-type-body-regular-12 flex items-center gap-1 text-(--admin-text-secondary)">
              Đến
              <input
                type="date"
                aria-label="Đến ngày (ICT)"
                value={toParam}
                onChange={(e) => params.set({ to: e.target.value || null })}
                className="h-8 rounded-[2px] border border-(--admin-border-default) bg-(--admin-surface-card) px-2 text-(--admin-text-primary) focus-visible:outline-2 focus-visible:outline-(--admin-brand-navy-900)"
              />
            </label>
          </>
        )}
        <SearchBox placeholder="Tìm bãi, chủ bãi, số hợp đồng…" width={300} value={search} onSearch={(q) => params.set({ q: q || null })} />
        <ToolbarSelect
          label="Nguồn tỷ lệ"
          value={source ?? ''}
          options={[{ value: '', label: 'Tất cả' }, { value: 'LotContract', label: 'Hợp đồng' }, { value: 'DefaultConfig', label: 'Mặc định' }]}
          onChange={(v) => params.set({ source: v || null })}
          width={180}
        />
        {tab === 'lots' && (
          <ToolbarSelect label="Sắp xếp" value={sort} options={LOT_SORT_OPTIONS} onChange={(v) => params.set({ sort: v === 'commission' ? null : v })} width={190} />
        )}
      </ToolbarRow>

      {!rangeResult.ok && <Banner tone="danger">{rangeResult.error} (tối đa {MAX_RANGE_DAYS} ngày, theo ngày ICT)</Banner>}

      <Banner
        tone="info"
        action={
          <Link to={SETTINGS_LINK} className="adm-type-link-small whitespace-nowrap text-(--admin-brand-navy-900) hover:underline focus-visible:outline-2 focus-visible:outline-(--admin-brand-navy-900)">
            Đổi tỷ lệ mặc định
          </Link>
        }
      >
        Tỷ lệ mặc định hiện tại: <strong>{defaults ? formatRate(defaults.defaultRate) : EMPTY}</strong> (DEFAULT_COMMISSION_RATE)
        {defaults?.defaultNextChange && (
          <>
            {' '}· đã lên lịch <strong>{formatRate(defaults.defaultNextChange.rate)}</strong> từ {formatDateTimeIct(defaults.defaultNextChange.effectiveFromUtc)}
          </>
        )}
        . Hoa hồng được hệ thống tự tính định kỳ cho mọi giao dịch đã thu tiền theo tỷ lệ hợp đồng của bãi tại thời điểm thanh toán (không có
        hợp đồng hiệu lực thì dùng tỷ lệ mặc định); hoàn tiền tự giảm hoa hồng tương ứng.
      </Banner>

      {rates.data?.partial && (
        <Banner tone="warning">
          Thiếu thông tin một số bãi từ ParkingService – chỉ hiện các bãi có hợp đồng đã ký.{rates.data.partialReason ? ` (${rates.data.partialReason})` : ''}
        </Banner>
      )}
      {(rates.status === 'error' || rates.status === 'unavailable' || rates.status === 'forbidden') && (
        <Banner tone="danger" action={<AdminButton variant="secondary" onClick={rates.retry}>Thử lại</AdminButton>}>
          Không tải được tỷ lệ hoa hồng (AdminService): {rates.error?.message}
        </Banner>
      )}
      {(summary.status === 'error' || summary.status === 'unavailable' || summary.status === 'forbidden') && (
        <Banner tone="danger" action={<AdminButton variant="secondary" onClick={summary.retry}>Thử lại</AdminButton>}>
          Không tải được số hoa hồng (PaymentService): {summary.error?.message}
        </Banner>
      )}

      <KpiGrid totals={summary.data?.totals ?? null} status={summary.status} />

      <Panel title="Hoa hồng theo ngày" description="Theo ngày thanh toán (ICT) · vạch vàng trên cột = ngày còn giao dịch chờ tính">
        {summary.status === 'loading' ? (
          <StateBlock type="loading" bare />
        ) : summary.status === 'idle' ? (
          <StateBlock type="empty" title="Chưa chọn kỳ hợp lệ" message="Chọn lại khoảng ngày để xem biểu đồ." bare />
        ) : summary.data === null ? (
          <QueryState
            status={sourceStatus(summary)}
            error={summary.error}
            onRetry={summary.retry}
            copy={{ emptyTitle: '', emptyMessage: '', errorTitle: 'Không tải được số hoa hồng', service: 'PaymentService' }}
          />
        ) : noTransactions ? (
          <StateBlock type="empty" title="Không có giao dịch trong kỳ" message="Chưa có giao dịch đã thu tiền nào trong khoảng ngày đã chọn." bare />
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
              <StateBlock type="empty" title="Không có bãi" message="Không có bãi nào khớp bộ lọc trong kỳ này." bare />
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
              <StateBlock type="empty" title="Không có chủ bãi" message="Không có chủ bãi nào khớp bộ lọc trong kỳ này." bare />
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
