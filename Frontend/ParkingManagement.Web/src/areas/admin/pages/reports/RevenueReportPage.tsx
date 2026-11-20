import React from 'react';
import { adminCommissionApi } from '@/api/adminCommissionApi';
import type { AdminCommissionByLotDto, AdminCommissionSummaryDto } from '@/types/adminCommission';
import { AdminTitleBar } from '../../layout/AdminTitleBar';
import { KpiStrip, KpiTile } from '../../components/KpiTile';
import { Panel } from '../../components/Panel';
import { StateBlock } from '../../components/StateBlock';
import { ToolbarRow, ToolbarSelect } from '../../components/Toolbar';
import { CellLink, CellText, DataTable, type DataTableColumn } from '../../components/DataTable';
import { useAdminQuery } from '../../hooks/useAdminQuery';
import { useListParams } from '../../hooks/useListParams';
import { formatNumber, formatPercent, formatTimeIct, formatVnd } from '../../utils/format';
import { displayDate } from '../finance/commission/commissionPeriod';
import { RevenueDailyChart } from './RevenueDailyChart';
import { REVENUE_PERIOD_OPTIONS, resolveRevenueRange, share } from './revenuePeriod';
import { useLotNames } from './useLotNames';
import { QueryIssueState } from '../../components/PageLayout';

const TOP_LOTS = 10;
const LOT_DETAIL = '/admin/operations/parking-lots';

const DateInput: React.FC<{ label: string; value: string; onChange: (v: string) => void }> = ({ label, value, onChange }) => (
  <label className="flex h-8 items-center gap-2 rounded-[2px] border border-(--admin-border-default) bg-(--admin-surface-card) px-2.5 focus-within:border-(--admin-brand-navy-900)">
    <span className="adm-type-body-regular-12 text-(--admin-text-secondary)">{label}</span>
    <input
      type="date"
      aria-label={label}
      value={value}
      onChange={(e) => onChange(e.target.value)}
      className="adm-type-body-default bg-transparent text-(--admin-text-primary) outline-none"
    />
  </label>
);

const Kpis: React.FC<{ data: AdminCommissionSummaryDto | null; loading: boolean }> = ({ data, loading }) => {
  const t = data?.totals;
  const refundShare = t ? share(t.refundedAmount, t.grossAmount) : null;
  const commissionNote =
    t && t.pendingCount > 0 ? (
      <span className="adm-type-kpi-delta text-(--admin-status-warning-text)">
        {`Not final · ${formatNumber(t.pendingCount)} ${t.pendingCount === 1 ? 'payment' : 'payments'} not calculated yet`}
      </span>
    ) : undefined;
  return (
    <KpiStrip>
      <KpiTile
        label="Gross revenue"
        loading={loading}
        value={formatVnd(t?.grossAmount)}
        delta={t ? `${formatNumber(t.transactionCount)} ${t.transactionCount === 1 ? 'payment' : 'payments'}` : undefined}
      />
      <KpiTile
        label="Platform commission"
        loading={loading}
        value={formatVnd(t?.commissionAmount)}
        note={commissionNote}
        delta={t?.effectiveRate !== null && t?.effectiveRate !== undefined ? `${formatPercent(t.effectiveRate * 100)} of net revenue` : undefined}
      />
      <KpiTile
        label="Refunds"
        loading={loading}
        value={formatVnd(t?.refundedAmount)}
        delta={refundShare !== null ? `${formatPercent(refundShare)} of gross` : undefined}
      />
      <KpiTile
        label="Succeeded payments"
        loading={loading}
        value={formatNumber(t?.transactionCount)}
        delta={t ? `Net revenue ${formatVnd(t.netAmount)}` : undefined}
      />
    </KpiStrip>
  );
};

/** Panel ngoài Figma (An duyệt D): 10 bãi có doanh thu gộp cao nhất trong kỳ; tên bãi thiếu → "Lot #id". */
const TopLots: React.FC<{ rows: AdminCommissionByLotDto[] }> = ({ rows }) => {
  const lotName = useLotNames();
  const top = [...rows].sort((a, b) => b.grossAmount - a.grossAmount || a.parkingLotId - b.parkingLotId).slice(0, TOP_LOTS);
  const columns: DataTableColumn<AdminCommissionByLotDto>[] = [
    {
      key: 'lot',
      header: 'Lot',
      render: (r) => (
        <span className="inline-block min-w-[160px]">
          <CellLink to={`${LOT_DETAIL}/${r.parkingLotId}`}>{lotName(r.parkingLotId) ?? `Lot #${r.parkingLotId}`}</CellLink>
        </span>
      ),
    },
    { key: 'gross', header: 'Gross', align: 'right', render: (r) => <CellText>{formatVnd(r.grossAmount)}</CellText> },
    { key: 'refunds', header: 'Refunds', align: 'right', render: (r) => <CellText>{formatVnd(r.refundedAmount)}</CellText> },
    { key: 'net', header: 'Net', align: 'right', render: (r) => <CellText>{formatVnd(r.netAmount)}</CellText> },
    {
      key: 'commission',
      header: 'Commission',
      align: 'right',
      render: (r) => <CellText>{r.pendingCount > 0 ? `${formatVnd(r.commissionAmount)} (not final)` : formatVnd(r.commissionAmount)}</CellText>,
    },
    { key: 'payments', header: 'Payments', align: 'right', render: (r) => <CellText>{formatNumber(r.transactionCount)}</CellText> },
  ];
  return (
    <Panel title="Top lots by gross revenue" padded={false}>
      <DataTable bare rows={top} rowKey={(r) => `${r.parkingLotId}-${r.ownerProfileId}`} columns={columns} />
    </Panel>
  );
};

/**
 * Reports / Revenue (P0-5, US-081): PaymentService commission-summary – 4 KPI, doanh thu gộp theo ngày (ICT), top bãi.
 * Kỳ trên URL (?period=month|prev-month|7d|custom&from=&to=). Không có nguồn cho phương thức / mục đích / so sánh kỳ trước → không hiển thị.
 */
export const RevenueReportPage: React.FC = () => {
  const params = useListParams();
  const result = resolveRevenueRange(params.get('period'), params.get('from'), params.get('to'));
  const range = result.ok ? result.range : null;
  const query = useAdminQuery(range ? () => adminCommissionApi.summary(range.fromUtc, range.toUtc) : null, [range?.fromUtc, range?.toUtc]);
  const data = query.status === 'ready' ? query.data : null;
  const period = result.ok ? result.range.period : result.period;

  const subtitle = data && range ? `${displayDate(range.from)} – ${displayDate(range.to)} (ICT) · updated ${formatTimeIct(data.asOfUtc)}` : undefined;

  const body = (() => {
    if (!result.ok) return <StateBlock type="empty" title="Choose a valid period" message={result.error} />;
    switch (query.status) {
      case 'forbidden':
        return <StateBlock type="noPermission" message="Only administrators can view platform revenue." />;
      case 'notFound':
      case 'offline':
      case 'sessionExpired':
        return <QueryIssueState status={query.status} error={query.error} onRetry={query.refetch} />;
      case 'error':
      case 'unavailable':
        return (
          <StateBlock
            type="error"
            title="Could not load revenue"
            message={query.error?.problem ? query.error.message : 'The revenue report could not be loaded. Try again.'}
            onRetry={query.refetch}
          />
        );
      default:
        return (
          <>
            <Kpis data={data} loading={query.status === 'loading'} />
            <Panel title="Gross revenue by day">
              {query.status === 'loading' || !data ? (
                <StateBlock type="loading" bare />
              ) : data.totals.transactionCount === 0 ? (
                <StateBlock type="empty" bare title="No payments in this period" message="Pick another period to see revenue." />
              ) : (
                <RevenueDailyChart days={data.series} />
              )}
            </Panel>
            {data && data.byLot.length > 0 && <TopLots rows={data.byLot} />}
          </>
        );
    }
  })();

  return (
    <>
      <AdminTitleBar title="Revenue" subtitle={subtitle} />
      <ToolbarRow>
        <ToolbarSelect
          label="Period"
          value={period}
          width={200}
          options={REVENUE_PERIOD_OPTIONS}
          onChange={(v) =>
            params.set(
              v === 'custom'
                ? { period: v, from: range?.from ?? null, to: range?.to ?? null }
                : { period: v === 'month' ? null : v, from: null, to: null }
            )
          }
        />
        {period === 'custom' && (
          <>
            <DateInput label="From" value={params.get('from')} onChange={(v) => params.set({ from: v || null })} />
            <DateInput label="To" value={params.get('to')} onChange={(v) => params.set({ to: v || null })} />
          </>
        )}
      </ToolbarRow>
      {body}
    </>
  );
};
