import React, { useState } from 'react';
import { adminSettlementsApi } from '@/api/adminSettlementsApi';
import type { SettlementGenerationResultDto, SettlementListQuery, SettlementStatsDto, SettlementSummaryDto } from '@/types/adminSettlements';
import { SettlementStatus } from '@/types/adminEnums';
import { AdminButton } from '../../../components/AdminButton';
import { Banner } from '../../../components/Banner';
import { CellTwoLine, DataTable, type DataTableColumn } from '../../../components/DataTable';
import { FilterColumn, FilterPanel } from '../../../components/FilterColumn';
import { KpiTile } from '../../../components/KpiTile';
import { ListScreen, QueryState } from '../../../components/PageLayout';
import { Pagination } from '../../../components/Pagination';
import { StatusIndicator } from '../../../components/StatusIndicator';
import { SearchBox, ToolbarRow, ToolbarSelect } from '../../../components/Toolbar';
import { useAdminQuery } from '../../../hooks/useAdminQuery';
import { useAdminToast } from '../../../hooks/useAdminToast';
import { DEFAULT_PAGE_SIZE, useListParams } from '../../../hooks/useListParams';
import { EMPTY, formatNumber, formatVnd } from '../../../utils/format';
import { saveFile } from '../../operations/lot-contracts/lotContractFormat';
import { GenerateSettlementsDialog } from './SettlementDialogs';
import { Money } from './SettlementCells';
import { SettlementDrawer } from './SettlementDrawer';
import { actionError, formatPeriod, ownerLabel, periodOptionLabel, SKIP_REASON_LABEL } from './settlementFormat';
import { useOwnerAccounts } from './useOwnerAccounts';

const STATUSES = Object.values(SettlementStatus);
const isStatus = (value: string): value is SettlementStatus => (STATUSES as string[]).includes(value);
const positiveInt = (value: string): number | null => (/^\d+$/.test(value) && Number(value) > 0 ? Number(value) : null);

const SettlementKpis: React.FC<{ stats: SettlementStatsDto | null; loading: boolean }> = ({ stats, loading }) => {
  const tile = (label: string, value: number | undefined, format: (n: number) => string, note?: React.ReactNode) => (
    <KpiTile
      label={label}
      value={value === undefined ? EMPTY : format(value)}
      count={value === undefined ? undefined : { value, format }}
      loading={loading}
      muted={!stats && !loading}
      note={note}
    />
  );
  return (
    <section aria-label="Settlement indicators" className="adm-stagger-children grid grid-cols-2 gap-2 md:grid-cols-4 xl:grid-cols-7">
      {tile('Draft', stats?.draft, formatNumber)}
      {tile('Pending review', stats?.pendingReview, formatNumber)}
      {tile('Approved', stats?.approved, formatNumber)}
      {tile('Paid', stats?.paid, formatNumber)}
      <div data-testid="kpi-blocked" data-tone={(stats?.blocked ?? 0) > 0 ? 'warning' : 'default'} className="flex min-w-0">
        {tile('Blocked', stats?.blocked, formatNumber, (stats?.blocked ?? 0) > 0 && (
          <span className="adm-type-kpi-delta text-(--admin-status-warning)">Need regenerate / fixes</span>
        ))}
      </div>
      {tile('Net payout in review', stats?.pendingReviewNetPayout, formatVnd)}
      {tile('Awaiting transfer', stats?.awaitingPayoutNetPayout, formatVnd)}
    </section>
  );
};

/** Kết quả sinh kỳ → một dòng toast. */
const generationMessage = (r: SettlementGenerationResultDto) => {
  const count = (o: string) => r.owners.filter((x) => x.outcome === o).length;
  const skipped = r.owners.filter((x) => x.outcome === 'Skipped');
  const reasons = [...new Set(skipped.map((x) => SKIP_REASON_LABEL[x.reason ?? ''] ?? x.reason))].join(', ');
  return `${formatPeriod(r.periodStart, r.periodEnd)}: ${count('Created')} created, ${count('Regenerated')} regenerated, ${skipped.length} skipped${
    reasons ? ` (${reasons})` : ''
  }.`;
};

/**
 * Finance / Settlements (US-080): kỳ quyết toán của chủ bãi theo SETTLEMENT_CYCLE. Mọi số từ PaymentService; tên chủ bãi từ AdminService.
 * Bộ lọc (trạng thái, kỳ, tìm kiếm, bị chặn, trang) + kỳ đang mở (?settlement=) nằm trên URL.
 */
export const SettlementsPage: React.FC = () => {
  const toast = useAdminToast();
  const params = useListParams();
  const page = params.page;
  const statusParam = params.get('status');
  const filter: SettlementListQuery = {
    status: isStatus(statusParam) ? statusParam : null,
    periodStart: params.get('period') || null,
    search: params.get('q') || null,
    blocked: params.get('blocked') === 'true' ? true : params.get('blocked') === 'false' ? false : null,
  };
  const filterKey = JSON.stringify(filter);
  const openId = positiveInt(params.get('settlement'));
  const [generating, setGenerating] = useState(false);
  const [exporting, setExporting] = useState(false);

  const list = useAdminQuery(() => adminSettlementsApi.list(filter, page, DEFAULT_PAGE_SIZE), [filterKey, page], (d) => d.items.length === 0);
  const stats = useAdminQuery(() => adminSettlementsApi.stats(filter.periodStart ?? null), [filter.periodStart]);
  const periods = useAdminQuery(() => adminSettlementsApi.periods(), []);
  const rows = list.data?.items ?? [];
  const { byOwner } = useOwnerAccounts(rows.map((r) => r.ownerProfileId));

  const refresh = () => {
    list.refetch();
    stats.refetch();
    periods.refetch();
  };
  const open = (id: number | null) => params.set({ settlement: id, page: page <= 1 ? null : page });

  const exportCsv = async () => {
    setExporting(true);
    try {
      saveFile(await adminSettlementsApi.exportList(filter), 'settlements.csv');
      toast.show({ type: 'success', title: 'Export ready', message: 'The CSV uses the filters shown on screen (max 10,000 rows).' });
    } catch (err: unknown) {
      toast.show({ type: 'error', title: 'Export failed', message: actionError(err).message });
    } finally {
      setExporting(false);
    }
  };

  const columns: DataTableColumn<SettlementSummaryDto>[] = [
    { key: 'owner', header: 'Owner', render: (r) => <CellTwoLine primary={ownerLabel(r.ownerProfileId, byOwner.get(r.ownerProfileId))} secondary={r.code} /> },
    { key: 'period', header: 'Period', width: 190, render: (r) => <CellTwoLine primary={formatPeriod(r.periodStart, r.periodEnd)} secondary={r.cycle?.toLowerCase()} /> },
    { key: 'gross', header: 'Gross', width: 115, align: 'right', render: (r) => <Money value={r.grossAmount} /> },
    { key: 'commission', header: 'Commission', width: 110, align: 'right', render: (r) => <Money value={r.commissionAmount} /> },
    { key: 'refunds', header: 'Refunds', width: 105, align: 'right', render: (r) => <Money value={r.refundAmount} /> },
    { key: 'held', header: 'Held', width: 100, align: 'right', render: (r) => <Money value={r.heldAmount} /> },
    { key: 'adjust', header: 'Adjust.', width: 100, align: 'right', render: (r) => <Money value={r.adjustmentAmount + r.carryOverAmount} /> },
    { key: 'net', header: 'Net payout', width: 120, align: 'right', render: (r) => <Money value={r.netPayout} /> },
    {
      key: 'status',
      header: 'Status',
      width: 140,
      render: (r) => (
        <span className="flex flex-col gap-0.5">
          <StatusIndicator value={r.status} />
          {r.isBlocked && <StatusIndicator tone="warning" label="Blocked" />}
        </span>
      ),
    },
  ];

  const hasFilter = Object.values(filter).some((v) => v !== null);

  return (
    <ListScreen
      title="Settlements"
      subtitle={list.data ? `${formatNumber(list.data.totalCount)} settlements · one per owner per cycle` : 'One settlement per owner per cycle'}
      actions={
        <>
          <AdminButton variant="onNavy" icon="download" onClick={exportCsv} disabled={exporting || list.status !== 'ready'}>
            {exporting ? 'Exporting…' : 'Export CSV'}
          </AdminButton>
          <AdminButton variant="onNavy" icon="plus" onClick={() => setGenerating(true)} disabled={periods.status === 'loading'}>
            Generate
          </AdminButton>
        </>
      }
      top={
        <>
          <Banner tone="info">
            Net payout = Gross − Refunds − Collected by owner − Commission − Held + Adjustments + Carry-over. Cash / VietQR is already with the owner, so
            only its commission is deducted; a negative net payout is carried over. Approved periods are closed – correct them with an adjustment.
          </Banner>
          {(stats.status === 'error' || stats.status === 'unavailable') && (
            <Banner tone="danger" action={<AdminButton variant="secondary" onClick={stats.refetch}>Retry</AdminButton>}>
              Could not load indicators (PaymentService): {stats.error?.message}
            </Banner>
          )}
          <SettlementKpis stats={stats.data} loading={stats.status === 'loading'} />
        </>
      }
      filters={
        <FilterColumn>
          <FilterPanel
            title="Settlement status"
            value={filter.status ?? ''}
            onChange={(v) => params.set({ status: v || null })}
            options={[
              { value: '', label: 'All' },
              { value: 'Draft', label: 'Draft', count: stats.data?.draft },
              { value: 'PendingReview', label: 'Pending review', count: stats.data?.pendingReview },
              { value: 'Approved', label: 'Approved', count: stats.data?.approved },
              { value: 'Paid', label: 'Paid', count: stats.data?.paid },
            ]}
          />
        </FilterColumn>
      }
      toolbar={
        <ToolbarRow>
          <SearchBox placeholder="Search settlement code or owner ID…" width={280} value={params.get('q')} onSearch={(q) => params.set({ q: q || null })} />
          <ToolbarSelect
            label="Period"
            width={290}
            value={filter.periodStart ?? ''}
            options={[{ value: '', label: 'All periods' }, ...(periods.data ?? []).map((p) => ({ value: p.periodStart, label: periodOptionLabel(p) }))]}
            onChange={(v) => params.set({ period: v || null })}
          />
          <ToolbarSelect
            label="Blocked"
            width={150}
            value={params.get('blocked')}
            options={[{ value: '', label: 'Any' }, { value: 'true', label: 'Blocked only' }, { value: 'false', label: 'Not blocked' }]}
            onChange={(v) => params.set({ blocked: v || null })}
          />
          {hasFilter && (
            <AdminButton variant="ghost" className="adm-motion-fade" onClick={() => params.set({ status: null, period: null, q: null, blocked: null })}>
              Clear filters
            </AdminButton>
          )}
        </ToolbarRow>
      }
    >
      <DataTable
        animateRows
        busy={list.isFetching && list.data !== null}
        columns={columns}
        rows={list.status === 'ready' ? rows : []}
        rowKey={(r) => r.id}
        onRowClick={(r) => open(r.id)}
        selectedKey={openId}
        placeholder={
          <QueryState
            status={list.status}
            error={list.error}
            onRetry={list.refetch}
            copy={{
              emptyTitle: hasFilter ? 'No settlements match' : 'No settlements yet',
              emptyMessage: hasFilter ? 'Try other filters.' : 'Settlements are generated after each period ends, or use Generate.',
              errorTitle: 'Could not load settlements',
              service: 'PaymentService',
            }}
          />
        }
        footer={
          list.data && list.data.totalCount > DEFAULT_PAGE_SIZE && (
            <Pagination page={page} pageSize={DEFAULT_PAGE_SIZE} totalCount={list.data.totalCount} onPageChange={params.setPage} />
          )
        }
      />

      {openId !== null && <SettlementDrawer key={openId} settlementId={openId} onClose={() => open(null)} onChanged={refresh} />}
      {generating && (
        <GenerateSettlementsDialog
          periods={periods.data ?? []}
          onClose={() => setGenerating(false)}
          onDone={(result) => {
            setGenerating(false);
            toast.show({ type: 'success', title: 'Generation finished', message: generationMessage(result) });
            refresh();
          }}
        />
      )}
    </ListScreen>
  );
};
