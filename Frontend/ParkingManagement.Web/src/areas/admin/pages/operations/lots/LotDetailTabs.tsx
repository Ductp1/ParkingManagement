import React from 'react';
import { adminLotsApi } from '@/api/adminLotsApi';
import { adminOperationsApi } from '@/api/adminOperationsApi';
import type { ParkingLotHierarchyDto } from '@/types/adminOperations';
import type { AdminApiError } from '@/api/adminClientApi';
import type { AdminQueryStatus } from '@/types/adminCommon';
import type { RateCardDto } from '@/types/adminLots';
import { Panel } from '../../../components/Panel';
import { DetailList, DetailRow } from '../../../components/DetailRow';
import { StateBlock } from '../../../components/StateBlock';
import { StatusIndicator } from '../../../components/StatusIndicator';
import { CellMono, CellMuted, CellStatus, CellText, CellTwoLine, DataTable } from '../../../components/DataTable';
import { humanizeEnum } from '../../../utils/enumLabels';
import { useAdminQuery } from '../../../hooks/useAdminQuery';
import { formatDate, formatDateTimeIct, formatNumber, formatVnd } from '../../../utils/format';
import { CLOSURE_LABEL, band, hoursText, mondayFirst, multiplier, sortClosures } from './lotDetailFormat';
import { DetailColumns, QueryState, QueryIssueState } from '../../../components/PageLayout';

/**
 * Operations / Parking Lots / Detail – 4 sub-tab chỉ đọc (P0-4, US-081): Capacity, Hours & Closures, Amenities & Photos, Pricing.
 * Dữ liệu do chủ bãi cấu hình; admin chỉ xem. Tách file riêng để ParkingLotDetailPage (đang được US-069 sửa) thay đổi tối thiểu.
 */

interface TabStateProps {
  status: AdminQueryStatus;
  error: AdminApiError | null;
  onRetry: () => void;
  emptyTitle: string;
  emptyMessage: string;
  errorTitle: string;
}

/** Trạng thái chung của một tab: loading / empty / không có quyền / lỗi (hiện nguyên văn ProblemDetails của server). */
const TabState: React.FC<TabStateProps> = ({ status, error, onRetry, emptyTitle, emptyMessage, errorTitle }) => {
  switch (status) {
    case 'loading':
      return <StateBlock type="loading" />;
    case 'empty':
      return <StateBlock type="empty" title={emptyTitle} message={emptyMessage} />;
    case 'forbidden':
      return <StateBlock type="noPermission" message="Only administrators can view this lot's settings." />;
    case 'notFound':
    case 'offline':
    case 'sessionExpired':
      return <QueryIssueState status={status} error={error} onRetry={onRetry} />;
    case 'error':
    case 'unavailable':
      return (
        <StateBlock
          type="error"
          title={errorTitle}
          message={error?.problem ? error.message : 'This information could not be loaded. Try again.'}
          onRetry={onRetry}
        />
      );
    default:
      return null;
  }
};

// ===== Layout =====

/** Cây Zone / Floor / Slot của bãi (ParkingService hierarchy) – chuyển nguyên văn từ ParkingLotDetailPage. */
export const LayoutTab: React.FC<{ lotId: number }> = ({ lotId }) => {
  const query = useAdminQuery(() => adminOperationsApi.getParkingLotHierarchy(lotId), [lotId], (d) => d.zones.length === 0);
  const tree: ParkingLotHierarchyDto | null = query.data;
  if (!tree || query.status !== 'ready') {
    return (
      <QueryState
        status={query.status}
        error={query.error}
        onRetry={query.refetch}
        bare={false}
        copy={{ emptyTitle: 'No layout yet', emptyMessage: 'The owner has not created zones, floors and slots.', errorTitle: 'Could not load the layout', service: 'ParkingService' }}
      />
    );
  }
  const floors = tree.zones.flatMap((z) => z.floors.map((f) => ({ zone: z, floor: f })));
  return (
    <Panel title="Zones & floors" description="Zones, floors and slots" padded={false}>
      <DataTable
        bare
        rows={floors}
        rowKey={(r) => r.floor.id}
        columns={[
          { key: 'zone', header: 'Zone', render: (r) => <CellText>{`${r.zone.code} · ${r.zone.name}`}</CellText> },
          { key: 'floor', header: 'Floor', render: (r) => <CellText>{`${r.floor.name} (level ${r.floor.level})`}</CellText> },
          { key: 'grid', header: 'Grid', render: (r) => <CellMono>{`${r.floor.gridColumns}×${r.floor.gridRows}`}</CellMono> },
          { key: 'slots', header: 'Slots', render: (r) => <CellText>{r.floor.slots.length}</CellText> },
          {
            key: 'available',
            header: 'Available',
            render: (r) => <CellText>{r.floor.slots.filter((s) => s.isActive && s.state === 'Available').length}</CellText>,
          },
          { key: 'max', header: 'Max height', render: (r) => <CellText>{r.floor.maxHeightCm ? `${r.floor.maxHeightCm} cm` : '—'}</CellText> },
          { key: 'state', header: 'State', render: (r) => <CellStatus value={r.zone.isClosed || r.floor.isClosed ? 'Closed' : 'Active'} label={r.zone.isClosed || r.floor.isClosed ? 'Closed' : 'Open'} /> },
        ]}
      />
    </Panel>
  );
};

// ===== Capacity =====

/** Sức chứa vận hành tại cổng (GateService, chỉ đọc) + số xe đang đỗ. */
export const CapacityTab: React.FC<{ lotId: number }> = ({ lotId }) => {
  const query = useAdminQuery(() => adminLotsApi.capacity(lotId), [lotId], (d) => !d.configured);
  const c = query.data;
  if (query.status !== 'ready' || !c) {
    return (
      <TabState
        status={query.status}
        error={query.error}
        onRetry={query.refetch}
        emptyTitle="Capacity not set up"
        emptyMessage="The owner has not set how many vehicles this lot takes at the gate."
        errorTitle="Could not load capacity"
      />
    );
  }
  return (
    <DetailColumns
      main={
        <Panel title="Gate capacity">
          <DetailList>
            <DetailRow label="Total capacity">{formatNumber(c.capacity)}</DetailRow>
            <DetailRow label="Kept for walk-in">{formatNumber(c.transientBuffer)}</DetailRow>
            <DetailRow label="Kept for subscribers">{formatNumber(c.subscriberQuota)}</DetailRow>
            <DetailRow label="Active subscribers">{formatNumber(c.activeSubscribers)}</DetailRow>
            {c.updatedAtUtc && <DetailRow label="Last changed">{formatDateTimeIct(c.updatedAtUtc)}</DetailRow>}
          </DetailList>
        </Panel>
      }
      side={
        <Panel title="Right now">
          <DetailList>
            <DetailRow label="Parked">{`${formatNumber(c.totalParked)} of ${formatNumber(c.capacity)}`}</DetailRow>
            <DetailRow label="Walk-in parked">{formatNumber(c.transientParked)}</DetailRow>
            <DetailRow label="Subscribers parked">{formatNumber(c.subscriberParked)}</DetailRow>
            <DetailRow label="Free for walk-in">{formatNumber(c.availableForTransient)}</DetailRow>
            <DetailRow label="Free for subscribers">{formatNumber(c.availableForSubscriber)}</DetailRow>
          </DetailList>
        </Panel>
      }
    />
  );
};

// ===== Hours & Closures =====

const WeekHours: React.FC<{ lotId: number }> = ({ lotId }) => {
  const query = useAdminQuery(() => adminLotsApi.operatingHours(lotId), [lotId], (d) => d.schedule.length === 0);
  const week = query.data;
  if (query.status !== 'ready' || !week) {
    return (
      <TabState
        status={query.status}
        error={query.error}
        onRetry={query.refetch}
        emptyTitle="No opening hours"
        emptyMessage="The owner has not set opening hours for this lot."
        errorTitle="Could not load opening hours"
      />
    );
  }
  const range = week.weekStartDate && week.weekEndDate ? `Week of ${formatDate(week.weekStartDate)} – ${formatDate(week.weekEndDate)}` : undefined;
  return (
    <Panel title="Opening hours this week" description={range} padded={false}>
      <DataTable
        bare
        animateRows={false}
        rows={[...week.schedule].sort((a, b) => mondayFirst(a.dayOfWeekNumber) - mondayFirst(b.dayOfWeekNumber))}
        rowKey={(d) => d.dayOfWeek}
        columns={[
          { key: 'day', header: 'Day', width: 140, render: (d) => <CellText>{humanizeEnum(d.dayOfWeek)}</CellText> },
          { key: 'hours', header: 'Hours', render: (d) => <CellText>{hoursText(d)}</CellText> },
          {
            key: 'note',
            header: 'Note',
            render: (d) => (d.isOverride ? <CellTwoLine primary="Changed for this week" secondary={d.reason ?? undefined} /> : null),
          },
        ]}
      />
    </Panel>
  );
};

const Closures: React.FC<{ lotId: number }> = ({ lotId }) => {
  const query = useAdminQuery(() => adminLotsApi.closures(lotId), [lotId], (d) => d.length === 0);
  if (query.status !== 'ready' || !query.data) {
    return (
      <Panel title="Closures">
        <TabState
          status={query.status}
          error={query.error}
          onRetry={query.refetch}
          emptyTitle="No closures"
          emptyMessage="The owner has not closed this lot or any part of it."
          errorTitle="Could not load closures"
        />
      </Panel>
    );
  }
  return (
    <Panel title="Closures" padded={false}>
      <DataTable
        bare
        rows={sortClosures(query.data)}
        rowKey={(c) => c.id}
        columns={[
          { key: 'target', header: 'Closed area', render: (c) => <CellTwoLine primary={c.targetName} secondary={humanizeEnum(c.targetType)} /> },
          { key: 'from', header: 'From', width: 150, render: (c) => <CellMuted>{formatDateTimeIct(c.startsAtUtc)}</CellMuted> },
          {
            key: 'to',
            header: 'To',
            width: 150,
            render: (c) => <CellMuted>{c.endsAtUtc ? formatDateTimeIct(c.endsAtUtc) : 'Until reopened'}</CellMuted>,
          },
          { key: 'reason', header: 'Reason', render: (c) => <CellTwoLine primary={c.reason} secondary={c.isEmergency ? 'Emergency' : undefined} /> },
          {
            key: 'slots',
            header: 'Slots',
            width: 70,
            align: 'right',
            render: (c) => <CellText>{formatNumber(c.affectedSlotsCount)}</CellText>,
          },
          {
            key: 'status',
            header: 'Status',
            width: 110,
            render: (c) => {
              const s = CLOSURE_LABEL[c.status];
              return <StatusIndicator value={c.status} label={s?.label} tone={s?.tone ?? 'neutral'} />;
            },
          },
        ]}
      />
    </Panel>
  );
};

/** Giờ mở cửa tuần hiện tại + lịch đóng cửa (ParkingService). */
export const HoursTab: React.FC<{ lotId: number }> = ({ lotId }) => (
  <div className="flex flex-col gap-4">
    <WeekHours lotId={lotId} />
    <Closures lotId={lotId} />
  </div>
);

// ===== Amenities & Photos =====

/** Ảnh bãi; URL không tải được (vd. seed trỏ tới /uploads/… không có máy chủ – audit §7.7) → khung ghi rõ thay vì ô trắng. */
const PhotoTile: React.FC<{ url: string; alt: string }> = ({ url, alt }) => {
  const [failed, setFailed] = React.useState(false);
  const frame = 'aspect-[4/3] w-full border border-(--admin-border-default)';
  return failed ? (
    <div role="img" aria-label={alt} className={`${frame} flex items-center justify-center bg-(--admin-surface-page)`}>
      <span className="adm-type-body-small text-(--admin-text-secondary)">Photo unavailable</span>
    </div>
  ) : (
    <img src={url} alt={alt} loading="lazy" onError={() => setFailed(true)} className={`${frame} object-cover`} />
  );
};

/** Tiện ích và ảnh do chủ bãi đăng (ParkingService attributes). */
export const AmenitiesTab: React.FC<{ lotId: number }> = ({ lotId }) => {
  const query = useAdminQuery(() => adminLotsApi.attributes(lotId), [lotId]);
  const a = query.data;
  if (query.status !== 'ready' || !a) {
    return (
      <TabState
        status={query.status}
        error={query.error}
        onRetry={query.refetch}
        emptyTitle=""
        emptyMessage=""
        errorTitle="Could not load amenities and photos"
      />
    );
  }
  const photos = [...a.photos].sort((x, y) => Number(y.isCover) - Number(x.isCover) || x.sortOrder - y.sortOrder);
  return (
    <div className="flex flex-col gap-4">
      <Panel title="Amenities">
        {a.amenities.length === 0 ? (
          <StateBlock type="empty" bare title="No amenities listed" message="The owner has not listed any amenities for this lot." />
        ) : (
          <ul className="m-0 flex list-none flex-col gap-2 p-0">
            {a.amenities.map((m) => (
              <li key={m.id} className="flex flex-col">
                <span className="adm-type-body-strong text-(--admin-text-primary)">{m.name}</span>
                {m.note && <span className="adm-type-body-small text-(--admin-text-secondary)">{m.note}</span>}
              </li>
            ))}
          </ul>
        )}
      </Panel>
      <Panel title="Photos">
        {photos.length === 0 ? (
          <StateBlock type="empty" bare title="No photos" message="The owner has not uploaded photos of this lot." />
        ) : (
          <ul className="m-0 grid list-none grid-cols-[repeat(auto-fill,minmax(180px,1fr))] gap-3 p-0">
            {photos.map((p) => (
              <li key={p.id} className="flex flex-col gap-1">
                <PhotoTile url={p.url} alt={p.caption ?? `${a.name} photo`} />
                <span className="adm-type-body-small text-(--admin-text-secondary)">
                  {[p.isCover ? 'Cover photo' : null, p.caption].filter(Boolean).join(' · ')}
                </span>
              </li>
            ))}
          </ul>
        )}
      </Panel>
    </div>
  );
};

// ===== Pricing =====

const RateCardPanel: React.FC<{ card: RateCardDto }> = ({ card }) => {
  const period = `${card.isActive ? 'Active' : 'Inactive'} · from ${formatDate(card.effectiveFromUtc)}${
    card.effectiveToUtc ? ` to ${formatDate(card.effectiveToUtc)}` : ''
  }`;
  const rules = [...card.rules].sort((a, b) => a.vehicleType.localeCompare(b.vehicleType) || a.sortOrder - b.sortOrder || a.fromMinute - b.fromMinute);
  return (
    <Panel title={card.name} description={period} padded={false}>
      <div className="px-3 py-3">
        <DetailList>
          <DetailRow label="Weekend">{multiplier(card.weekendMultiplier)}</DetailRow>
          <DetailRow label="Holiday">{multiplier(card.holidayMultiplier)}</DetailRow>
          <DetailRow label="Oversized vehicle">{multiplier(card.oversizedMultiplier)}</DetailRow>
          <DetailRow label="Overstay">{multiplier(card.overstayMultiplier)}</DetailRow>
          <DetailRow label="Overnight surcharge">{formatVnd(card.overnightSurcharge)}</DetailRow>
          <DetailRow label="Daily cap">{card.maxDailyAmount === null ? 'No cap' : formatVnd(card.maxDailyAmount)}</DetailRow>
        </DetailList>
      </div>
      {rules.length > 0 && (
        <DataTable
          bare
          animateRows={false}
          rows={rules}
          rowKey={(r) => r.id}
          columns={[
            { key: 'vehicle', header: 'Vehicle', width: 140, render: (r) => <CellText>{humanizeEnum(r.vehicleType)}</CellText> },
            { key: 'band', header: 'Time band', render: (r) => <CellText>{band(r.fromMinute, r.toMinute)}</CellText> },
            { key: 'block', header: 'Charged per', width: 120, render: (r) => <CellText>{`${formatNumber(r.blockMinutes)} min`}</CellText> },
            { key: 'price', header: 'Price', width: 120, align: 'right', render: (r) => <CellText>{formatVnd(r.pricePerBlock)}</CellText> },
          ]}
        />
      )}
    </Panel>
  );
};

/** Bảng giá của bãi (PaymentService): bảng đang áp dụng trước. */
export const PricingTab: React.FC<{ lotId: number }> = ({ lotId }) => {
  const query = useAdminQuery(() => adminLotsApi.rateCards(lotId), [lotId], (d) => d.length === 0);
  if (query.status !== 'ready' || !query.data) {
    return (
      <TabState
        status={query.status}
        error={query.error}
        onRetry={query.refetch}
        emptyTitle="No price list yet"
        emptyMessage="The owner has not published prices for this lot."
        errorTitle="Could not load prices"
      />
    );
  }
  const cards = [...query.data].sort((a, b) => Number(b.isActive) - Number(a.isActive) || b.effectiveFromUtc.localeCompare(a.effectiveFromUtc));
  return (
    <div className="flex flex-col gap-4">
      {cards.map((card) => (
        <RateCardPanel key={card.id} card={card} />
      ))}
    </div>
  );
};
