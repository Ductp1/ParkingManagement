import React, { useState } from 'react';
import { adminLayoutApi } from '@/api/adminLayoutApi';
import type { ZoneHierarchyDto } from '@/types/adminOperations';
import type { LayoutVersionSummaryDto } from '@/types/ownerLayout';
import { ActionDialog } from '../../../../components/ActionDialog';
import { AdminButton } from '../../../../components/AdminButton';
import { AdminDialog } from '../../../../components/AdminDialog';
import { CellText, CellTwoLine, DataTable } from '../../../../components/DataTable';
import { SelectField, TextField, ToggleField } from '../../../../components/FormFields';
import { QueryState } from '../../../../components/PageLayout';
import { useAdminQuery } from '../../../../hooks/useAdminQuery';
import { formatDateTimeIct } from '../../../../utils/format';
import { MAX_GRID, type FloorModel } from './layoutModel';

const NEW_ZONE = 'new';

interface AddFloorDialogProps {
  lotId: number;
  zones: ZoneHierarchyDto[];
  onClose: () => void;
  onDone: (floorId: number) => void;
}

/** Thêm tầng vào khu có sẵn, hoặc tạo khu mới rồi thêm tầng. */
export const AddFloorDialog: React.FC<AddFloorDialogProps> = ({ lotId, zones, onClose, onDone }) => {
  const [zoneId, setZoneId] = useState(zones.length ? String(zones[0].id) : NEW_ZONE);
  const [zoneCode, setZoneCode] = useState('');
  const [zoneName, setZoneName] = useState('');
  const [outdoor, setOutdoor] = useState(false);
  const [name, setName] = useState('');
  const [level, setLevel] = useState('0');
  const [columns, setColumns] = useState('12');
  const [rows, setRows] = useState('6');
  const [submitted, setSubmitted] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [apiError, setApiError] = useState<string | null>(null);
  // Khu vừa tạo (tạo tầng lỗi thì thử lại không tạo khu lần nữa).
  const [createdZone, setCreatedZone] = useState<{ id: number; label: string } | null>(null);

  const required = (v: string) => (v.trim() === '' ? 'Required.' : null);
  const whole = (v: string, min: number, max: number) =>
    v.trim() !== '' && Number.isInteger(Number(v)) && Number(v) >= min && Number(v) <= max ? null : `A whole number from ${min} to ${max}.`;
  const isNew = zoneId === NEW_ZONE;
  const errors = {
    zoneCode: isNew ? required(zoneCode) : null,
    zoneName: isNew ? required(zoneName) : null,
    name: required(name),
    level: whole(level, -10, 50),
    columns: whole(columns, 1, MAX_GRID),
    rows: whole(rows, 1, MAX_GRID),
  };
  const show = (message: string | null) => (submitted ? message : null);

  const confirm = async () => {
    setSubmitted(true);
    if (Object.values(errors).some(Boolean)) return;
    setSubmitting(true);
    setApiError(null);
    try {
      let target = Number(zoneId);
      if (isNew) {
        const zone = await adminLayoutApi.createZone(lotId, { code: zoneCode.trim(), name: zoneName.trim(), isOutdoor: outdoor });
        target = zone.id;
        setCreatedZone({ id: zone.id, label: `${zoneCode.trim()} · ${zoneName.trim()}` });
        setZoneId(String(zone.id));
      }
      const floor = await adminLayoutApi.createFloor(target, {
        name: name.trim(),
        level: Number(level),
        gridColumns: Number(columns),
        gridRows: Number(rows),
      });
      onDone(floor.id);
    } catch (err: unknown) {
      setApiError(err instanceof Error ? err.message : String(err));
    } finally {
      setSubmitting(false);
    }
  };

  const zoneOptions = [
    ...zones.map((z) => ({ value: String(z.id), label: `${z.code} · ${z.name}` })),
    ...(createdZone ? [{ value: String(createdZone.id), label: createdZone.label }] : []),
    { value: NEW_ZONE, label: 'New zone…' },
  ];

  return (
    <AdminDialog
      open
      size={640}
      title="Add floor"
      description="The floor starts empty; design its layout after it is created."
      confirmLabel="Add floor"
      onClose={onClose}
      onConfirm={confirm}
      submitting={submitting}
      error={apiError}
    >
      <SelectField label="Zone" required value={zoneId} onChange={(v) => v && setZoneId(v)} options={zoneOptions} />
      {isNew && (
        <>
          <div className="grid grid-cols-2 gap-4">
            <TextField label="Zone code" required value={zoneCode} onChange={setZoneCode} maxLength={10} error={show(errors.zoneCode)} />
            <TextField label="Zone name" required value={zoneName} onChange={setZoneName} maxLength={100} error={show(errors.zoneName)} />
          </div>
          <ToggleField label="Outdoor zone" checked={outdoor} onChange={setOutdoor} />
        </>
      )}
      <div className="grid grid-cols-2 gap-4">
        <TextField label="Floor name" required value={name} onChange={setName} maxLength={100} placeholder="B1" error={show(errors.name)} />
        <TextField label="Level" type="number" required value={level} onChange={setLevel} helper="0 = ground, -1 = basement 1" error={show(errors.level)} />
        <TextField label="Grid columns" type="number" required value={columns} onChange={setColumns} error={show(errors.columns)} />
        <TextField label="Grid rows" type="number" required value={rows} onChange={setRows} error={show(errors.rows)} />
      </div>
    </AdminDialog>
  );
};

/** Lịch sử phiên bản đã xuất bản + khôi phục. */
export const VersionsDialog: React.FC<{ floor: FloorModel; onClose: () => void; onRestored: (versionNo: number) => void }> = ({
  floor,
  onClose,
  onRestored,
}) => {
  const query = useAdminQuery(() => adminLayoutApi.versions(floor.floorId), [floor.floorId], (d) => !d.some((v) => v.isPublished));
  const [restoring, setRestoring] = useState<LayoutVersionSummaryDto | null>(null);
  const published = (query.data ?? []).filter((v) => v.isPublished).sort((a, b) => b.versionNo - a.versionNo);

  if (restoring) {
    return (
      <ActionDialog
        open
        title={`Restore version ${restoring.versionNo}?`}
        description="It is published again as a new version and parking slots are synchronised with it."
        confirmLabel="Restore"
        field={{ label: 'Reason', required: true, placeholder: 'Why restore this version?', maxLength: 500 }}
        onClose={() => setRestoring(null)}
        onSubmit={async (note) => {
          await adminLayoutApi.rollback(floor.floorId, restoring.versionNo, note);
          onRestored(restoring.versionNo);
        }}
      />
    );
  }

  return (
    <AdminDialog open size={640} title={`Layout versions · ${floor.floorName}`} cancelLabel="Close" onClose={onClose}>
      {query.status !== 'ready' ? (
        <QueryState
          status={query.status}
          error={query.error}
          onRetry={query.refetch}
          copy={{ emptyTitle: 'No published version', emptyMessage: 'This floor has not been published yet.', errorTitle: 'Could not load versions', service: 'ParkingService' }}
        />
      ) : (
        <DataTable
          bare
          rows={published}
          rowKey={(v) => v.id}
          columns={[
            { key: 'version', header: 'Version', width: 90, render: (v) => <CellText>{`v${v.versionNo}`}</CellText> },
            {
              key: 'published',
              header: 'Published',
              width: 170,
              render: (v) => <CellTwoLine primary={v.publishedAtUtc ? formatDateTimeIct(v.publishedAtUtc) : '—'} secondary={`User #${v.createdByUserId}`} />,
            },
            { key: 'note', header: 'Note', render: (v) => <CellText>{v.changeNote ?? '—'}</CellText> },
            {
              key: 'restore',
              header: '',
              width: 100,
              render: (v) =>
                v === published[0] ? (
                  <CellText>Current</CellText>
                ) : (
                  <AdminButton variant="ghost" icon="refresh" onClick={() => setRestoring(v)}>
                    Restore
                  </AdminButton>
                ),
            },
          ]}
        />
      )}
    </AdminDialog>
  );
};
