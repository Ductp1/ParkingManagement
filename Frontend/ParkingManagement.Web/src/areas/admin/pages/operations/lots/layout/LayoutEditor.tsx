import React, { Suspense, lazy, useEffect, useRef, useState } from 'react';
import { adminLayoutApi } from '@/api/adminLayoutApi';
import { ActionDialog } from '../../../../components/ActionDialog';
import { AdminButton } from '../../../../components/AdminButton';
import { AdminDialog } from '../../../../components/AdminDialog';
import { Banner } from '../../../../components/Banner';
import { DetailList, DetailRow } from '../../../../components/DetailRow';
import { SelectField, TextField } from '../../../../components/FormFields';
import { QueryState } from '../../../../components/PageLayout';
import { Panel } from '../../../../components/Panel';
import { StateBlock } from '../../../../components/StateBlock';
import { useAdminQuery } from '../../../../hooks/useAdminQuery';
import { useAdminToast } from '../../../../hooks/useAdminToast';
import { humanizeEnum } from '../../../../utils/enumLabels';
import { LayoutGrid, LayoutLegend, TOOL_DRAG_TYPE } from './LayoutGrid';
import {
  MAX_CELL_SPAN,
  MAX_GRID,
  MIN_STANDARD_SIZE,
  PAINT_TYPES,
  SLOT_TYPES,
  VEHICLE_TYPES,
  canRemoveEdge,
  detectPrefix,
  diffSlots,
  fromDto,
  isSlot,
  renumberSlots,
  resizeEdge,
  slotCode,
  standardLayout,
  toCells,
  validateLayout,
  type FloorModel,
  type LayoutModel,
  type Side,
} from './layoutModel';
import { useLayoutEditing, type Tool } from './useLayoutEditing';

const Layout3DView = lazy(() => import('./Layout3DView'));

const TOOLS: Tool[] = ['select', ...PAINT_TYPES, 'eraser'];
const SIDES: Side[] = ['top', 'bottom', 'left', 'right'];
const ARROWS: Record<string, [number, number]> = { ArrowLeft: [-1, 0], ArrowRight: [1, 0], ArrowUp: [0, -1], ArrowDown: [0, 1] };

const errorText = (err: unknown) => (err instanceof Error ? err.message : String(err));
const options = (values: readonly string[]) => values.map((v) => ({ value: v, label: humanizeEnum(v) }));
const snapshot = (m: LayoutModel) => JSON.stringify([m.columns, m.rows, toCells(m)]);

interface LayoutEditorProps {
  /** Tầng đang sửa với sơ đồ đang áp dụng (để so thay đổi). */
  floor: FloorModel;
  /** Mọi tầng của bãi – dùng cho xem trước 3D. */
  floors: FloorModel[];
  onClose: () => void;
  onPublished: () => void;
}

/** Thêm / bỏ hàng hoặc cột ở một cạnh của lưới. */
const EdgeStepper: React.FC<{ side: Side; model: LayoutModel; onChange: (delta: 1 | -1) => void }> = ({ side, model, onChange }) => {
  const line = side === 'top' || side === 'bottom' ? 'row' : 'column';
  return (
    <div className="flex items-center gap-1.5">
      <span className="adm-type-body-regular-12 w-12 text-(--admin-text-secondary)">{humanizeEnum(side.charAt(0).toUpperCase() + side.slice(1))}</span>
      <AdminButton variant="secondary" aria-label={`Remove ${side} ${line}`} disabled={!canRemoveEdge(model, side)} onClick={() => onChange(-1)}>
        −
      </AdminButton>
      <AdminButton
        variant="secondary"
        aria-label={`Add ${line} at ${side}`}
        disabled={(line === 'row' ? model.rows : model.columns) >= MAX_GRID}
        onClick={() => onChange(1)}
      >
        +
      </AdminButton>
    </div>
  );
};

const EditorBody: React.FC<LayoutEditorProps & { draft: LayoutModel }> = ({ floor, floors, draft, onClose, onPublished }) => {
  const toast = useAdminToast();
  const [saved, setSaved] = useState(() => snapshot(draft));
  const [prefix, setPrefix] = useState(() => detectPrefix(draft));
  const [preview3d, setPreview3d] = useState(false);
  const [busy, setBusy] = useState<'save' | 'discard' | null>(null);
  const [apiError, setApiError] = useState<string | null>(null);
  const [dialog, setDialog] = useState<'publish' | 'discard' | 'close' | 'renumber' | 'standard' | null>(null);
  const editing = useLayoutEditing(draft, prefix);
  const { model, tool, selected } = editing;

  const published = floor.model;
  const { errors, warnings } = validateLayout(model, published);
  const diff = diffSlots(published, model);
  const dirty = snapshot(model) !== saved;
  const single = selected.length === 1 ? selected[0] : null;
  const payload = (changeNote?: string) => ({ columns: model.columns, rows: model.rows, cells: toCells(model), changeNote });
  const renumbered = renumberSlots(model, published, prefix);
  const renamed = renumbered.cells.filter((c, i) => isSlot(c) && slotCode(c) !== slotCode(model.cells[i])).length;
  const standard = standardLayout(model.columns, model.rows, prefix);

  // Phím tắt nghe ở window vì Safari không focus nút khi bấm chuột. Ref giữ trạng thái mới nhất.
  const shortcuts = useRef({ editing, dialog });
  useEffect(() => {
    shortcuts.current = { editing, dialog };
  });
  useEffect(() => {
    const onKey = (e: KeyboardEvent) => {
      const { editing: current, dialog: open } = shortcuts.current;
      const target = e.target as HTMLElement;
      if (open || target.closest?.('input, textarea, select, [role="dialog"]')) return;
      const command = e.ctrlKey || e.metaKey;
      const key = e.key.toLowerCase();
      if (command && (key === 'z' || key === 'y')) {
        e.preventDefault();
        if (key === 'y' || e.shiftKey) current.redo();
        else current.undo();
        return;
      }
      if (e.key === 'Escape') current.clearSelection();
      if (!current.selected.length) return;
      if (e.key === 'Delete' || e.key === 'Backspace') {
        e.preventDefault();
        current.deleteSelection();
      } else if (ARROWS[e.key]) {
        e.preventDefault();
        current.nudge(...ARROWS[e.key]);
      }
    };
    window.addEventListener('keydown', onKey);
    return () => window.removeEventListener('keydown', onKey);
  }, []);

  const run = async (kind: 'save' | 'discard', action: () => Promise<unknown>, done: () => void) => {
    setBusy(kind);
    setApiError(null);
    try {
      await action();
      done();
    } catch (err: unknown) {
      setApiError(errorText(err));
    } finally {
      setBusy(null);
    }
  };

  const saveDraft = () =>
    run(
      'save',
      () => adminLayoutApi.saveDraft(floor.floorId, payload('Admin draft')),
      () => {
        setSaved(snapshot(model));
        toast.show({ type: 'success', title: 'Draft saved', message: 'Drivers still see the published layout.' });
      }
    );

  return (
    <div className="flex flex-col gap-4">
      <Panel title={`Designing ${floor.floorName} (level ${floor.level})`}>
        <div className="flex flex-col gap-3">
          <div className="flex flex-wrap items-center gap-2" role="toolbar" aria-label="Layout tools">
            {TOOLS.map((t) => (
              <AdminButton
                key={t}
                variant={tool === t ? 'primary' : 'secondary'}
                aria-pressed={tool === t}
                onClick={() => editing.setTool(t)}
                draggable={t !== 'select'}
                onDragStart={(e) => e.dataTransfer.setData(TOOL_DRAG_TYPE, t)}
              >
                {humanizeEnum(t.charAt(0).toUpperCase() + t.slice(1))}
              </AdminButton>
            ))}
            <span className="mx-1 h-6 w-px bg-(--admin-border-default)" aria-hidden="true" />
            <AdminButton variant="secondary" onClick={editing.undo} disabled={!editing.canUndo} title="Ctrl/⌘ + Z">
              Undo
            </AdminButton>
            <AdminButton variant="secondary" onClick={editing.redo} disabled={!editing.canRedo} title="Ctrl/⌘ + Shift + Z">
              Redo
            </AdminButton>
          </div>
          <div className="flex flex-wrap items-end gap-x-6 gap-y-2">
            <div className="w-40">
              <TextField label="New slot prefix" value={prefix} onChange={setPrefix} maxLength={10} />
            </div>
            <fieldset className="m-0 flex flex-wrap items-center gap-x-4 gap-y-2 border-0 p-0">
              <legend className="adm-type-body-regular-12 mb-1 text-(--admin-text-secondary)">{`Grid ${model.columns} × ${model.rows} – add or remove at`}</legend>
              {SIDES.map((side) => (
                <EdgeStepper key={side} side={side} model={model} onChange={(delta) => editing.apply((m) => resizeEdge(m, side, delta))} />
              ))}
            </fieldset>
          </div>
          <div className="flex flex-wrap items-center gap-2">
            <AdminButton variant="secondary" icon="refresh" onClick={() => setDialog('renumber')} disabled={renamed === 0}>
              Renumber slots
            </AdminButton>
            <AdminButton variant="secondary" onClick={() => setDialog('standard')}>
              Generate standard layout
            </AdminButton>
            <AdminButton variant="ghost" aria-pressed={preview3d} onClick={() => setPreview3d((v) => !v)}>
              {preview3d ? 'Hide 3D preview' : 'Show 3D preview'}
            </AdminButton>
          </div>
          <span className="adm-type-body-small text-(--admin-text-secondary)">
            Select: drag cells to move them, drag on empty space to select an area, Shift+click to add. Arrows move, Delete removes. Paint
            tools: click or drag to paint, Shift+drag to fill an area, or drag a tool onto the grid. Renaming a slot code creates a new slot.
          </span>
          <LayoutGrid model={model} showState={false} selected={selected} edit={editing.grid} />
          <LayoutLegend showState={false} />
          {preview3d && (
            <Suspense fallback={<StateBlock type="loading" />}>
              <Layout3DView
                floors={floors.map((f) => (f.floorId === floor.floorId ? { ...f, model } : f))}
                activeFloorId={floor.floorId}
                showState={false}
              />
            </Suspense>
          )}
        </div>
      </Panel>

      {single && isSlot(single) ? (
        <Panel title="Selected slot">
          <div className="grid grid-cols-2 gap-3 md:grid-cols-5">
            <TextField label="Code" value={single.code ?? ''} onChange={(v) => editing.patchSelected({ code: v.toUpperCase() })} maxLength={20} />
            <SelectField label="Slot type" value={single.slotType ?? ''} onChange={(v) => editing.patchSelected({ slotType: v || null })} options={options(SLOT_TYPES)} />
            <SelectField label="Largest vehicle" value={single.maxVehicleType ?? ''} onChange={(v) => editing.patchSelected({ maxVehicleType: v || null })} options={options(VEHICLE_TYPES)} />
            {(['widthCells', 'heightCells'] as const).map((key) => (
              <SelectField
                key={key}
                label={key === 'widthCells' ? 'Width (cells)' : 'Length (cells)'}
                value={String(single[key])}
                onChange={(v) => v && editing.patchSelected({ [key]: Number(v) })}
                options={Array.from({ length: MAX_CELL_SPAN }, (_, i) => ({ value: String(i + 1), label: String(i + 1) }))}
              />
            ))}
          </div>
        </Panel>
      ) : (
        selected.length > 0 && (
          <div className="flex items-center gap-3">
            <span className="adm-type-body-strong text-(--admin-text-primary)">{`${selected.length} cell(s) selected`}</span>
            <AdminButton variant="secondary" icon="trash" onClick={editing.deleteSelection}>
              Delete selected
            </AdminButton>
            <AdminButton variant="ghost" onClick={editing.clearSelection}>
              Clear selection
            </AdminButton>
          </div>
        )
      )}

      {errors.length > 0 && <Banner tone="danger">{errors.join(' ')}</Banner>}
      {warnings.length > 0 && <Banner tone="warning">{warnings.join(' ')}</Banner>}
      {apiError && <Banner tone="danger">{apiError}</Banner>}

      <div className="flex flex-wrap items-center gap-2">
        <span className="adm-type-body-regular-12 mr-auto text-(--admin-text-secondary)">
          {`Compared with the published layout: ${diff.added.length} added, ${diff.removed.length} removed, ${diff.moved.length} moved, ${diff.retyped.length} changed type.`}
          {dirty ? ' Unsaved changes.' : ''}
        </span>
        <AdminButton variant="ghost" onClick={() => (dirty ? setDialog('close') : onClose())} disabled={busy !== null}>
          Close editor
        </AdminButton>
        <AdminButton variant="secondary" onClick={() => setDialog('discard')} disabled={busy !== null}>
          Discard draft
        </AdminButton>
        <AdminButton variant="secondary" onClick={saveDraft} disabled={busy !== null || errors.length > 0} loading={busy === 'save'} loadingLabel="Saving…">
          Save draft
        </AdminButton>
        <AdminButton onClick={() => setDialog('publish')} disabled={busy !== null || errors.length > 0}>
          Publish
        </AdminButton>
      </div>

      {dialog === 'publish' && (
        <ActionDialog
          open
          title="Publish layout"
          description="The layout replaces the current one for the whole system: parking slots are created, updated or switched off by slot code."
          confirmLabel="Publish"
          field={{ label: 'Change note', required: true, placeholder: 'What changed on this floor?', maxLength: 500 }}
          onClose={() => setDialog(null)}
          onSubmit={async (note) => {
            await adminLayoutApi.publish(floor.floorId, payload(note));
            toast.show({ type: 'success', title: 'Layout published', message: `${floor.floorName} now uses the new layout.` });
            onPublished();
          }}
        >
          <DetailList>
            <DetailRow label="Slots after publish">{model.cells.filter(isSlot).length}</DetailRow>
            <DetailRow label="Added">{diff.added.join(', ') || '—'}</DetailRow>
            <DetailRow label="Removed">{diff.removed.join(', ') || '—'}</DetailRow>
            <DetailRow label="Moved">{diff.moved.join(', ') || '—'}</DetailRow>
            <DetailRow label="Changed type">{diff.retyped.join(', ') || '—'}</DetailRow>
          </DetailList>
          {warnings.length > 0 && <Banner tone="warning">{warnings.join(' ')}</Banner>}
        </ActionDialog>
      )}

      <AdminDialog
        open={dialog === 'renumber'}
        title="Renumber slots?"
        description={`${renamed} slot(s) get a new code in reading order (prefix “${prefix.trim().toUpperCase()}”). Slot records follow their code, so occupied or reserved slots keep theirs. You can undo this.`}
        confirmLabel="Renumber"
        onClose={() => setDialog(null)}
        onConfirm={() => {
          editing.apply(() => renumbered);
          setDialog(null);
        }}
      />
      <AdminDialog
        open={dialog === 'standard'}
        title="Generate standard layout?"
        description={
          standard
            ? `Replaces this drawing with walls, an entry, an exit, driving aisles and ${standard.cells.filter(isSlot).length} slots on the current ${model.columns} × ${model.rows} grid. You can undo this.`
            : `The grid needs at least ${MIN_STANDARD_SIZE} × ${MIN_STANDARD_SIZE} cells.`
        }
        destructive
        confirmLabel="Replace drawing"
        confirmDisabled={!standard}
        onClose={() => setDialog(null)}
        onConfirm={() => {
          if (standard) editing.apply(() => standard);
          setDialog(null);
        }}
      />
      <AdminDialog
        open={dialog === 'discard'}
        title="Discard draft?"
        description="The saved draft and unsaved changes are removed. The published layout stays as it is."
        destructive
        confirmLabel="Discard draft"
        cancelLabel="Keep editing"
        submitting={busy === 'discard'}
        error={apiError}
        onClose={() => setDialog(null)}
        onConfirm={() => run('discard', () => adminLayoutApi.discardDraft(floor.floorId), onClose)}
      />
      <AdminDialog
        open={dialog === 'close'}
        title="Close without saving?"
        description="Changes made since the last saved draft are lost."
        destructive
        confirmLabel="Close editor"
        cancelLabel="Keep editing"
        onClose={() => setDialog(null)}
        onConfirm={onClose}
      />
    </div>
  );
};

/** Trình thiết kế sơ đồ một tầng: mở bản nháp (nếu có), lưu nháp, xuất bản. */
export const LayoutEditor: React.FC<LayoutEditorProps> = (props) => {
  const query = useAdminQuery(() => adminLayoutApi.getDraft(props.floor.floorId), [props.floor.floorId]);
  if (query.status !== 'ready' || !query.data) {
    return (
      <div className="flex flex-col gap-2">
        <QueryState
          status={query.status}
          error={query.error}
          onRetry={query.refetch}
          bare={false}
          copy={{ emptyTitle: 'No layout', emptyMessage: '', errorTitle: 'Could not load the draft', service: 'ParkingService' }}
        />
        <AdminButton variant="ghost" onClick={props.onClose} className="self-start">
          Back to the layout
        </AdminButton>
      </div>
    );
  }
  return <EditorBody {...props} draft={fromDto(query.data)} />;
};
