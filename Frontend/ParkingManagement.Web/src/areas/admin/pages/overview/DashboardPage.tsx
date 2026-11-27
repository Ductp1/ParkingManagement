import React, { useState } from 'react';
import { Link } from 'react-router-dom';
import { AdminTitleBar } from '../../layout/AdminTitleBar';
import { AdminButton } from '../../components/AdminButton';
import { KpiStrip } from '../../components/KpiTile';
import { DetailColumns } from '../../components/PageLayout';
import { Panel } from '../../components/Panel';
import { StateBlock } from '../../components/StateBlock';
import { formatClockIct, formatDate, formatDateTimeIct, formatNumber, formatVnd, ictDateParam } from '../../utils/format';
import { KpiWidget, QueueRow } from './DashboardWidgetState';
import { DashboardRetryContext } from './dashboardRetry';
import { GateDevicesPanel, LotOccupancyPanel, PaymentsByMethodPanel } from './DashboardTables';
import { REFRESH_INTERVAL_MS, useDashboardOverview } from './useDashboardOverview';
import type { BookingsTodayDto, DashboardOverviewDto } from './dashboardTypes';

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
  const parts = [`Hoàn tất ${formatNumber(v.completed)}`, `Hủy ${formatNumber(v.cancelled + v.cancelledNoRefund)}`];
  if (v.noShow !== null) parts.push(`No-show ${formatNumber(v.noShow)}`);
  if (v.cancelledByDriver !== null && v.cancelledByLot !== null)
    parts.push(`Khách hủy ${formatNumber(v.cancelledByDriver)} · Bãi hủy ${formatNumber(v.cancelledByLot)}`);
  return parts.join(' · ');
};

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
const KpiRow: React.FC<{ data: DashboardOverviewDto | null }> = ({ data }) => (
  <KpiStrip>
    <KpiWidget
      label="Doanh thu hôm nay (gộp)"
      widget={data?.kpis.revenueToday ?? null}
      count={(v) => v.gross}
      format={formatVnd}
      detail={(v) => `Ròng ${formatVnd(v.net)} · đã hoàn ${formatVnd(v.refunded)} · ${formatNumber(v.succeededCount)} giao dịch`}
    />
    <KpiWidget
      label="Phiên gửi xe đang hoạt động"
      widget={data?.kpis.activeSessions ?? null}
      count={(v) => v.total}
      to="/admin/operations/gate-activity"
      detail={(v) => (
        <span className="flex flex-wrap items-center gap-1.5">
          <span>
            Vãng lai {formatNumber(v.walkIn)} · Đặt trước {formatNumber(v.booked)}
          </span>
          <Badge tone={v.abnormal > 0 ? 'danger' : 'muted'}>Bất thường {formatNumber(v.abnormal)}</Badge>
        </span>
      )}
    />
    <KpiWidget label="Booking tạo hôm nay" widget={data?.kpis.bookingsToday ?? null} count={(v) => v.created} detail={bookingBreakdown} />
    <KpiWidget
      label="Tranh chấp đang mở"
      widget={data?.kpis.openDisputes ?? null}
      count={(v) => v.unresolved}
      detail={(v) =>
        `Mới ${formatNumber(v.open)} · Chờ chủ bãi ${formatNumber(v.awaitingOwner)} · Đã phản hồi ${formatNumber(v.ownerResponded)} · Leo thang ${formatNumber(v.escalated)}`
      }
    />
  </KpiStrip>
);

const QueuesPanel: React.FC<{ data: DashboardOverviewDto | null }> = ({ data }) => {
  const q = data?.queues ?? null;
  return (
    <Panel title="Hàng đợi xử lý" padded={false}>
      <QueueRow
        label="Hồ sơ KYB cần duyệt"
        to="/admin/operations/kyb-review?status=Submitted"
        widget={q?.kybApplications ?? null}
        count={(v) => v.toReview}
        detail={(v) => `${formatNumber(v.underReview)} đang duyệt · ${formatNumber(v.needsMoreInfo)} chờ chủ bãi bổ sung`}
      />
      <QueueRow
        label="Hồ sơ đối tác"
        to="/admin/operations/partner-onboarding?status=Submitted"
        widget={q?.partnerApplications ?? null}
        count={(v) => v.toReview}
        detail={(v) => `${formatNumber(v.needsMoreInfo)} chờ bổ sung · ${formatNumber(v.accountIssued)} chờ kích hoạt`}
      />
      <QueueRow
        label="Hợp đồng bãi chờ ký"
        to="/admin/operations/lot-contracts?status=Pending"
        widget={q?.lotContractsToSign ?? null}
        count={(v) => v.pending}
      />
      <QueueRow
        label="Risk flag chưa xử lý"
        widget={q?.riskFlags ?? null}
        count={(v) => v.unresolved}
        urgent={(v) => v.critical > 0 || v.overdue > 0}
        detail={(v) => (
          <span className="flex flex-wrap gap-1.5">
            <Badge tone={v.critical > 0 ? 'danger' : 'muted'}>Nghiêm trọng {formatNumber(v.critical)}</Badge>
            <Badge tone={v.overdue > 0 ? 'danger' : 'muted'}>Quá hạn {formatNumber(v.overdue)}</Badge>
            <span>{formatNumber(v.investigating)} đang điều tra</span>
          </span>
        )}
      />
      <QueueRow label="Yêu cầu hoàn tiền chờ duyệt" widget={q?.refundsRequested ?? null} count={(v) => v.requested} />
      <QueueRow label="Kỳ quyết toán chờ duyệt" widget={q?.settlementsPendingReview ?? null} count={(v) => v.pendingReview} />
      <QueueRow
        label="Tranh chấp chờ chủ bãi quá hạn 48 giờ"
        widget={q?.disputesAwaitingOwner ?? null}
        count={(v) => v.overdue}
        urgent={(v) => v.overdue > 0}
        detail={(v) => `Trong ${formatNumber(v.awaitingOwner)} tranh chấp đang chờ chủ bãi`}
      />
      <QueueRow
        label="Yêu cầu dữ liệu cá nhân"
        widget={q?.dataSubjectRequestsDue ?? null}
        count={(v) => v.open}
        urgent={(v) => v.overdue > 0}
        detail={(v) => (
          <span className="flex flex-wrap gap-1.5">
            <Badge tone="muted">Sắp đến hạn {formatNumber(v.dueSoon)}</Badge>
            <Badge tone={v.overdue > 0 ? 'danger' : 'muted'}>Quá hạn {formatNumber(v.overdue)}</Badge>
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
  return (
    <Panel title="Quản trị nền tảng" description="Chế tài · thay đổi cấu hình · audit log hôm nay (ICT)" padded={false}>
      <QueueRow
        label="Chế tài đang hiệu lực"
        to="/admin/support/sanctions?status=Active"
        widget={g?.activeSanctions ?? null}
        count={(v) => v.total}
        detail={(v) =>
          `Cảnh cáo ${formatNumber(v.warning)} · Hạ hạng ${formatNumber(v.rankingPenalty)} · Tạm khóa ${formatNumber(v.temporarySuspension)} · Cấm vĩnh viễn ${formatNumber(v.permanentBan)}`
        }
      />
      <QueueRow
        label="Thay đổi cấu hình sắp hiệu lực"
        to={next?.nextKey ? `/admin/access/system-settings/${encodeURIComponent(next.nextKey)}` : '/admin/access/system-settings'}
        widget={g?.scheduledConfigChanges ?? null}
        count={(v) => v.count}
        detail={(v) => (v.nextKey ? `Tiếp theo: ${v.nextKey} lúc ${formatDateTimeIct(v.nextEffectiveFromUtc)}` : 'Không có thay đổi nào đã hẹn')}
      />
      <QueueRow
        label="Audit log hôm nay"
        to={auditLink(todayParam)}
        widget={g?.auditLogsToday ?? null}
        count={(v) => v.total}
        detail={(v) =>
          v.bySource.length === 0 ? (
            'Chưa có bản ghi nào hôm nay'
          ) : (
            <span className="flex flex-wrap gap-x-2">
              {v.bySource.map((s) => (
                <Link
                  key={s.sourceService}
                  to={auditLink(todayParam, s.sourceService)}
                  aria-label={`Audit log hôm nay của ${s.sourceService}: ${formatNumber(s.count)} bản ghi`}
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
  const generated = data ? `cập nhật lúc ${formatClockIct(data.generatedAtUtc)}` : 'đang tải…';
  const titleBar = (
    <AdminTitleBar
      title="Tổng quan nền tảng"
      subtitle={`Dữ liệu ngày ${dayLabel} (ICT) · ${generated} · tự làm mới mỗi ${REFRESH_INTERVAL_MS / 1000} giây khi tab đang mở`}
      actions={
        <AdminButton
          variant="onNavy"
          icon="refresh"
          onClick={refresh}
          disabled={isFetching}
          loading={isFetching && data !== null}
          loadingLabel="Đang làm mới…"
          aria-label="Làm mới số liệu dashboard"
        >
          Làm mới
        </AdminButton>
      }
    />
  );
  const liveRegion = (
    <p className="sr-only" aria-live="polite" role="status" data-testid="dashboard-live">
      {data && !isFetching ? `Đã cập nhật số liệu lúc ${formatClockIct(data.generatedAtUtc)}` : ''}
    </p>
  );

  // Cả request lỗi lần đầu (mạng / 401 / 500…): StateBlock + Thử lại; 403 = không có quyền. Dữ liệu cũ (nếu có) thì giữ – nhánh dưới.
  if (!data && query.status !== 'loading') {
    const status = query.error?.status ?? 0;
    return (
      <>
        {titleBar}
        {liveRegion}
        {query.status === 'forbidden' ? (
          <StateBlock type="noPermission" title="Không có quyền xem dashboard" message={query.error?.message} />
        ) : (
          <StateBlock
            type="error"
            title="Không tải được dashboard"
            message={
              status === 0
                ? `Không kết nối được AdminService qua API Gateway (${query.error?.message ?? 'lỗi mạng'}).`
                : `${query.error?.message ?? 'AdminService không phản hồi'} (HTTP ${status}).`
            }
            onRetry={refresh}
            retryLabel="Thử lại"
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
            Không làm mới được ({query.error.message}). Đang hiển thị số liệu lúc {formatClockIct(data.generatedAtUtc)}.
          </span>
          <AdminButton variant="secondary" icon="refresh" onClick={refresh} disabled={isFetching}>
            Thử lại
          </AdminButton>
        </div>
      )}
      <KpiRow data={data} />
      <DetailColumns
        main={
          <>
            <LotOccupancyPanel widget={data?.tables.lotOccupancy ?? null} busy={isFetching && data !== null} />
            <PaymentsByMethodPanel widget={data?.tables.paymentsByMethodToday ?? null} busy={isFetching && data !== null} />
          </>
        }
        side={
          <>
            <QueuesPanel data={data} />
            <GateDevicesPanel widget={data?.tables.gateDevices ?? null} busy={isFetching && data !== null} />
            <GovernancePanel data={data} />
          </>
        }
      />
    </DashboardRetryContext.Provider>
  );
};
