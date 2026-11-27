import React, { Suspense, lazy, useState } from 'react';
import { adminLayoutApi } from '@/api/adminLayoutApi';
import { adminOperationsApi } from '@/api/adminOperationsApi';
import type { FloorLayoutDto } from '@/types/ownerLayout';
import { AdminButton } from '../../../../components/AdminButton';
import { Banner } from '../../../../components/Banner';
import { QueryState } from '../../../../components/PageLayout';
import { Panel } from '../../../../components/Panel';
import { StateBlock } from '../../../../components/StateBlock';
import { useAdminQuery } from '../../../../hooks/useAdminQuery';
import { useAdminToast } from '../../../../hooks/useAdminToast';
import { formatDateTimeIct } from '../../../../utils/format';
import { AddFloorDialog, VersionsDialog } from './LayoutDialogs';
import { LayoutEditor } from './LayoutEditor';
import { LayoutGrid, LayoutLegend } from './LayoutGrid';
import { fromDto, isSlot, type FloorModel } from './layoutModel';

const Layout3DView = lazy(() => import('./Layout3DView'));

const toFloor = (dto: FloorLayoutDto): FloorModel => ({
  floorId: dto.floorId,
  floorName: dto.floorName,
  level: dto.level,
  model: fromDto(dto),
});

const summary = (dto: FloorLayoutDto) => {
  const slots = dto.cells.filter(isSlot);
  const count = (state: string) => slots.filter((s) => s.state === state).length;
  return `${slots.length} slots · ${count('Available')} available · ${count('Reserved')} reserved · ${count('Occupied')} occupied · ${count('Maintenance')} in maintenance`;
};

/**
 * Operations / Parking Lots / Detail – tab Layout: sơ đồ đang áp dụng của từng tầng (2D như tài xế thấy, hoặc 3D dựng từ 2D);
 * Admin thiết kế lại, thêm tầng, khôi phục phiên bản cũ. Dữ liệu và việc đồng bộ ô đỗ thuộc ParkingService (US-059 / US-060).
 */
export const LayoutTab: React.FC<{ lotId: number }> = ({ lotId }) => {
  const toast = useAdminToast();
  const tree = useAdminQuery(() => adminOperationsApi.getParkingLotHierarchy(lotId), [lotId]);
  const floorRefs = (tree.data?.zones ?? []).flatMap((zone) => zone.floors.map((floor) => ({ zone, floor })));
  const floorKey = floorRefs.map((r) => r.floor.id).join(',');
  const layouts = useAdminQuery(
    floorKey ? () => Promise.all(floorRefs.map((r) => adminLayoutApi.getLayout(r.floor.id))) : null,
    [floorKey]
  );
  const [floorId, setFloorId] = useState<number | null>(null);
  const [view, setView] = useState<'2d' | '3d'>('2d');
  const [editing, setEditing] = useState(false);
  const [dialog, setDialog] = useState<'floor' | 'versions' | null>(null);

  if (!tree.data || tree.status !== 'ready') {
    return (
      <QueryState
        status={tree.status}
        error={tree.error}
        onRetry={tree.refetch}
        bare={false}
        copy={{ emptyTitle: 'No layout yet', emptyMessage: '', errorTitle: 'Could not load the layout', service: 'ParkingService' }}
      />
    );
  }

  const addFloor = dialog === 'floor' && (
    <AddFloorDialog
      lotId={lotId}
      zones={tree.data.zones}
      onClose={() => setDialog(null)}
      onDone={(id) => {
        setDialog(null);
        setFloorId(id);
        toast.show({ type: 'success', title: 'Floor added', message: 'Use “Edit layout” to design it.' });
        tree.refetch();
      }}
    />
  );

  if (floorRefs.length === 0) {
    return (
      <div className="flex flex-col items-start gap-3">
        <StateBlock type="empty" title="No layout yet" message="This lot has no floors. Add one to design its layout." />
        <AdminButton icon="plus" onClick={() => setDialog('floor')}>
          Add floor
        </AdminButton>
        {addFloor}
      </div>
    );
  }

  // Sau khi thêm tầng, layouts cũ chưa gồm tầng mới → coi như đang tải.
  const ready = layouts.status === 'ready' && layouts.data?.length === floorRefs.length;
  if (!ready || !layouts.data) {
    return (
      <QueryState
        status={layouts.status === 'ready' ? 'loading' : layouts.status}
        error={layouts.error}
        onRetry={layouts.refetch}
        bare={false}
        copy={{ emptyTitle: 'No layout yet', emptyMessage: '', errorTitle: 'Could not load the layout', service: 'ParkingService' }}
      />
    );
  }

  const current = layouts.data.find((d) => d.floorId === floorId) ?? layouts.data[0];
  const floors = layouts.data.map(toFloor);
  const floor = floors.find((f) => f.floorId === current.floorId) ?? floors[0];
  const reload = () => {
    layouts.refetch();
    tree.refetch();
  };

  if (editing) {
    return (
      <LayoutEditor
        key={floor.floorId}
        floor={floor}
        floors={floors}
        onClose={() => setEditing(false)}
        onPublished={() => {
          setEditing(false);
          reload();
        }}
      />
    );
  }

  const version =
    current.versionNo > 0 && current.publishedAtUtc
      ? `Published v${current.versionNo} · ${formatDateTimeIct(current.publishedAtUtc)}`
      : 'Not published yet – drawn from the slot positions';

  return (
    <div className="flex flex-col gap-4">
      <div className="flex flex-wrap items-center gap-2">
        <div role="group" aria-label="Floor" className="mr-auto flex flex-wrap gap-2">
          {floorRefs.map(({ zone, floor: f }) => (
            <AdminButton
              key={f.id}
              variant={f.id === current.floorId ? 'primary' : 'secondary'}
              aria-pressed={f.id === current.floorId}
              onClick={() => setFloorId(f.id)}
            >
              {`${zone.code} · ${f.name}${zone.isClosed || f.isClosed ? ' (closed)' : ''}`}
            </AdminButton>
          ))}
        </div>
        <div role="group" aria-label="View" className="flex gap-1">
          {(['2d', '3d'] as const).map((v) => (
            <AdminButton key={v} variant={view === v ? 'primary' : 'secondary'} aria-pressed={view === v} onClick={() => setView(v)}>
              {v.toUpperCase()}
            </AdminButton>
          ))}
        </div>
        <AdminButton variant="ghost" icon="clock" onClick={() => setDialog('versions')}>
          Versions
        </AdminButton>
        <AdminButton variant="secondary" icon="plus" onClick={() => setDialog('floor')}>
          Add floor
        </AdminButton>
        <AdminButton icon="edit" onClick={() => setEditing(true)}>
          Edit layout
        </AdminButton>
      </div>

      <Panel title={`${current.floorName} (level ${current.level})`} description={`${version} · ${summary(current)}`}>
        <div className="flex flex-col gap-3">
          {view === '2d' ? (
            floor.model.cells.length === 0 ? (
              <StateBlock type="empty" bare title="Empty floor" message="Use “Edit layout” to draw roads, entries and slots." />
            ) : (
              <LayoutGrid model={floor.model} showState />
            )
          ) : (
            <Suspense fallback={<StateBlock type="loading" />}>
              <Layout3DView floors={floors} activeFloorId={current.floorId} showState />
            </Suspense>
          )}
          <LayoutLegend showState />
        </div>
      </Panel>
      <Banner tone="info">
        Publishing updates the parking slots. The lot’s total and available counters shown to drivers are not recalculated by ParkingService
        yet.
      </Banner>

      {addFloor}
      {dialog === 'versions' && (
        <VersionsDialog
          floor={floor}
          onClose={() => setDialog(null)}
          onRestored={(versionNo) => {
            setDialog(null);
            toast.show({ type: 'success', title: 'Layout restored', message: `Version ${versionNo} is published again.` });
            reload();
          }}
        />
      )}
    </div>
  );
};
