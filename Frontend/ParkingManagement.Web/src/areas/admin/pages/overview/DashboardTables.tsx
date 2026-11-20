import React, { useMemo, useState } from 'react';
import { Link } from 'react-router-dom';
import { CellText, DataTable, type DataTableColumn } from '../../components/DataTable';
import { Panel } from '../../components/Panel';
import { StateBlock } from '../../components/StateBlock';
import { StatusIndicator } from '../../components/StatusIndicator';
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

/** Ngưỡng chia trạng thái theo % lấp đầy (giống thiết kế "Live facility occupancy"). */
const AMPLE_BELOW_PERCENT = 50;

/**
 * Ô lấp đầy kiểu thiết kế: "x / y slots" bên trái, % bên phải, thanh tiến độ ngay dưới.
 * Từ 90% thanh và % đổi màu cảnh báo (kèm trạng thái "Near capacity" ở cột Status – không chỉ dựa vào màu).
 */
const OccupancyCell: React.FC<{ active: number; capacity: number; percent: number; label: string; fallback?: boolean }> = ({
  active,
  capacity,
  percent,
  label,
  fallback,
}) => {
  const warning = percent >= OCCUPANCY_WARNING_PERCENT;
  const width = Math.min(100, Math.max(0, percent));
  return (
    <span className="flex min-w-[180px] flex-col gap-1">
      <span className="flex items-baseline justify-between gap-2">
        <span
          className="adm-type-body-small tabular-nums text-(--admin-text-primary)"
          title={fallback ? "Capacity is the lot's total slots (gate capacity not configured yet)" : undefined}
        >
          {formatNumber(active)} / {formatNumber(capacity)} slots
          {fallback && <span className="text-(--admin-text-secondary)">*</span>}
        </span>
        <span className={`adm-type-body-strong tabular-nums ${warning ? 'text-(--admin-status-warning-text)' : 'text-(--admin-text-primary)'}`}>
          {formatPercent(percent)}
        </span>
      </span>
      <span
        role="meter"
        aria-label={`Occupancy of ${label}`}
        aria-valuemin={0}
        aria-valuemax={100}
        aria-valuenow={width}
        aria-valuetext={formatPercent(percent)}
        className="relative block h-1.5 overflow-hidden rounded-full bg-(--admin-chart-track)"
      >
        <span
          className={`adm-tr-colors absolute inset-y-0 left-0 rounded-full ${warning ? 'bg-(--admin-status-warning)' : 'bg-(--admin-status-success)'}`}
          style={{ width: `${width}%` }}
        />
      </span>
    </span>
  );
};

/**
 * Trạng thái vận hành của bãi trong một ô: bãi không hoạt động / dừng khẩn cấp (đỏ) > gần đầy ≥ 90% (cam)
 * > bình thường / còn nhiều chỗ (xanh). Tạm dừng đặt ngay là dòng phụ.
 */
const LotStatusCell: React.FC<{ row: LotOccupancyRowDto }> = ({ row }) => {
  const main =
    row.lotStatus !== 'Active' ? (
      <StatusIndicator tone="danger" label={lotStatusLabel(row.lotStatus)} />
    ) : row.isEmergencyStopped ? (
      <StatusIndicator tone="danger" label="Emergency stop" />
    ) : row.occupancyPercent >= OCCUPANCY_WARNING_PERCENT ? (
      <StatusIndicator tone="warning" label="Near capacity" />
    ) : row.occupancyPercent >= AMPLE_BELOW_PERCENT ? (
      <StatusIndicator tone="success" label="Normal flow" />
    ) : (
      <StatusIndicator tone="success" label="Ample space" />
    );
  return (
    <span className="flex flex-col gap-0.5">
      {main}
      {row.lotStatus !== 'Active' && row.isEmergencyStopped && <span className="adm-type-body-small text-(--admin-status-danger)">Emergency stop</span>}
      {row.instantBookingPaused && <span className="adm-type-body-small text-(--admin-text-secondary)">Instant booking paused</span>}
    </span>
  );
};

const occupied = (active: number, capacity: number) => `${formatNumber(active)} / ${formatNumber(capacity)}`;

/** Cột theo thiết kế "Live facility occupancy": bãi (tên + quận, thành phố) · lấp đầy (số + thanh) · tích hợp · trạng thái. */
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
    key: 'occupancy',
    header: 'Occupancy',
    width: 260,
    render: (r) => (
      <OccupancyCell active={r.activeSessions} capacity={r.capacity} percent={r.occupancyPercent} label={r.name} fallback={r.capacitySource === 'parking'} />
    ),
  },
  { key: 'integration', header: 'Integration', width: 110, render: (r) => <CellText>{integrationTierLabel(r.integrationTier)}</CellText> },
  { key: 'status', header: 'Status', width: 170, render: (r) => <LotStatusCell row={r} /> },
];

const districtName = (r: DistrictOccupancyRowDto) => `${r.district ?? 'Unknown district'}, ${r.city}`;

const DISTRICT_COLUMNS: DataTableColumn<DistrictOccupancyRowDto>[] = [
  { key: 'district', header: 'District', render: (r) => <span className="adm-type-body-strong">{districtName(r)}</span> },
  { key: 'lots', header: 'Lots', width: 90, align: 'right', render: (r) => formatNumber(r.lotCount) },
  {
    key: 'occupancy',
    header: 'Occupancy',
    width: 300,
    render: (r) => <OccupancyCell active={r.activeSessions} capacity={r.capacity} percent={r.occupancyPercent} label={districtName(r)} />,
  },
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
    <Panel
    title="Live occupancy by lot"
    description="Vehicles parked now against gate capacity · lots waiting for KYB approval are not counted"
    action={{ label: 'All parking lots →', to: '/admin/operations/parking-lots' }}
    padded={false}
  >
      <WidgetBody widget={widget} label="Lot occupancy">
        {(value) => <OccupancyTables value={value} asOfUtc={widget?.asOfUtc ?? null} busy={busy} />}
      </WidgetBody>
    </Panel>
  );

// ===================== Gate device health =====================

/** Một dòng thiết bị theo loại (thiết kế "System health & sensors"): tên · "x/y online" · trạng thái bằng chấm + chữ. */
const DeviceHealthRow: React.FC<{ row: GateDeviceTypeRowDto }> = ({ row }) => (
  <li className="flex min-h-10 items-center justify-between gap-3 border-b border-(--admin-border-default) px-3 py-2 last:border-b-0">
    <span className="adm-type-body-default text-(--admin-text-primary)">{deviceTypeLabel(row.deviceType)}</span>
    <span className="flex flex-wrap items-center justify-end gap-x-3 gap-y-0.5">
      <span className="adm-type-body-small tabular-nums text-(--admin-text-secondary)">
        {formatNumber(row.online)}/{formatNumber(row.total)} online
      </span>
      {row.offline > 0 ? (
        <StatusIndicator tone="danger" label={`${formatNumber(row.offline)} offline`} />
      ) : row.maintenance > 0 ? (
        <StatusIndicator tone="warning" label={`${formatNumber(row.maintenance)} maintenance`} />
      ) : (
        <StatusIndicator tone="success" label="All online" />
      )}
    </span>
  </li>
);

export const GateDevicesPanel: React.FC<{ widget: DashboardWidget<GateDevicesDto> | null }> = ({ widget }) =>
  isHiddenWidget(widget) ? null : (
    <Panel title="Gate device health" action={{ label: 'All devices →', to: '/admin/operations/gate-activity/devices' }} padded={false}>
      <WidgetBody widget={widget} label="Gate devices">
        {(value) => (
          <>
            <div className="flex flex-wrap items-center justify-between gap-2 border-b border-(--admin-border-default) bg-(--admin-surface-page) px-3 py-1.5">
              <span className={`adm-type-body-small ${value.offline > 0 ? 'text-(--admin-status-danger)' : 'text-(--admin-text-secondary)'}`}>
                {value.offline > 0 ? `${formatNumber(value.offline)} offline` : 'No devices offline'} · total {formatNumber(value.total)}
              </span>
              <AsOfNote asOfUtc={widget?.asOfUtc ?? null} />
            </div>
            <div data-testid="gate-devices">
              {value.byType.length === 0 ? (
                <StateBlock type="empty" bare className="min-h-0 py-6" title="No gate devices yet" message="Devices that owners register at their gates appear here." />
              ) : (
                <ul className="m-0 list-none p-0">
                  {value.byType.map((r) => (
                    <DeviceHealthRow key={r.deviceType} row={r} />
                  ))}
                </ul>
              )}
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
