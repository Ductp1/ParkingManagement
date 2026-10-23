import React, { useState } from 'react';
import { Link } from 'react-router-dom';
import { adminKybApi } from '@/api/adminKybApi';
import { adminPartnerOnboardingApi } from '@/api/adminPartnerOnboardingApi';
import { KybStatus, PartnerApplicationStatus } from '@/types/adminEnums';
import { AdminTitleBar } from '../../layout/AdminTitleBar';
import { KpiStrip, KpiTile } from '../../components/KpiTile';
import { DetailColumns } from '../../components/PageLayout';
import { Panel } from '../../components/Panel';
import { DataTable } from '../../components/DataTable';
import { StateBlock } from '../../components/StateBlock';
import { FutureTag } from '../../components/FutureTag';
import { useAdminQuery, type AdminQueryResult } from '../../hooks/useAdminQuery';
import { formatDate, formatNumber } from '../../utils/format';

const NO_AGGREGATE = 'No aggregate endpoint yet';

const KPIS = ['Revenue today (succeeded payments)', 'Active parking sessions', 'Bookings today', 'Open disputes'];

const QueueRow: React.FC<{ label: string; to: string; query?: AdminQueryResult<number>; future?: boolean }> = ({ label, to, query, future }) => (
  <Link
    to={to}
    className="flex h-11 items-center justify-between gap-3 border-b border-(--admin-border-default) px-3 last:border-b-0 hover:bg-(--admin-surface-selected)"
  >
    <span className="adm-type-body-default flex items-center gap-2 text-(--admin-text-primary)">
      {label}
      {future && <FutureTag />}
    </span>
    <span className="adm-type-kpi-value-sm text-(--admin-text-primary)" title={query && query.status !== 'ready' ? query.error?.message ?? NO_AGGREGATE : undefined}>
      {query?.status === 'loading' ? '…' : query?.status === 'ready' ? formatNumber(query.data) : '—'}
    </span>
  </Link>
);

/**
 * Overview / Dashboard. Chưa có endpoint tổng hợp (Payments, ParkingSessions, Slots…) → KPI và bảng hiển thị
 * trạng thái thật "chưa có nguồn"; hàng đợi KYB / Partner lấy totalCount từ API danh sách.
 */
export const DashboardPage: React.FC = () => {
  const [today] = useState(() => formatDate(new Date().toISOString()));

  const kybQueue = useAdminQuery(async () => {
    const [submitted, underReview] = await Promise.all([
      adminKybApi.list({ status: KybStatus.Submitted, page: 1, pageSize: 1 }),
      adminKybApi.list({ status: KybStatus.UnderReview, page: 1, pageSize: 1 }),
    ]);
    return submitted.totalCount + underReview.totalCount;
  }, []);
  const partnerQueue = useAdminQuery(
    async () => (await adminPartnerOnboardingApi.list({ status: PartnerApplicationStatus.Submitted, page: 1, pageSize: 1 })).totalCount,
    []
  );

  return (
    <>
      <AdminTitleBar
        title="Platform overview"
        subtitle={`Today ${today} · ICT · aggregates from Payments, ParkingSessions, Slots, GateDevices, Complaints, RiskFlags, KybApplications`}
      />
      <KpiStrip>
        {KPIS.map((k) => (
          <KpiTile key={k} label={k} value="—" delta={NO_AGGREGATE} />
        ))}
      </KpiStrip>
      <DetailColumns
        main={
          <>
            <Panel title="Lot occupancy & capacity" description="ParkingLots.AvailableSlots / TotalSlots · LotCapacityConfigs flags" action={{ label: 'View all lots →', to: '/admin/operations/parking-lots' }} padded={false}>
              <DataTable
                bare
                rows={[]}
                rowKey={() => 0}
                columns={['Parking lot', 'Occupancy', 'Integration', 'Lot status', 'Capacity flag'].map((h) => ({ key: h, header: h, render: () => null }))}
                placeholder={<StateBlock type="unavailable" bare message="ParkingService has no admin lot list / occupancy endpoint yet." />}
              />
            </Panel>
            <Panel title="Recent payments" action={{ label: 'All transactions →', to: '/admin/finance/transactions' }} padded={false}>
              <DataTable
                bare
                rows={[]}
                rowKey={() => 0}
                columns={['Payment', 'Booking', 'Method', 'Purpose', 'Amount', 'Status', 'Paid at'].map((h) => ({ key: h, header: h, render: () => null }))}
                placeholder={<StateBlock type="unavailable" bare message="PaymentService has no admin payments list yet (only ?bookingId=)." />}
              />
            </Panel>
          </>
        }
        side={
          <Panel title="Work queues" padded={false}>
            <QueueRow label="KYB applications to review" to="/admin/operations/kyb-review?status=Submitted" query={kybQueue} />
            <QueueRow label="Partner applications" to="/admin/operations/partner-onboarding?status=Submitted" query={partnerQueue} future />
            <QueueRow label="Refunds requested" to="/admin/finance/refunds?status=Requested" />
            <QueueRow label="Settlements pending review" to="/admin/finance/settlements?status=PendingReview" />
            <QueueRow label="Disputes awaiting owner > 48h" to="/admin/support/disputes?status=AwaitingOwner" />
          </Panel>
        }
      />
    </>
  );
};
