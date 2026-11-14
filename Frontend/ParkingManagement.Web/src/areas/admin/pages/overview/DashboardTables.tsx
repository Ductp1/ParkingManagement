import React, { useMemo, useState } from 'react';
import { Link } from 'react-router-dom';
import { DataTable, type DataTableColumn } from '../../components/DataTable';
import { Panel } from '../../components/Panel';
import { StateBlock } from '../../components/StateBlock';
import { formatNumber, formatPercent, formatVnd } from '../../utils/format';
import {
  deviceTypeLabel,
  integrationTierLabel,
  isHiddenWidget,
  lotStatusLabel,
  OCCUPANCY_WARNING_PERCENT,
  paymentMethodLabel,
} from './dashboardFormat';
import { AsOfNote, WidgetTablePlaceholder } from './DashboardWidgetState';
import type {
  DashboardWidget,
  DistrictOccupancyRowDto,
  GateDeviceTypeRowDto,
  GateDevicesDto,
  LotOccupancyDto,
  LotOccupancyRowDto,
  PaymentMethodRowDto,
  PaymentsByMethodDto,
} from './dashboardTypes';

const LOADING = <StateBlock type="loading" bare />;

/** Thân panel theo trạng thái widget: đang tải lần đầu / lỗi nguồn / có dữ liệu (chưa có nguồn thì cả panel bị ẩn ở ngoài). */
const WidgetBody = <T,>({ widget, label, children }: { widget: DashboardWidget<T> | null; label: string; children: (value: T) => React.ReactNode }) => {
  if (widget === null) return LOADING;
  if (widget.status !== 'available' || widget.value === null) return <WidgetTablePlaceholder widget={widget} label={label} />;
  return <>{children(widget.value)}</>;
};

// ===================== Lot occupancy =====================

/** Thanh % lấp đầy; từ 90% chuyển màu cảnh báo kèm chữ "Nearly full" (không chỉ dựa vào màu). */
const OccupancyBar: React.FC<{ percent: number; label: string }> = ({ percent, label }) => {
  const warning = percent >= OCCUPANCY_WARNING_PERCENT;
  return (
    <span className="flex min-w-[140px] items-center gap-2">
      <span
        role="meter"
        aria-label={`Occupancy of ${label}`}
        aria-valuemin={0}
        aria-valuemax={100}
        aria-valuenow={Math.min(100, percent)}
        aria-valuetext={formatPercent(percent)}
        className="relative h-2 flex-1 overflow-hidden rounded-[2px] bg-(--admin-surface-page)"
      >
        <span
          className={`adm-tr-colors absolute inset-y-0 left-0 ${warning ? 'bg-(--admin-status-danger)' : 'bg-(--admin-brand-navy-800)'}`}
          style={{ width: `${Math.min(100, percent)}%` }}
        />
      </span>
      <span className={`adm-type-body-default w-14 text-right tabular-nums ${warning ? 'text-(--admin-status-danger)' : ''}`}>
        {formatPercent(percent)}
      </span>
      {warning && <span className="adm-type-body-small whitespace-nowrap text-(--admin-status-danger)">Nearly full</span>}
    </span>
  );
};

const LotFlags: React.FC<{ row: LotOccupancyRowDto }> = ({ row }) => (
  <span className="flex flex-wrap gap-1">
    {row.isEmergencyStopped && (
      <span className="adm-type-body-small rounded-[2px] border border-(--admin-status-danger) px-1.5 text-(--admin-status-danger)">Emergency stop</span>
    )}
    {row.instantBookingPaused && (
      <span className="adm-type-body-small rounded-[2px] border border-(--admin-border-default) px-1.5 text-(--admin-text-secondary)">
        Instant booking paused
      </span>
    )}
    {!row.isEmergencyStopped && !row.instantBookingPaused && <span className="adm-type-body-small text-(--admin-text-secondary)">—</span>}
  </span>
);

const occupied = (active: number, capacity: number) => `${formatNumber(active)} / ${formatNumber(capacity)}`;

/** Cột theo Figma "Lot occupancy & capacity": bãi (tên + quận, thành phố) · lấp đầy · tích hợp · trạng thái bãi · cờ sức chứa. */
const LOT_COLUMNS: DataTableColumn<LotOccupancyRowDto>[] = [
  {
    key: 'name',
    header: 'Lot',
    // Cột tên bãi không bị các cột cố định ép hẹp: tối thiểu 200px, bảng cuộn ngang trong panel khi màn hẹp.
    render: (r) => (
      <span className="flex min-w-[200px] flex-col">
        <Link
          to={`/admin/operations/parking-lots/${r.parkingLotId}`}
          className="adm-type-body-strong text-(--admin-brand-navy-900) hover:underline focus-visible:outline-2 focus-visible:outline-(--admin-brand-navy-900)"
        >
          {r.name}
        </Link>
        <span className="adm-type-body-small text-(--admin-text-secondary)">{[r.district ?? 'Unknown district', r.city].join(', ')}</span>
      </span>
    ),
  },
  {
    key: 'occupied',
    header: 'Parked / Capacity',
    width: 130,
    align: 'right',
    render: (r) => (
      <span className="tabular-nums" title={r.capacitySource === 'parking' ? "Capacity is the lot's total slots (gate capacity not configured yet)" : undefined}>
        {occupied(r.activeSessions, r.capacity)}
        {r.capacitySource === 'parking' && <span className="text-(--admin-text-secondary)">*</span>}
      </span>
    ),
  },
  { key: 'percent', header: 'Occupancy', width: 210, render: (r) => <OccupancyBar percent={r.occupancyPercent} label={r.name} /> },
  { key: 'integration', header: 'Integration', width: 100, render: (r) => integrationTierLabel(r.integrationTier) },
  {
    key: 'status',
    header: 'Lot status',
    width: 110,
    render: (r) => (
      <span className={r.lotStatus === 'Active' ? '' : 'text-(--admin-status-danger)'}>{lotStatusLabel(r.lotStatus)}</span>
    ),
  },
  { key: 'flags', header: 'Capacity flags', width: 150, render: (r) => <LotFlags row={r} /> },
];

const districtName = (r: DistrictOccupancyRowDto) => `${r.district ?? 'Unknown district'}, ${r.city}`;

const DISTRICT_COLUMNS: DataTableColumn<DistrictOccupancyRowDto>[] = [
  { key: 'district', header: 'District', render: (r) => districtName(r) },
  { key: 'lots', header: 'Lots', width: 90, align: 'right', render: (r) => formatNumber(r.lotCount) },
  { key: 'occupied', header: 'Parked / Capacity', width: 130, align: 'right', render: (r) => occupied(r.activeSessions, r.capacity) },
  { key: 'percent', header: 'Occupancy', width: 210, render: (r) => <OccupancyBar percent={r.occupancyPercent} label={districtName(r)} /> },
];

type OccupancyView = 'lot' | 'district';
type SortDirection = 'desc' | 'asc';

const ToggleButton: React.FC<{ pressed: boolean; onClick: () => void; children: React.ReactNode }> = ({ pressed, onClick, children }) => (
  <button
    type="button"
    aria-pressed={pressed}
    onClick={onClick}
    className={`adm-type-body-small adm-tr-colors h-7 rounded-[2px] border px-2.5 focus-visible:outline-2 focus-visible:outline-offset-1 focus-visible:outline-(--admin-brand-navy-900) ${
      pressed
        ? 'border-(--admin-brand-navy-900) bg-(--admin-brand-navy-900) text-(--admin-text-on-navy)'
        : 'border-(--admin-border-default) bg-(--admin-surface-card) text-(--admin-brand-navy-900) hover:bg-(--admin-surface-selected)'
    }`}
  >
    {children}
  </button>
);

const sortByPercent = <T extends { occupancyPercent: number }>(rows: T[], direction: SortDirection) =>
  [...rows].sort((a, b) => (direction === 'desc' ? b.occupancyPercent - a.occupancyPercent : a.occupancyPercent - b.occupancyPercent));

const OccupancyTables: React.FC<{ value: LotOccupancyDto; asOfUtc: string | null; busy: boolean }> = ({ value, asOfUtc, busy }) => {
  const [view, setView] = useState<OccupancyView>('lot');
  const [direction, setDirection] = useState<SortDirection>('desc');
  const lots = useMemo(() => sortByPercent(value.lots, direction), [value.lots, direction]);
  const districts = useMemo(() => sortByPercent(value.byDistrict, direction), [value.byDistrict, direction]);
  const hasFallbackCapacity = value.lots.some((l) => l.capacitySource === 'parking');

  return (
    <>
      <div className="flex flex-wrap items-center justify-between gap-2 border-b border-(--admin-border-default) px-3 py-2">
        <div role="group" aria-label="Show occupancy by" className="flex gap-1">
          <ToggleButton pressed={view === 'lot'} onClick={() => setView('lot')}>
            By lot
          </ToggleButton>
          <ToggleButton pressed={view === 'district'} onClick={() => setView('district')}>
            By district
          </ToggleButton>
        </div>
        <div className="flex flex-wrap items-center gap-3">
          <span className="adm-type-body-small text-(--admin-text-secondary)">
            All lots {occupied(value.totalActive, value.totalCapacity)} · {formatPercent(value.occupancyPercent)}
          </span>
          <AsOfNote asOfUtc={asOfUtc} />
          <button
            type="button"
            onClick={() => setDirection((d) => (d === 'desc' ? 'asc' : 'desc'))}
            aria-label={direction === 'desc' ? 'Sorted by occupancy, highest first. Sort lowest first' : 'Sorted by occupancy, lowest first. Sort highest first'}
            className="adm-type-link-small rounded-[2px] px-1 text-(--admin-brand-navy-900) hover:underline focus-visible:outline-2 focus-visible:outline-(--admin-brand-navy-900)"
          >
            Occupancy {direction === 'desc' ? '↓' : '↑'}
          </button>
        </div>
      </div>
      <div className="overflow-x-auto" data-testid={`occupancy-${view}`}>
        {view === 'lot' ? (
          <DataTable
            bare
            columns={LOT_COLUMNS}
            rows={lots}
            rowKey={(r) => r.parkingLotId}
            busy={busy}
            placeholder={<StateBlock type="empty" bare title="No live lots yet" message="Lots appear here once their KYB review is approved." />}
          />
        ) : (
          <DataTable
            bare
            columns={DISTRICT_COLUMNS}
            rows={districts}
            rowKey={(r) => `${r.city}|${r.district ?? ''}`}
            busy={busy}
            placeholder={<StateBlock type="empty" bare title="No live lots yet" message="Lots appear here once their KYB review is approved." />}
          />
        )}
      </div>
      {view === 'lot' && hasFallbackCapacity && (
        <p className="adm-type-body-small m-0 px-3 py-2 text-(--admin-text-secondary)">
          * Gate capacity not configured yet: the lot&apos;s total declared slots are used.
        </p>
      )}
    </>
  );
};

export const LotOccupancyPanel: React.FC<{ widget: DashboardWidget<LotOccupancyDto> | null; busy: boolean }> = ({ widget, busy }) =>
  isHiddenWidget(widget) ? null : (
    <Panel title="Lot occupancy" description="Vehicles parked now / capacity · lots waiting for KYB approval are not counted" padded={false}>
      <WidgetBody widget={widget} label="Lot occupancy">
        {(value) => <OccupancyTables value={value} asOfUtc={widget?.asOfUtc ?? null} busy={busy} />}
      </WidgetBody>
    </Panel>
  );

// ===================== Gate devices =====================

const deviceCount = (n: number, highlight: boolean) => (
  <span className={`tabular-nums ${highlight && n > 0 ? 'font-semibold text-(--admin-status-danger)' : ''}`}>{formatNumber(n)}</span>
);

const DEVICE_COLUMNS: DataTableColumn<GateDeviceTypeRowDto>[] = [
  { key: 'type', header: 'Device type', render: (r) => deviceTypeLabel(r.deviceType) },
  { key: 'online', header: 'Online', width: 90, align: 'right', render: (r) => deviceCount(r.online, false) },
  { key: 'offline', header: 'Offline', width: 90, align: 'right', render: (r) => deviceCount(r.offline, true) },
  { key: 'maintenance', header: 'Maintenance', width: 110, align: 'right', render: (r) => deviceCount(r.maintenance, false) },
  { key: 'total', header: 'Total', width: 80, align: 'right', render: (r) => formatNumber(r.total) },
];

export const GateDevicesPanel: React.FC<{ widget: DashboardWidget<GateDevicesDto> | null; busy: boolean }> = ({ widget, busy }) =>
  isHiddenWidget(widget) ? null : (
    <Panel
      title="Gate devices"
      description="Devices in use by type and status"
      action={{ label: 'All devices →', to: '/admin/operations/gate-activity/devices' }}
      padded={false}
    >
      <WidgetBody widget={widget} label="Gate devices">
        {(value) => (
          <>
            <div className="flex flex-wrap items-center justify-between gap-2 border-b border-(--admin-border-default) px-3 py-2">
              <span className={`adm-type-body-small ${value.offline > 0 ? 'text-(--admin-status-danger)' : 'text-(--admin-text-secondary)'}`}>
                {value.offline > 0 ? `${formatNumber(value.offline)} offline` : 'No devices offline'} · total{' '}
                {formatNumber(value.total)}
              </span>
              <AsOfNote asOfUtc={widget?.asOfUtc ?? null} />
            </div>
            <div className="overflow-x-auto" data-testid="gate-devices">
              <DataTable
                bare
                columns={DEVICE_COLUMNS}
                rows={value.byType}
                rowKey={(r) => r.deviceType}
                busy={busy}
                placeholder={<StateBlock type="empty" bare title="No gate devices yet" message="Devices that owners register at their gates appear here." />}
              />
            </div>
          </>
        )}
      </WidgetBody>
    </Panel>
  );

// ===================== Payments by method =====================

const PAYMENT_COLUMNS: DataTableColumn<PaymentMethodRowDto>[] = [
  { key: 'method', header: 'Method', render: (r) => paymentMethodLabel(r.method) },
  { key: 'count', header: 'Payments', width: 120, align: 'right', render: (r) => formatNumber(r.count) },
  { key: 'gross', header: 'Gross revenue', width: 170, align: 'right', render: (r) => <span className="tabular-nums">{formatVnd(r.gross)}</span> },
];

export const PaymentsByMethodPanel: React.FC<{ widget: DashboardWidget<PaymentsByMethodDto> | null; busy: boolean }> = ({ widget, busy }) =>
  isHiddenWidget(widget) ? null : (
    <Panel title="Today's payments by method" description="Totals only – no individual payments or customers" padded={false}>
      <WidgetBody widget={widget} label="Payments by method">
        {(value) => (
          <>
            <div className="flex justify-end border-b border-(--admin-border-default) px-3 py-2">
              <AsOfNote asOfUtc={widget?.asOfUtc ?? null} />
            </div>
            <div className="overflow-x-auto" data-testid="payments-by-method">
              <DataTable
                bare
                columns={PAYMENT_COLUMNS}
                rows={value.methods}
                rowKey={(r) => r.method}
                busy={busy}
                placeholder={<StateBlock type="empty" bare title="No payments today" message="No successful payments yet today (ICT)." />}
              />
            </div>
          </>
        )}
      </WidgetBody>
    </Panel>
  );
