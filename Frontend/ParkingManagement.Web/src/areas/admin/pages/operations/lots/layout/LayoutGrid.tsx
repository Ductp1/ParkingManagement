import React, { useRef } from 'react';
import type { LayoutCellDto } from '@/types/ownerLayout';
import { humanizeEnum } from '../../../../utils/enumLabels';
import { LAYOUT_COLORS, cellAt, colorKeyOf, isSlot, type LayoutColorKey, type LayoutModel, type Point } from './layoutModel';

const CELL_WIDTH = 40;
const CELL_HEIGHT = 28;
const GAP = 2;
/** Kiểu dữ liệu kéo-thả từ bảng công cụ vào lưới. */
export const TOOL_DRAG_TYPE = 'text/layout-tool';

const CELL_LABEL: Record<string, string> = { entry: 'IN', exit: 'OUT', ramp: 'RAMP', pillar: '●' };

const describe = (cell: LayoutCellDto | undefined, showState: boolean) => {
  if (!cell) return 'empty';
  if (!isSlot(cell)) return cell.type;
  return ['slot', cell.code, showState ? cell.state : cell.slotType].filter(Boolean).join(' ');
};

type Area = Pick<LayoutCellDto, 'x' | 'y' | 'widthCells' | 'heightCells'>;

export interface GridEditHandlers {
  onStart: (at: Point, shift: boolean) => void;
  onMove: (at: Point) => void;
  onEnd: () => void;
  onCancel: () => void;
  /** Kích hoạt một ô bằng bàn phím. */
  onActivate: (at: Point, shift: boolean) => void;
  /** Đang kéo một công cụ ở trên lưới (null = đã ra ngoài). */
  onDragTool: (at: Point | null) => void;
  onDropTool: (tool: string, at: Point) => void;
  /** Bóng mờ vị trí đích khi kéo ô / công cụ. */
  ghost: { cells: Area[]; valid: boolean } | null;
  /** Khung chọn hoặc vùng sắp tô. */
  marquee: { x: number; y: number; width: number; height: number } | null;
}

interface LayoutGridProps {
  model: LayoutModel;
  /** true = xem bản đang áp dụng (màu theo trạng thái ô đỗ); false = đang thiết kế. */
  showState: boolean;
  selected?: LayoutCellDto[];
  /** Có giá trị = chế độ sửa: mỗi ô là một nút, chuột / cảm ứng kéo được trên lưới. */
  edit?: GridEditHandlers;
}

const placed = (x: number, y: number, width: number, height: number): React.CSSProperties => ({
  gridColumn: `${x + 1} / span ${width}`,
  gridRow: `${y + 1} / span ${height}`,
});

/** Sơ đồ 2D vẽ đúng vị trí x / y và kích thước ô, như tài xế nhìn bãi từ trên xuống. */
export const LayoutGrid: React.FC<LayoutGridProps> = ({ model, showState, selected = [], edit }) => {
  const gridRef = useRef<HTMLDivElement>(null);
  const pressed = useRef(false);

  /** Ô lưới dưới con trỏ; ngoài lưới thì kẹp về ô sát biên. */
  const pointAt = (e: { clientX: number; clientY: number }): Point => {
    const box = gridRef.current!.getBoundingClientRect();
    const clamp = (v: number, max: number) => Math.min(max - 1, Math.max(0, v));
    return {
      x: clamp(Math.floor((e.clientX - box.left) / (CELL_WIDTH + GAP)), model.columns),
      y: clamp(Math.floor((e.clientY - box.top) / (CELL_HEIGHT + GAP)), model.rows),
    };
  };

  const emptyPositions: Point[] = [];
  for (let y = 0; y < model.rows; y += 1)
    for (let x = 0; x < model.columns; x += 1) if (!cellAt(model, x, y)) emptyPositions.push({ x, y });

  const tile = (x: number, y: number, width: number, height: number, cell?: LayoutCellDto) => {
    const style: React.CSSProperties = {
      ...placed(x, y, width, height),
      background: cell ? LAYOUT_COLORS[colorKeyOf(cell, showState)] : undefined,
    };
    const dark = !cell || cell.type === 'road';
    const isSelected = !!cell && selected.includes(cell);
    const className = `flex min-h-0 min-w-0 items-center justify-center overflow-hidden rounded-[2px] text-[9px] leading-none font-bold ${
      dark ? 'border border-dashed border-(--admin-border-default) text-(--admin-text-primary)' : 'text-white'
    } ${isSelected ? 'outline-2 outline-offset-1 outline-(--admin-brand-navy-900)' : ''}`;
    const text = cell ? (isSlot(cell) ? cell.code : CELL_LABEL[cell.type]) : null;
    const label = `Column ${x + 1}, row ${y + 1}: ${describe(cell, showState)}`;
    if (!edit) {
      return (
        <div key={`${x},${y},${cell?.type ?? 'empty'}`} role="img" aria-label={label} title={describe(cell, showState)} className={className} style={style}>
          {text}
        </div>
      );
    }
    return (
      <button
        key={`${x},${y}`}
        type="button"
        aria-label={label}
        aria-pressed={cell ? isSelected : undefined}
        className={`${className} cursor-crosshair p-0`}
        style={style}
        // Chuột / cảm ứng xử lý bằng pointer events trên lưới; click chỉ còn cho bàn phím (detail = 0).
        onClick={(e) => {
          if (e.detail === 0) edit.onActivate({ x, y }, e.shiftKey);
        }}
      >
        {text}
      </button>
    );
  };

  const editing: React.HTMLAttributes<HTMLDivElement> = edit
    ? {
        onPointerDown: (e) => {
          if (e.button !== 0) return;
          pressed.current = true;
          // Giữ mọi sự kiện kéo ở lưới, kể cả khi con trỏ / ngón tay ra khỏi lưới.
          gridRef.current?.setPointerCapture?.(e.pointerId);
          edit.onStart(pointAt(e), e.shiftKey);
        },
        onPointerMove: (e) => {
          if (pressed.current) edit.onMove(pointAt(e));
        },
        onPointerUp: () => {
          if (!pressed.current) return;
          pressed.current = false;
          edit.onEnd();
        },
        onPointerCancel: () => {
          pressed.current = false;
          edit.onCancel();
        },
        onDragOver: (e) => {
          if (!e.dataTransfer.types.includes(TOOL_DRAG_TYPE)) return;
          e.preventDefault();
          edit.onDragTool(pointAt(e));
        },
        onDragLeave: (e) => {
          if (!e.currentTarget.contains(e.relatedTarget as Node | null)) edit.onDragTool(null);
        },
        onDrop: (e) => {
          const tool = e.dataTransfer.getData(TOOL_DRAG_TYPE);
          if (!tool) return;
          e.preventDefault();
          edit.onDropTool(tool, pointAt(e));
        },
      }
    : {};

  return (
    <div className="max-w-full overflow-auto border border-(--admin-border-default) bg-(--admin-surface-page) p-2">
      <div
        ref={gridRef}
        data-testid="layout-grid"
        className={`grid w-max select-none ${edit ? 'touch-none' : ''}`}
        style={{
          gap: GAP,
          gridTemplateColumns: `repeat(${model.columns}, ${CELL_WIDTH}px)`,
          gridTemplateRows: `repeat(${model.rows}, ${CELL_HEIGHT}px)`,
        }}
        {...editing}
      >
        {emptyPositions.map((p) => tile(p.x, p.y, 1, 1))}
        {model.cells.map((c) => tile(c.x, c.y, c.widthCells, c.heightCells, c))}
        {edit?.ghost?.cells
          .filter((c) => c.x >= 0 && c.y >= 0 && c.x + c.widthCells <= model.columns && c.y + c.heightCells <= model.rows)
          .map((c) => (
            <div
              key={`ghost-${c.x},${c.y}`}
              aria-hidden="true"
              data-ghost={edit.ghost!.valid ? 'valid' : 'blocked'}
              className={`pointer-events-none z-10 rounded-[2px] border-2 ${
                edit.ghost!.valid
                  ? 'border-(--admin-status-success) bg-(--admin-status-success)/30'
                  : 'border-(--admin-status-danger) bg-(--admin-status-danger)/30'
              }`}
              style={placed(c.x, c.y, c.widthCells, c.heightCells)}
            />
          ))}
        {edit?.marquee && (
          <div
            aria-hidden="true"
            className="pointer-events-none z-10 rounded-[2px] border-2 border-dashed border-(--admin-brand-navy-900) bg-(--admin-brand-navy-900)/10"
            style={placed(edit.marquee.x, edit.marquee.y, edit.marquee.width, edit.marquee.height)}
          />
        )}
      </div>
    </div>
  );
};

const STATE_KEYS: LayoutColorKey[] = ['Available', 'Occupied', 'Reserved', 'Maintenance', 'Unavailable', 'EvCharging'];
const DESIGN_KEYS: LayoutColorKey[] = ['Available', 'EvCharging'];
const STRUCTURE_KEYS: LayoutColorKey[] = ['road', 'entry', 'exit', 'ramp', 'pillar', 'wall'];

export const LayoutLegend: React.FC<{ showState: boolean }> = ({ showState }) => (
  <ul className="m-0 flex list-none flex-wrap gap-x-4 gap-y-1 p-0">
    {[...(showState ? STATE_KEYS : DESIGN_KEYS), ...STRUCTURE_KEYS].map((key) => (
      <li key={key} className="adm-type-body-small flex items-center gap-1.5 text-(--admin-text-secondary)">
        <span className="size-3 rounded-[2px]" style={{ background: LAYOUT_COLORS[key] }} aria-hidden="true" />
        {!showState && key === 'Available' ? 'Slot' : humanizeEnum(key.charAt(0).toUpperCase() + key.slice(1))}
      </li>
    ))}
  </ul>
);
