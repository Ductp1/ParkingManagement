import React, { useState } from 'react';
import { AdminTitleBar } from '../../layout/AdminTitleBar';
import { KpiStrip, KpiTile } from '../../components/KpiTile';
import { DetailColumns, QueryState } from '../../components/PageLayout';
import { Panel } from '../../components/Panel';
import { DataTable } from '../../components/DataTable';
import { AdminButton } from '../../components/AdminButton';
import { useAdminQuery } from '../../hooks/useAdminQuery';
import { useVisibleInterval } from '../../hooks/useVisibleInterval';
import { formatDate, formatDateTimeIct, formatNumber, formatTimeIct, ictDateParam } from '../../utils/format';
import { adminDashboardApi } from './adminDashboardApi';
import type { DashboardOverviewDto, DashboardWidget, NoValue } from './dashboardTypes';
import { QueueRow, WidgetTablePlaceholder, WidgetUnavailableNote } from './DashboardWidgetState';

/** US-081: tự làm mới khi tab đang hiển thị; Admin vẫn có nút Refresh. */
const REFRESH_INTERVAL_MS = 60_000;

const KPIS: Array<{ label: string; pick: (d: DashboardOverviewDto) => DashboardWidget<NoValue> }> = [
  { label: 'Revenue today (succeeded payments)', pick: (d) => d.kpis.revenueToday },
  { label: 'Active parking sessions', pick: (d) => d.kpis.activeSessions },
  { label: 'Bookings today', pick: (d) => d.kpis.bookingsToday },
  { label: 'Open disputes', pick: (d) => d.kpis.openDisputes },
];

const LOT_COLUMNS = ['Parking lot', 'Occupancy', 'Integration', 'Lot status', 'Capacity flag'];
const PAYMENT_COLUMNS = ['Payment', 'Booking', 'Method', 'Purpose', 'Amount', 'Status', 'Paid at'];
const DEVICE_COLUMNS = ['Device type', 'Online', 'Offline', 'Maint.'];

const LOADING_COPY = { emptyTitle: '', emptyMessage: '', errorTitle: '', service: 'AdminService' };

/** Bảng chưa có nguồn: header đúng Figma + trạng thái nguồn, không vẽ dòng mẫu. */
const SourceTable: React.FC<{ columns: string[]; widget: DashboardWidget<NoValue> | null }> = ({ columns, widget }) => (
  <DataTable
    bare
    rows={[]}
    rowKey={() => 0}
    columns={columns.map((h) => ({ key: h, header: h, render: () => null }))}
    placeholder={widget ? <WidgetTablePlaceholder widget={widget} /> : <QueryState status="loading" error={null} onRetry={() => undefined} copy={LOADING_COPY} />}
  />
);

/**
 * Overview / Dashboard (US-081, UC-45): GET /api/v1/admin/dashboard/overview. Mỗi widget tự mang trạng thái: có số (count-up
 * lần đầu), chưa có nguồn ("No data source yet · needs X") hoặc nguồn lỗi – một nguồn lỗi không làm hỏng cả trang.
 * Không có số mẫu: widget chưa có API nguồn hiện "—", không hiện 0.
 */
export const DashboardPage: React.FC = () => {
  const query = useAdminQuery(() => adminDashboardApi.overview(), []);
  const refresh = query.refetch;
  useVisibleInterval(refresh, REFRESH_INTERVAL_MS);

  // Ngày hiển thị trước khi có dữ liệu: chốt một lần lúc mở màn (không gọi Date trong mỗi lần render).
  const [openedToday] = useState(() => formatDate(new Date().toISOString()));
  const data = query.data;
  const today = data ? formatDate(data.generatedAtUtc) : openedToday;
  const todayParam = data ? ictDateParam(data.todayFromUtc) : null;
  const auditLink = todayParam ? `/admin/access/audit-log?from=${todayParam}&to=${todayParam}` : '/admin/access/audit-log';

  const titleBar = (
    <AdminTitleBar
      title="Platform overview"
      subtitle={`Today ${today} · ICT · ${data ? `updated ${formatTimeIct(data.generatedAtUtc)}` : 'loading…'} · refreshes every 60 s while this tab is open`}
      actions={
        <AdminButton variant="onNavy" icon="refresh" onClick={refresh} disabled={query.isFetching} loading={query.isFetching && data !== null} loadingLabel="Refreshing…">
          Refresh
        </AdminButton>
      }
    />
  );

  // Cả endpoint lỗi (AdminService không phản hồi / không có quyền): Figma "Dashboard – Error / No permission".
  if (!data && query.status !== 'loading') {
    return (
      <>
        {titleBar}
        <QueryState
          status={query.status}
          error={query.error}
          onRetry={refresh}
          bare={false}
          copy={{ emptyTitle: 'Nothing to show', emptyMessage: 'The dashboard returned no data.', errorTitle: 'Could not load the dashboard', service: 'AdminService' }}
        />
      </>
    );
  }

  const q = data?.queues ?? null;
  const g = data?.governance ?? null;
  const t = data?.tables ?? null;

  return (
    <>
      {titleBar}
      <KpiStrip>
        {KPIS.map((k) => {
          const widget = data ? k.pick(data) : null;
          return (
            <KpiTile
              key={k.label}
              label={k.label}
              value="—"
              loading={widget === null}
              muted
              note={widget && <WidgetUnavailableNote widget={widget} />}
            />
          );
        })}
      </KpiStrip>
      <DetailColumns
        main={
          <>
            <Panel title="Lot occupancy & capacity" description="ParkingLots.AvailableSlots / TotalSlots · LotCapacityConfigs flags" action={{ label: 'View all lots →', to: '/admin/operations/parking-lots' }} padded={false}>
              <SourceTable columns={LOT_COLUMNS} widget={t?.lotOccupancy ?? null} />
            </Panel>
            <Panel title="Recent payments" action={{ label: 'All transactions →', to: '/admin/finance/transactions' }} padded={false}>
              <SourceTable columns={PAYMENT_COLUMNS} widget={t?.recentPayments ?? null} />
            </Panel>
            <Panel title="Gate devices" description="GateDevices.Status by DeviceType" padded={false}>
              <SourceTable columns={DEVICE_COLUMNS} widget={t?.gateDevices ?? null} />
            </Panel>
          </>
        }
        side={
          <>
            <Panel title="Work queues" padded={false}>
              <QueueRow
                label="KYB applications to review"
                to="/admin/operations/kyb-review?status=Submitted"
                widget={q?.kybApplications ?? null}
                count={(v) => v.toReview}
                detail={(v) => `${formatNumber(v.underReview)} under review · ${formatNumber(v.needsMoreInfo)} waiting for owner info`}
              />
              <QueueRow
                label="Partner applications"
                to="/admin/operations/partner-onboarding?status=Submitted"
                widget={q?.partnerApplications ?? null}
                count={(v) => v.toReview}
                detail={(v) => `${formatNumber(v.needsMoreInfo)} waiting for info · ${formatNumber(v.accountIssued)} awaiting activation`}
              />
              <QueueRow label="Lot contracts to sign" to="/admin/operations/lot-contracts?status=Pending" widget={q?.lotContractsToSign ?? null} count={(v) => v.pending} />
              <QueueRow label="Refunds requested" to="/admin/finance/refunds?status=Requested" widget={q?.refundsRequested ?? null} />
              <QueueRow label="Settlements pending review" to="/admin/finance/settlements?status=PendingReview" widget={q?.settlementsPendingReview ?? null} />
              <QueueRow label="Disputes awaiting owner > 48h" to="/admin/support/disputes?status=AwaitingOwner" widget={q?.disputesAwaitingOwner ?? null} />
              <QueueRow
                label={
                  q?.riskFlags.status === 'available' && q.riskFlags.value
                    ? `Open risk flags (${formatNumber(q.riskFlags.value.critical)} critical)`
                    : 'Open risk flags'
                }
                to="/admin/support/risk-flags"
                widget={q?.riskFlags ?? null}
                count={(v) => v.unresolved}
                detail={(v) => `${formatNumber(v.overdue)} past due · ${formatNumber(v.investigating)} investigating`}
              />
              <QueueRow label="Data subject requests due ≤ 7 days" to="/admin/access/security-privacy" widget={q?.dataSubjectRequestsDue ?? null} />
            </Panel>
            <Panel title="Platform governance" description="Sanctions · SystemConfigChanges · AuditLogs (today, ICT)" padded={false}>
              <QueueRow
                label="Active sanctions"
                to="/admin/support/sanctions"
                widget={g?.activeSanctions ?? null}
                count={(v) => v.total}
                detail={(v) => `${formatNumber(v.temporarySuspension)} suspended · ${formatNumber(v.permanentBan)} banned · ${formatNumber(v.warning + v.rankingPenalty)} other`}
              />
              <QueueRow
                label="Scheduled setting changes"
                to={
                  g?.scheduledConfigChanges.status === 'available' && g.scheduledConfigChanges.value?.nextKey
                    ? `/admin/access/system-settings/${encodeURIComponent(g.scheduledConfigChanges.value.nextKey)}`
                    : '/admin/access/system-settings'
                }
                widget={g?.scheduledConfigChanges ?? null}
                count={(v) => v.count}
                detail={(v) => (v.nextKey ? `Next: ${v.nextKey} from ${formatDateTimeIct(v.nextEffectiveFromUtc)}` : 'Nothing scheduled')}
              />
              <QueueRow
                label="Audit log entries today"
                to={auditLink}
                widget={g?.auditLogsToday ?? null}
                count={(v) => v.total}
                detail={(v) =>
                  v.bySource.length === 0
                    ? 'No entries yet today'
                    : v.bySource
                        .slice(0, 3)
                        .map((s) => `${s.sourceService} ${formatNumber(s.count)}`)
                        .join(' · ')
                }
              />
            </Panel>
          </>
        }
      />
    </>
  );
};
