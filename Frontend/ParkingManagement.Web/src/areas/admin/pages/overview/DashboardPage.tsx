import React, { useState } from 'react';
import { Link } from 'react-router-dom';
import { AdminTitleBar } from '../../layout/AdminTitleBar';
import { AdminButton } from '../../components/AdminButton';
import { KpiStrip } from '../../components/KpiTile';
import { DetailColumns, QueryIssueState } from '../../components/PageLayout';
import { isQueryIssue } from '../../utils/queryStatus';
import { Panel } from '../../components/Panel';
import { StateBlock } from '../../components/StateBlock';
import { formatClockIct, formatDate, formatDateTimeIct, formatNumber, formatPercent, formatVnd, ictDateParam } from '../../utils/format';
import { AdminIcon } from '../../components/AdminIcon';
import { ADMIN_ICONS, type AdminIconName } from '../../components/adminIcons';
import { KpiWidget, QueueRow } from './DashboardWidgetState';
import { isHiddenWidget, OCCUPANCY_WARNING_PERCENT } from './dashboardFormat';
import { DashboardRetryContext } from './dashboardRetry';
import { GateDevicesPanel, LotOccupancyPanel, PaymentsByMethodPanel } from './DashboardTables';
import { REFRESH_INTERVAL_MS, useDashboardOverview } from './useDashboardOverview';
import type { BookingsTodayDto, DashboardOverviewDto, DashboardWidget, LotOccupancyDto } from './dashboardTypes';

const auditLink = (todayParam: string | null, service?: string) => {
  const params = new URLSearchParams();
  if (todayParam) {
    params.set('from', todayParam);
    params.set('to', todayParam);
  }
  if (service) params.set('service', service);
  const query = params.toString();
  return `/admin/access/audit-log${query ? `?${query}` : ''}`;
};

/** Breakdown booking: chỉ hiện phần nguồn đã tách được (null = chưa tách, không hiện 0). */
const bookingBreakdown = (v: BookingsTodayDto) => {
  const parts = [`Completed ${formatNumber(v.completed)}`, `Cancelled ${formatNumber(v.cancelled + v.cancelledNoRefund)}`];
  if (v.noShow !== null) parts.push(`No-show ${formatNumber(v.noShow)}`);
  if (v.cancelledByDriver !== null && v.cancelledByLot !== null)
    parts.push(`By driver ${formatNumber(v.cancelledByDriver)} · By lot ${formatNumber(v.cancelledByLot)}`);
  return parts.join(' · ');
};

/** Cả nhóm widget chưa có nguồn → ẩn luôn khung chứa (KPI strip / panel). Đang tải lần đầu (null) thì vẫn hiện skeleton. */
const allHidden = (widgets: Array<DashboardWidget<unknown>> | null): boolean => widgets !== null && widgets.every(isHiddenWidget);

const Badge: React.FC<{ tone: 'danger' | 'muted'; children: React.ReactNode }> = ({ tone, children }) => (
  <span
    className={`adm-type-body-small inline-block rounded-[2px] border px-1.5 ${
      tone === 'danger' ? 'border-(--admin-status-danger) text-(--admin-status-danger)' : 'border-(--admin-border-default) text-(--admin-text-secondary)'
    }`}
  >
    {children}
  </span>
);

/** Dải KPI: doanh thu (gộp + ròng), phiên đang hoạt động (+ badge bất thường), booking hôm nay, tranh chấp đang mở. */
/** KPI "Current occupancy": đang đỗ / sức chứa toàn sàn (cùng nguồn bảng lấp đầy), % + chỗ còn trống; ≥ 90% tô cam. */
const occupancyFormat = (occupancy: DashboardWidget<LotOccupancyDto> | null) => {
  const capacity = occupancy?.status === 'available' && occupancy.value ? occupancy.value.totalCapacity : 0;
  return (n: number) => `${formatNumber(n)} / ${formatNumber(capacity)}`;
};

/** Dải KPI (thiết kế "Facility operations overview"): doanh thu · lấp đầy · phiên đang đỗ · booking · tranh chấp. Ô chưa có nguồn bị ẩn. */
const KpiRow: React.FC<{ data: DashboardOverviewDto | null }> = ({ data }) => {
  const occupancy = data?.tables.lotOccupancy ?? null;
  if (allHidden(data ? [...Object.values(data.kpis), data.tables.lotOccupancy] : null)) return null;
  return (
    <KpiStrip>
      <KpiWidget
        label="Revenue today (gross)"
        widget={data?.kpis.revenueToday ?? null}
        count={(v) => v.gross}
        format={formatVnd}
        detail={(v) => `Net ${formatVnd(v.net)} · refunded ${formatVnd(v.refunded)} · ${formatNumber(v.succeededCount)} payments`}
      />
      <KpiWidget
        label="Current occupancy"
        widget={occupancy}
        count={(v) => v.totalActive}
        format={occupancyFormat(occupancy)}
        to="/admin/operations/parking-lots"
        detail={(v) => (
          <span className={v.occupancyPercent >= OCCUPANCY_WARNING_PERCENT ? 'text-(--admin-status-warning-text)' : undefined}>
            {formatPercent(v.occupancyPercent)} full · {formatNumber(Math.max(0, v.totalCapacity - v.totalActive))} slots free
          </span>
        )}
      />
      <KpiWidget
        label="Active parking sessions"
        widget={data?.kpis.activeSessions ?? null}
        count={(v) => v.total}
        to="/admin/operations/gate-activity"
        detail={(v) => (
          <span className="flex flex-wrap items-center gap-1.5">
            <span>
              Walk-in {formatNumber(v.walkIn)} · Booked {formatNumber(v.booked)}
            </span>
            <Badge tone={v.abnormal > 0 ? 'danger' : 'muted'}>Abnormal {formatNumber(v.abnormal)}</Badge>
          </span>
        )}
      />
      <KpiWidget label="Bookings created today" widget={data?.kpis.bookingsToday ?? null} count={(v) => v.created} detail={bookingBreakdown} />
      <KpiWidget
        label="Open disputes"
        widget={data?.kpis.openDisputes ?? null}
        count={(v) => v.unresolved}
        detail={(v) =>
          `New ${formatNumber(v.open)} · Awaiting owner ${formatNumber(v.awaitingOwner)} · Owner replied ${formatNumber(v.ownerResponded)} · Escalated ${formatNumber(v.escalated)}`
        }
      />
    </KpiStrip>
  );
};

/** Lối tắt tới các trang admin đã có (thiết kế "Operational quick actions") – không có hành động nào chưa có API. */
const QUICK_ACTIONS: Array<{ icon: AdminIconName; title: string; description: string; to: string }> = [
  { icon: 'file', title: 'Review KYB applications', description: 'Check documents and approve new lots', to: '/admin/operations/kyb-review?status=Submitted' },
  { icon: 'user', title: 'Partner applications', description: 'New owners waiting for review', to: '/admin/operations/partner-onboarding?status=Submitted' },
  { icon: 'ban', title: 'Sanctions', description: 'Issue or review SLA sanctions', to: '/admin/support/sanctions' },
  { icon: 'send', title: 'Send a notification', description: 'Message one user from a template', to: '/admin/communications/send-notification' },
  { icon: 'eye', title: 'Gate activity', description: 'Sessions and devices at the gates', to: '/admin/operations/gate-activity' },
];

const QuickActionsPanel: React.FC = () => (
  <Panel title="Quick actions" padded={false}>
    <ul className="m-0 list-none p-0">
      {QUICK_ACTIONS.map((a) => (
        <li key={a.to} className="border-b border-(--admin-border-default) last:border-b-0">
          <Link
            to={a.to}
            className="adm-tr-colors flex min-h-12 items-center gap-3 px-3 py-2 text-(--admin-text-primary) hover:bg-(--admin-surface-selected)"
          >
            <span aria-hidden="true" className="flex size-7 shrink-0 items-center justify-center rounded-[2px] bg-(--admin-surface-page) text-(--admin-brand-navy-900)">
              <AdminIcon icon={ADMIN_ICONS[a.icon]} />
            </span>
            <span className="flex min-w-0 flex-1 flex-col">
              <span className="adm-type-body-strong">{a.title}</span>
              <span className="adm-type-body-small text-(--admin-text-secondary)">{a.description}</span>
            </span>
            <span aria-hidden="true" className="text-(--admin-text-secondary)">
              <AdminIcon icon={ADMIN_ICONS.chevronRight} />
            </span>
          </Link>
        </li>
      ))}
    </ul>
  </Panel>
);

const QueuesPanel: React.FC<{ data: DashboardOverviewDto | null }> = ({ data }) => {
  const q = data?.queues ?? null;
  if (allHidden(q ? Object.values(q) : null)) return null;
  return (
    <Panel title="Work queues" padded={false}>
      <QueueRow
        label="KYB applications to review"
        to="/admin/operations/kyb-review?status=Submitted"
        widget={q?.kybApplications ?? null}
        count={(v) => v.toReview}
        detail={(v) => `${formatNumber(v.underReview)} under review · ${formatNumber(v.needsMoreInfo)} waiting for the owner`}
      />
      <QueueRow
        label="Partner applications"
        to="/admin/operations/partner-onboarding?status=Submitted"
        widget={q?.partnerApplications ?? null}
        count={(v) => v.toReview}
        detail={(v) => `${formatNumber(v.needsMoreInfo)} need more info · ${formatNumber(v.accountIssued)} awaiting activation`}
      />
      <QueueRow
        label="Lot contracts to sign"
        to="/admin/operations/lot-contracts?status=Pending"
        widget={q?.lotContractsToSign ?? null}
        count={(v) => v.pending}
      />
      <QueueRow
        label="Unresolved risk flags"
        widget={q?.riskFlags ?? null}
        count={(v) => v.unresolved}
        urgent={(v) => v.critical > 0 || v.overdue > 0}
        detail={(v) => (
          <span className="flex flex-wrap gap-1.5">
            <Badge tone={v.critical > 0 ? 'danger' : 'muted'}>Critical {formatNumber(v.critical)}</Badge>
            <Badge tone={v.overdue > 0 ? 'danger' : 'muted'}>Overdue {formatNumber(v.overdue)}</Badge>
            <span>{formatNumber(v.investigating)} investigating</span>
          </span>
        )}
      />
      <QueueRow label="Refunds awaiting approval" widget={q?.refundsRequested ?? null} count={(v) => v.requested} />
      <QueueRow label="Settlements awaiting review" widget={q?.settlementsPendingReview ?? null} count={(v) => v.pendingReview} />
      <QueueRow
        label="Disputes with owner reply over 48 h"
        widget={q?.disputesAwaitingOwner ?? null}
        count={(v) => v.overdue}
        urgent={(v) => v.overdue > 0}
        detail={(v) => `Of ${formatNumber(v.awaitingOwner)} disputes awaiting the owner`}
      />
      <QueueRow
        label="Personal data requests"
        widget={q?.dataSubjectRequestsDue ?? null}
        count={(v) => v.open}
        urgent={(v) => v.overdue > 0}
        detail={(v) => (
          <span className="flex flex-wrap gap-1.5">
            <Badge tone="muted">Due soon {formatNumber(v.dueSoon)}</Badge>
            <Badge tone={v.overdue > 0 ? 'danger' : 'muted'}>Overdue {formatNumber(v.overdue)}</Badge>
          </span>
        )}
      />
    </Panel>
  );
};

const GovernancePanel: React.FC<{ data: DashboardOverviewDto | null }> = ({ data }) => {
  const g = data?.governance ?? null;
  const todayParam = data ? ictDateParam(data.todayFromUtc) : null;
  const next = g?.scheduledConfigChanges.status === 'available' ? g.scheduledConfigChanges.value : null;
  if (allHidden(g ? Object.values(g) : null)) return null;
  return (
    <Panel title="Platform governance" description="Sanctions · scheduled settings · today's audit log (ICT)" padded={false}>
      <QueueRow
        label="Active sanctions"
        to="/admin/support/sanctions?status=Active"
        widget={g?.activeSanctions ?? null}
        count={(v) => v.total}
        detail={(v) =>
          `Warning ${formatNumber(v.warning)} · Ranking penalty ${formatNumber(v.rankingPenalty)} · Suspended ${formatNumber(v.temporarySuspension)} · Banned ${formatNumber(v.permanentBan)}`
        }
      />
      <QueueRow
        label="Scheduled setting changes"
        to={next?.nextKey ? `/admin/access/system-settings/${encodeURIComponent(next.nextKey)}` : '/admin/access/system-settings'}
        widget={g?.scheduledConfigChanges ?? null}
        count={(v) => v.count}
        detail={(v) => (v.nextKey ? `Next: ${v.nextKey} at ${formatDateTimeIct(v.nextEffectiveFromUtc)}` : 'Nothing scheduled')}
      />
      <QueueRow
        label="Audit log today"
        to={auditLink(todayParam)}
        widget={g?.auditLogsToday ?? null}
        count={(v) => v.total}
        detail={(v) =>
          v.bySource.length === 0 ? (
            'No entries yet today'
          ) : (
            <span className="flex flex-wrap gap-x-2">
              {v.bySource.map((s) => (
                <Link
                  key={s.sourceService}
                  to={auditLink(todayParam, s.sourceService)}
                  aria-label={`Today's audit log from ${s.sourceService}: ${formatNumber(s.count)} entries`}
                  className="rounded-[2px] text-(--admin-brand-navy-900) hover:underline focus-visible:outline-2 focus-visible:outline-(--admin-brand-navy-900)"
                >
                  {s.sourceService} {formatNumber(s.count)}
                </Link>
              ))}
            </span>
          )
        }
      />
    </Panel>
  );
};

/**
 * Overview / Dashboard (US-081, UC-45): GET /api/v1/admin/dashboard/overview. Mỗi widget tự mang trạng thái: có số (count-up lần
 * đầu), chưa có nguồn ("Chưa có nguồn dữ liệu · X") hoặc nguồn lỗi (+ "Thử lại") – một nguồn lỗi không làm hỏng cả trang.
 * Không có số mẫu, không có số 0 giả. Làm mới: nút thủ công + tự động 60 giây khi tab đang hiển thị, không gửi chồng request,
 * giữ dữ liệu cũ trong lúc tải lại.
 */
export const DashboardPage: React.FC = () => {
  const query = useDashboardOverview();
  const { data, refresh, isFetching } = query;
  // Ngày hiển thị trước khi có dữ liệu: chốt một lần lúc mở màn (không gọi Date trong mỗi lần render).
  const [openedAt] = useState(() => new Date().toISOString());

  const dayLabel = formatDate(data?.todayFromUtc ?? openedAt);
  const generated = data ? `updated ${formatClockIct(data.generatedAtUtc)}` : 'loading…';
  const titleBar = (
    <AdminTitleBar
      title="Platform overview"
      subtitle={`Live occupancy, work queues and gate health across all lots · ${dayLabel} (ICT) · ${generated} · refreshes every ${REFRESH_INTERVAL_MS / 1000} seconds`}
      actions={
        <AdminButton
          variant="onNavy"
          icon="refresh"
          onClick={refresh}
          disabled={isFetching}
          loading={isFetching && data !== null}
          loadingLabel="Refreshing…"
          aria-label="Refresh dashboard figures"
        >
          Refresh
        </AdminButton>
      }
    />
  );
  const liveRegion = (
    <p className="sr-only" aria-live="polite" role="status" data-testid="dashboard-live">
      {data && !isFetching ? `Figures updated at ${formatClockIct(data.generatedAtUtc)}` : ''}
    </p>
  );

  // Cả request lỗi lần đầu (mạng / 401 / 500…): StateBlock + Thử lại; 403 = không có quyền. Dữ liệu cũ (nếu có) thì giữ – nhánh dưới.
  if (!data && query.status !== 'loading') {
    return (
      <>
        {titleBar}
        {liveRegion}
        {query.status === 'forbidden' ? (
          <StateBlock type="noPermission" title="You cannot view the dashboard" message="The dashboard is for administrators." />
        ) : isQueryIssue(query.status) ? (
          <QueryIssueState status={query.status} error={query.error} onRetry={refresh} />
        ) : (
          <StateBlock
            type="error"
            title="Couldn't load the dashboard"
            message={query.error?.problem ? query.error.message : 'The server did not respond. Try again.'}
            onRetry={refresh}
          />
        )}
      </>
    );
  }

  return (
    <DashboardRetryContext.Provider value={refresh}>
      {titleBar}
      {liveRegion}
      {data && query.error && (
        <div
          role="alert"
          className="adm-motion-fade flex flex-wrap items-center gap-2 border border-(--admin-status-danger) bg-(--admin-surface-card) px-3 py-2"
        >
          <span className="adm-type-body-small text-(--admin-status-danger)">
            Couldn&apos;t refresh. Showing figures from {formatClockIct(data.generatedAtUtc)}.
          </span>
          <AdminButton variant="secondary" icon="refresh" onClick={refresh} disabled={isFetching}>
            Retry
          </AdminButton>
        </div>
      )}
      <KpiRow data={data} />
      <DetailColumns
        main={
          <>
            <LotOccupancyPanel widget={data?.tables.lotOccupancy ?? null} busy={isFetching && data !== null} />
            <PaymentsByMethodPanel widget={data?.tables.paymentsByMethodToday ?? null} busy={isFetching && data !== null} />
            <GovernancePanel data={data} />
          </>
        }
        side={
          <>
            <QueuesPanel data={data} />
            <QuickActionsPanel />
            <GateDevicesPanel widget={data?.tables.gateDevices ?? null} />
          </>
        }
      />
    </DashboardRetryContext.Provider>
  );
};
