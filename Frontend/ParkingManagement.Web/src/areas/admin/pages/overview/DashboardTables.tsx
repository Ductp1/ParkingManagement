import React, { useMemo, useState } from 'react';
import { Link } from 'react-router-dom';
import { DataTable, type DataTableColumn } from '../../components/DataTable';
import { Panel } from '../../components/Panel';
import { StateBlock } from '../../components/StateBlock';
import { formatNumber, formatPercent, formatVnd } from '../../utils/format';
import { deviceTypeLabel, integrationTierLabel, lotStatusLabel, OCCUPANCY_WARNING_PERCENT, paymentMethodLabel } from './dashboardFormat';
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

/** Thân panel theo trạng thái widget: đang tải lần đầu / chưa có nguồn / lỗi nguồn / có dữ liệu. */
const WidgetBody = <T,>({ widget, children }: { widget: DashboardWidget<T> | null; children: (value: T) => React.ReactNode }) => {
  if (widget === null) return LOADING;
  if (widget.status !== 'available' || widget.value === null) return <WidgetTablePlaceholder widget={widget} />;
  return <>{children(widget.value)}</>;
};

// ===================== Lấp đầy bãi =====================

/** Thanh % lấp đầy; từ 90% chuyển màu cảnh báo kèm chữ "Gần đầy" (không chỉ dựa vào màu). */
const OccupancyBar: React.FC<{ percent: number; label: string }> = ({ percent, label }) => {
  const warning = percent >= OCCUPANCY_WARNING_PERCENT;
  return (
    <span className="flex min-w-[140px] items-center gap-2">
      <span
        role="meter"
        aria-label={`Lấp đầy ${label}`}
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
      {warning && <span className="adm-type-body-small text-(--admin-status-danger)">Gần đầy</span>}
    </span>
  );
};

const LotFlags: React.FC<{ row: LotOccupancyRowDto }> = ({ row }) => (
  <span className="flex flex-wrap gap-1">
    {row.isEmergencyStopped && (
      <span className="adm-type-body-small rounded-[2px] border border-(--admin-status-danger) px-1.5 text-(--admin-status-danger)">Dừng khẩn cấp</span>
    )}
    {row.instantBookingPaused && (
      <span className="adm-type-body-small rounded-[2px] border border-(--admin-border-default) px-1.5 text-(--admin-text-secondary)">
        Tạm dừng đặt ngay
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
    header: 'Bãi',
    render: (r) => (
      <span className="flex min-w-0 flex-col">
        <Link
          to={`/admin/operations/parking-lots/${r.parkingLotId}`}
          className="adm-type-body-strong text-(--admin-brand-navy-900) hover:underline focus-visible:outline-2 focus-visible:outline-(--admin-brand-navy-900)"
        >
          {r.name}
        </Link>
        <span className="adm-type-body-small text-(--admin-text-secondary)">{[r.district ?? 'Chưa rõ quận', r.city].join(', ')}</span>
      </span>
    ),
  },
  {
    key: 'occupied',
    header: 'Đang đỗ / Sức chứa',
    width: 150,
    align: 'right',
    render: (r) => (
      <span className="tabular-nums" title={r.capacitySource === 'parking' ? 'Sức chứa lấy từ tổng số chỗ của bãi (chưa cấu hình sức chứa ở cổng)' : undefined}>
        {occupied(r.activeSessions, r.capacity)}
        {r.capacitySource === 'parking' && <span className="text-(--admin-text-secondary)">*</span>}
      </span>
    ),
  },
  { key: 'percent', header: 'Lấp đầy', width: 230, render: (r) => <OccupancyBar percent={r.occupancyPercent} label={r.name} /> },
  { key: 'integration', header: 'Tích hợp', width: 100, render: (r) => integrationTierLabel(r.integrationTier) },
  {
    key: 'status',
    header: 'Trạng thái bãi',
    width: 130,
    render: (r) => (
      <span className={r.lotStatus === 'Active' ? '' : 'text-(--admin-status-danger)'}>{lotStatusLabel(r.lotStatus)}</span>
    ),
  },
  { key: 'flags', header: 'Cờ sức chứa', width: 170, render: (r) => <LotFlags row={r} /> },
];

const districtName = (r: DistrictOccupancyRowDto) => `${r.district ?? 'Chưa rõ quận'}, ${r.city}`;

const DISTRICT_COLUMNS: DataTableColumn<DistrictOccupancyRowDto>[] = [
  { key: 'district', header: 'Quận', render: (r) => districtName(r) },
  { key: 'lots', header: 'Số bãi', width: 90, align: 'right', render: (r) => formatNumber(r.lotCount) },
  { key: 'occupied', header: 'Đang đỗ / Sức chứa', width: 150, align: 'right', render: (r) => occupied(r.activeSessions, r.capacity) },
  { key: 'percent', header: 'Lấp đầy', width: 230, render: (r) => <OccupancyBar percent={r.occupancyPercent} label={districtName(r)} /> },
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
        <div role="group" aria-label="Xem lấp đầy theo" className="flex gap-1">
          <ToggleButton pressed={view === 'lot'} onClick={() => setView('lot')}>
            Theo bãi
          </ToggleButton>
          <ToggleButton pressed={view === 'district'} onClick={() => setView('district')}>
            Theo quận
          </ToggleButton>
        </div>
        <div className="flex flex-wrap items-center gap-3">
          <span className="adm-type-body-small text-(--admin-text-secondary)">
            Toàn sàn {occupied(value.totalActive, value.totalCapacity)} · {formatPercent(value.occupancyPercent)}
          </span>
          <AsOfNote asOfUtc={asOfUtc} />
          <button
            type="button"
            onClick={() => setDirection((d) => (d === 'desc' ? 'asc' : 'desc'))}
            aria-label={direction === 'desc' ? 'Đang sắp lấp đầy giảm dần, bấm để sắp tăng dần' : 'Đang sắp lấp đầy tăng dần, bấm để sắp giảm dần'}
            className="adm-type-link-small rounded-[2px] px-1 text-(--admin-brand-navy-900) hover:underline focus-visible:outline-2 focus-visible:outline-(--admin-brand-navy-900)"
          >
            Lấp đầy {direction === 'desc' ? '↓' : '↑'}
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
            placeholder={<StateBlock type="empty" bare title="Chưa có bãi đang vận hành" message="ParkingService chưa có bãi nào đã duyệt." />}
          />
        ) : (
          <DataTable
            bare
            columns={DISTRICT_COLUMNS}
            rows={districts}
            rowKey={(r) => `${r.city}|${r.district ?? ''}`}
            busy={busy}
            placeholder={<StateBlock type="empty" bare title="Chưa có bãi đang vận hành" message="ParkingService chưa có bãi nào đã duyệt." />}
          />
        )}
      </div>
      {view === 'lot' && hasFallbackCapacity && (
        <p className="adm-type-body-small m-0 px-3 py-2 text-(--admin-text-secondary)">
          * Bãi chưa cấu hình sức chứa ở cổng: dùng tổng số chỗ khai báo của bãi.
        </p>
      )}
    </>
  );
};

export const LotOccupancyPanel: React.FC<{ widget: DashboardWidget<LotOccupancyDto> | null; busy: boolean }> = ({ widget, busy }) => (
  <Panel title="Lấp đầy bãi" description="Xe đang đỗ (GateService) / sức chứa · bãi chờ duyệt KYB không tính" padded={false}>
    <WidgetBody widget={widget}>{(value) => <OccupancyTables value={value} asOfUtc={widget?.asOfUtc ?? null} busy={busy} />}</WidgetBody>
  </Panel>
);

// ===================== Thiết bị cổng =====================

const deviceCount = (n: number, highlight: boolean) => (
  <span className={`tabular-nums ${highlight && n > 0 ? 'font-semibold text-(--admin-status-danger)' : ''}`}>{formatNumber(n)}</span>
);

const DEVICE_COLUMNS: DataTableColumn<GateDeviceTypeRowDto>[] = [
  { key: 'type', header: 'Loại thiết bị', render: (r) => deviceTypeLabel(r.deviceType) },
  { key: 'online', header: 'Trực tuyến', width: 110, align: 'right', render: (r) => deviceCount(r.online, false) },
  { key: 'offline', header: 'Mất kết nối', width: 110, align: 'right', render: (r) => deviceCount(r.offline, true) },
  { key: 'maintenance', header: 'Bảo trì', width: 100, align: 'right', render: (r) => deviceCount(r.maintenance, false) },
  { key: 'total', header: 'Tổng', width: 80, align: 'right', render: (r) => formatNumber(r.total) },
];

export const GateDevicesPanel: React.FC<{ widget: DashboardWidget<GateDevicesDto> | null; busy: boolean }> = ({ widget, busy }) => (
  <Panel
    title="Thiết bị cổng"
    description="Thiết bị đang dùng theo loại × trạng thái"
    action={{ label: 'Danh sách thiết bị →', to: '/admin/operations/gate-activity/devices' }}
    padded={false}
  >
    <WidgetBody widget={widget}>
      {(value) => (
        <>
          <div className="flex flex-wrap items-center justify-between gap-2 border-b border-(--admin-border-default) px-3 py-2">
            <span className={`adm-type-body-small ${value.offline > 0 ? 'text-(--admin-status-danger)' : 'text-(--admin-text-secondary)'}`}>
              {value.offline > 0 ? `${formatNumber(value.offline)} thiết bị mất kết nối` : 'Không có thiết bị mất kết nối'} · tổng{' '}
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
              placeholder={<StateBlock type="empty" bare title="Chưa có thiết bị cổng" message="GateService chưa ghi nhận thiết bị nào đang dùng." />}
            />
          </div>
        </>
      )}
    </WidgetBody>
  </Panel>
);

// ===================== Thanh toán theo phương thức =====================

const PAYMENT_COLUMNS: DataTableColumn<PaymentMethodRowDto>[] = [
  { key: 'method', header: 'Phương thức', render: (r) => paymentMethodLabel(r.method) },
  { key: 'count', header: 'Số giao dịch', width: 130, align: 'right', render: (r) => formatNumber(r.count) },
  { key: 'gross', header: 'Doanh thu gộp', width: 170, align: 'right', render: (r) => <span className="tabular-nums">{formatVnd(r.gross)}</span> },
];

export const PaymentsByMethodPanel: React.FC<{ widget: DashboardWidget<PaymentsByMethodDto> | null; busy: boolean }> = ({ widget, busy }) => (
  <Panel title="Thanh toán hôm nay theo phương thức" description="Chỉ số tổng – không hiển thị giao dịch hay khách cụ thể" padded={false}>
    <WidgetBody widget={widget}>
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
              placeholder={<StateBlock type="empty" bare title="Chưa có thanh toán hôm nay" message="Chưa có giao dịch thành công trong ngày (ICT)." />}
            />
          </div>
        </>
      )}
    </WidgetBody>
  </Panel>
);
