import React from 'react';
import { useParams } from 'react-router-dom';
import { adminOperationsApi } from '@/api/adminOperationsApi';
import type { AdminParkingLotDto, ParkingLotHierarchyDto } from '@/types/adminOperations';
import { AdminTitleBar } from '../../../layout/AdminTitleBar';
import { AdminButton } from '../../../components/AdminButton';
import { DetailColumns, QueryState, SummaryRow } from '../../../components/PageLayout';
import { Panel } from '../../../components/Panel';
import { DetailList, DetailRow } from '../../../components/DetailRow';
import { StatusIndicator } from '../../../components/StatusIndicator';
import { StateBlock } from '../../../components/StateBlock';
import { SubTabs } from '../../../components/SubTabs';
import { CellMono, CellStatus, CellText, DataTable } from '../../../components/DataTable';
import { useAdminQuery } from '../../../hooks/useAdminQuery';
import { useAdminBreadcrumb } from '../../../hooks/useAdminBreadcrumb';
import { humanizeEnum } from '../../../utils/enumLabels';
import { formatNumber, orEmpty } from '../../../utils/format';

const TABS = [
  { key: '', label: 'Information' },
  { key: 'layout', label: 'Layout' },
  { key: 'capacity', label: 'Capacity' },
  { key: 'hours', label: 'Hours & Closures' },
  { key: 'amenities', label: 'Amenities & Photos' },
  { key: 'integration', label: 'Integration & Devices' },
  { key: 'pricing', label: 'Pricing' },
];

const MISSING: Record<string, string> = {
  capacity: 'GET /api/v1/parking-lots/{id}/capacity (LotCapacityConfigs)',
  hours: 'GET /api/v1/parking-lots/{id}/operating-hours, /closures',
  amenities: 'GET /api/v1/parking-lots/{id}/amenities (admin view)',
  integration: 'GET /api/v1/parking-lots/{id}/integration',
  pricing: 'GET /api/v1/rate-cards?parkingLotId=',
};

const NO_ENDPOINT = 'No admin endpoint for this action yet';

const InformationTab: React.FC<{ lot: AdminParkingLotDto }> = ({ lot }) => (
  <DetailColumns
    main={
      <>
        <Panel title="Lot information" description="ParkingLots · edited by the owner; admin is read-only">
          <DetailList>
            <DetailRow label="Name">{lot.name}</DetailRow>
            <DetailRow label="Address">{lot.address}</DetailRow>
            <DetailRow label="Coordinates">
              {lot.latitude.toFixed(4)}, {lot.longitude.toFixed(4)}
            </DetailRow>
            <DetailRow label="Hotline">{orEmpty(lot.hotlinePhone)}</DetailRow>
            <DetailRow label="Max vehicle height">{lot.maxHeightCm} cm</DetailRow>
            <DetailRow label="Default open / close">{lot.openingHours}</DetailRow>
            <DetailRow label="Description">{orEmpty(lot.description)}</DetailRow>
          </DetailList>
        </Panel>
        <Panel title="Capacity snapshot">
          <DetailList>
            <DetailRow label="Total slots">{formatNumber(lot.totalSlots)}</DetailRow>
            <DetailRow label="Available slots">{formatNumber(lot.availableSlots)}</DetailRow>
            <DetailRow label="Rating">
              {lot.ratingCount > 0 ? `${lot.ratingAverage.toLocaleString('vi-VN', { maximumFractionDigits: 1 })} ★ (${formatNumber(lot.ratingCount)} reviews)` : '—'}
            </DetailRow>
          </DetailList>
        </Panel>
      </>
    }
    side={
      <>
        <Panel title="Owner & commission">
          <DetailList>
            <DetailRow label="Contract" future>
              —
            </DetailRow>
          </DetailList>
          <p className="adm-type-body-regular-12 m-0 mt-6 text-(--admin-text-secondary)">
            ParkingLotDto does not include the owner; open the owner from Users › Lot owner.
          </p>
        </Panel>
        <Panel title="Lot-level sanctions">
          <StateBlock type="unavailable" bare className="min-h-0 py-4" message="AdminService has no per-lot sanctions endpoint yet." />
        </Panel>
      </>
    }
  />
);

const LayoutTab: React.FC<{ lotId: number }> = ({ lotId }) => {
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
    <Panel title="Zones & floors" description="Zones · Floors · Slots (ParkingService hierarchy)" padded={false}>
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

/** Operations / Parking Lots / Detail – Information, Layout (API thật); các tab còn lại chờ endpoint. */
export const ParkingLotDetailPage: React.FC = () => {
  const { id, tab = '' } = useParams();
  const lotId = Number(id);
  const valid = Number.isInteger(lotId) && lotId > 0;
  const query = useAdminQuery(valid ? () => adminOperationsApi.getParkingLot(lotId) : null, [lotId]);
  const lot = query.data;
  useAdminBreadcrumb(lot ? lot.name : valid ? `Lot #${lotId}` : null);

  if (!lot) {
    return (
      <>
        <AdminTitleBar title="Parking Lots" subtitle="—" />
        <QueryState
          status={valid ? query.status : 'empty'}
          error={query.error}
          onRetry={query.refetch}
          bare={false}
          copy={{ emptyTitle: 'Parking lot not found', emptyMessage: 'Check the link.', errorTitle: 'Could not load the parking lot', service: 'ParkingService' }}
        />
      </>
    );
  }

  const base = `/admin/operations/parking-lots/${lot.id}`;
  return (
    <>
      <AdminTitleBar
        title={lot.name}
        subtitle={`Lot #${lot.id} · ${lot.address}`}
        actions={
          <>
            <AdminButton variant="onNavy" disabled title={NO_ENDPOINT}>
              Emergency stop
            </AdminButton>
            <AdminButton disabled title={NO_ENDPOINT}>
              Create closure
            </AdminButton>
          </>
        }
      />
      <SummaryRow
        items={[
          { label: 'Status', value: <StatusIndicator value={lot.status} /> },
          { label: 'Open now', value: <span className="adm-type-body-strong">{lot.isOpenNow ? 'Yes' : 'No'}</span> },
        ]}
        actions={
          <AdminButton variant="destructive" disabled title={NO_ENDPOINT}>
            Suspend lot
          </AdminButton>
        }
      />
      <SubTabs
        tabs={[
          ...TABS.map((t) => ({ label: t.label, to: t.key ? `${base}/${t.key}` : base })),
          { label: '3D view · Phase 2', to: base, disabled: true },
        ]}
      />
      {tab === '' && <InformationTab lot={lot} />}
      {tab === 'layout' && <LayoutTab lotId={lot.id} />}
      {MISSING[tab] && (
        <StateBlock
          type="unavailable"
          message={`ParkingService has no endpoint for ${humanizeEnum(TABS.find((t) => t.key === tab)?.label ?? tab)} yet (${MISSING[tab]}).`}
        />
      )}
    </>
  );
};
