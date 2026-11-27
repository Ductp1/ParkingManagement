import React, { useMemo, useState } from 'react';
import { adminOperationsApi } from '@/api/adminOperationsApi';
import { GateDeviceStatus, GateDeviceType, ParkingSessionStatus } from '@/types/adminEnums';
import type { AdminGateDeviceDto, AdminParkingSessionDto } from '@/types/adminOperations';
import { ListScreen, QueryState } from '../../../components/PageLayout';
import { FilterColumn, FilterPanel } from '../../../components/FilterColumn';
import { SearchBox, ToolbarRow, ToolbarSelect } from '../../../components/Toolbar';
import { SubTabs } from '../../../components/SubTabs';
import { CellAmount, CellMono, CellMuted, CellStatus, CellText, DataTable, type DataTableColumn } from '../../../components/DataTable';
import { Pagination } from '../../../components/Pagination';
import { useAdminQuery } from '../../../hooks/useAdminQuery';
import { DEFAULT_PAGE_SIZE, useListParams } from '../../../hooks/useListParams';
import { humanizeEnum } from '../../../utils/enumLabels';
import { formatShortDateTime, formatVnd } from '../../../utils/format';
import { GATE_SUB_TABS } from './gateTabs';


const SESSION_STATUSES = Object.values(ParkingSessionStatus);
const isSessionStatus = (v: string): v is ParkingSessionStatus => (SESSION_STATUSES as string[]).includes(v);

/** Ô nhập Lot ID (API parking-sessions bắt buộc parkingLotId). */
const LotIdBox: React.FC<{ value: string; onChange: (v: string) => void }> = ({ value, onChange }) => {
  const [text, setText] = useState(value);
  return (
    <form
      onSubmit={(e) => {
        e.preventDefault();
        onChange(text.trim());
      }}
      className="flex h-8 w-[150px] items-center gap-2 rounded-[2px] border border-(--admin-border-default) bg-(--admin-surface-card) px-2.5 focus-within:border-(--admin-brand-navy-900)"
    >
      <span className="adm-type-body-regular-12 text-(--admin-text-primary)">Lot:</span>
      <input
        inputMode="numeric"
        aria-label="Lot ID"
        placeholder="ID"
        value={text}
        onChange={(e) => setText(e.target.value.replace(/\D/g, ''))}
        onBlur={() => onChange(text.trim())}
        className="adm-type-body-default min-w-0 flex-1 bg-transparent outline-none placeholder:text-(--admin-text-placeholder)"
      />
    </form>
  );
};

const sessionColumns: DataTableColumn<AdminParkingSessionDto>[] = [
  { key: 'code', header: 'Session', render: (r) => <CellMono>{r.code}</CellMono> },
  { key: 'plate', header: 'Plate', render: (r) => <CellText>{r.plateDisplay || r.plateNumber}</CellText> },
  { key: 'lot', header: 'Lot', render: (r) => <CellText>#{r.parkingLotId}</CellText> },
  { key: 'booking', header: 'Booking', render: (r) => <CellText>{r.bookingCode ?? (r.isWalkIn ? 'Walk-in' : '—')}</CellText> },
  { key: 'entry', header: 'Entry', render: (r) => <CellMuted>{formatShortDateTime(r.entryAtUtc)}</CellMuted> },
  { key: 'exit', header: 'Exit', render: (r) => <CellMuted>{formatShortDateTime(r.exitAtUtc)}</CellMuted> },
  { key: 'method', header: 'In / out method', render: (r) => <CellText>{humanizeEnum(r.checkInMethod)}</CellText> },
  { key: 'fee', header: 'Fee', render: (r) => <CellAmount>{formatVnd(r.fee)}</CellAmount> },
  { key: 'status', header: 'Status', render: (r) => <CellStatus value={r.status} /> },
];

/** Operations / Gate Activity / Sessions – GET /api/v1/parking-sessions?parkingLotId=&status= (theo từng bãi). */
export const GateSessionsPage: React.FC = () => {
  const params = useListParams();
  const lot = params.get('lot');
  const lotId = Number(lot);
  const statusParam = params.get('status');
  const status = isSessionStatus(statusParam) ? statusParam : undefined;
  const walkIn = params.get('walkIn');
  const search = params.get('q').toLowerCase();
  const page = params.page;

  const query = useAdminQuery(lotId > 0 ? () => adminOperationsApi.getLotSessions(lotId, status) : null, [lotId, status]);

  const filtered = useMemo(
    () =>
      (query.data ?? []).filter(
        (s) =>
          (walkIn === '' || (walkIn === 'yes') === s.isWalkIn) &&
          (search === '' || [s.code, s.plateNumber, s.plateDisplay, s.bookingCode ?? ''].some((x) => x.toLowerCase().includes(search)))
      ),
    [query.data, walkIn, search]
  );
  const rows = filtered.slice((page - 1) * DEFAULT_PAGE_SIZE, page * DEFAULT_PAGE_SIZE);
  const status2 = query.status === 'ready' && filtered.length === 0 ? 'empty' : query.status;

  return (
    <ListScreen
      title="Gate Activity"
      subtitle="Read-only · ParkingSessions, GateEvents, GateDevices, Shifts"
      top={<SubTabs tabs={GATE_SUB_TABS} />}
      filters={
        <FilterColumn>
          <FilterPanel
            title="Session status"
            value={status ?? ''}
            onChange={(v) => params.set({ status: v || null })}
            options={[{ value: '', label: 'All' }, ...SESSION_STATUSES.map((s) => ({ value: s, label: humanizeEnum(s) }))]}
          />
        </FilterColumn>
      }
      toolbar={
        <ToolbarRow>
          <SearchBox placeholder="Search session, plate, booking…" value={params.get('q')} onSearch={(q) => params.set({ q: q || null })} />
          <LotIdBox value={lot} onChange={(v) => params.set({ lot: v || null })} />
          <ToolbarSelect
            label="Walk-in"
            value={walkIn}
            onChange={(v) => params.set({ walkIn: v || null })}
            options={[
              { value: '', label: 'Any' },
              { value: 'yes', label: 'Yes' },
              { value: 'no', label: 'No' },
            ]}
          />
        </ToolbarRow>
      }
    >
      <DataTable
        columns={sessionColumns}
        rows={query.status === 'ready' ? rows : []}
        rowKey={(r) => r.id}
        placeholder={
          lotId > 0 ? (
            <QueryState
              status={status2}
              error={query.error}
              onRetry={query.refetch}
              copy={{ emptyTitle: 'No sessions', emptyMessage: 'Vehicles appear here after check-in at a gate.', errorTitle: 'Could not load sessions', service: 'GateService' }}
            />
          ) : (
            <QueryState
              status="empty"
              error={null}
              onRetry={query.refetch}
              copy={{ emptyTitle: 'Choose a lot', emptyMessage: 'GateService lists sessions per lot — enter a Lot ID above.', errorTitle: '', service: 'GateService' }}
            />
          )
        }
        footer={
          query.status === 'ready' &&
          filtered.length > 0 && <Pagination page={page} pageSize={DEFAULT_PAGE_SIZE} totalCount={filtered.length} onPageChange={params.setPage} />
        }
      />
    </ListScreen>
  );
};

const DEVICE_TYPES = Object.values(GateDeviceType);
const DEVICE_STATUSES = Object.values(GateDeviceStatus);

const deviceColumns: DataTableColumn<AdminGateDeviceDto>[] = [
  { key: 'code', header: 'Code', render: (r) => <CellMono>{r.code}</CellMono> },
  { key: 'name', header: 'Name', render: (r) => <CellText>{r.name}</CellText> },
  { key: 'lot', header: 'Lot', render: (r) => <CellText>#{r.parkingLotId}</CellText> },
  { key: 'type', header: 'Type', render: (r) => <CellText>{humanizeEnum(r.deviceType)}</CellText> },
  { key: 'position', header: 'Position', render: (r) => <CellText>{r.position ?? '—'}</CellText> },
  { key: 'status', header: 'Status', render: (r) => <CellStatus value={r.status} /> },
  { key: 'seen', header: 'Last seen', render: (r) => <CellMuted>{formatShortDateTime(r.lastSeenAtUtc)}</CellMuted> },
  { key: 'fw', header: 'Firmware', render: (r) => <CellText>{r.firmwareVersion ?? '—'}</CellText> },
];

/** Operations / Gate Activity / Devices – GET /api/v1/gate-devices (Gateway chưa có route → hiển thị trạng thái thật). */
export const GateDevicesPage: React.FC = () => {
  const params = useListParams();
  const type = params.get('type');
  const status = params.get('status');
  const page = params.page;
  const query = useAdminQuery(() => adminOperationsApi.getGateDevices(), []);

  const filtered = useMemo(
    () => (query.data ?? []).filter((d) => (type === '' || d.deviceType === type) && (status === '' || d.status === status)),
    [query.data, type, status]
  );
  const rows = filtered.slice((page - 1) * DEFAULT_PAGE_SIZE, page * DEFAULT_PAGE_SIZE);
  const viewStatus = query.status === 'ready' && filtered.length === 0 ? 'empty' : query.status;

  return (
    <ListScreen
      title="Gate Activity"
      subtitle="GateDevices"
      top={<SubTabs tabs={GATE_SUB_TABS} />}
      filters={
        <FilterColumn>
          <FilterPanel
            title="Device type"
            value={type}
            onChange={(v) => params.set({ type: v || null })}
            options={[{ value: '', label: 'All' }, ...DEVICE_TYPES.map((t) => ({ value: t, label: humanizeEnum(t) }))]}
          />
          <FilterPanel
            title="Status"
            value={status}
            onChange={(v) => params.set({ status: v || null })}
            options={[{ value: '', label: 'Any' }, ...DEVICE_STATUSES.map((s) => ({ value: s, label: humanizeEnum(s) }))]}
          />
        </FilterColumn>
      }
    >
      <DataTable
        columns={deviceColumns}
        rows={query.status === 'ready' ? rows : []}
        rowKey={(r) => r.id}
        placeholder={
          <QueryState
            status={viewStatus}
            error={query.error}
            onRetry={query.refetch}
            copy={{ emptyTitle: 'No devices', emptyMessage: 'Gate devices registered by owners appear here.', errorTitle: 'Could not load devices', service: 'GateService (route /api/v1/gate-devices missing in Gateway)' }}
          />
        }
        footer={
          query.status === 'ready' &&
          filtered.length > 0 && <Pagination page={page} pageSize={DEFAULT_PAGE_SIZE} totalCount={filtered.length} onPageChange={params.setPage} />
        }
      />
    </ListScreen>
  );
};
