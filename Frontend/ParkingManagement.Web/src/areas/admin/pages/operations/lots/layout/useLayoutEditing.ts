import { useRef, useState } from 'react';
import type { LayoutCellDto } from '@/types/ownerLayout';
import {
  cellAt,
  cellsInRect,
  commit,
  fillRect,
  lineBetween,
  moveCells,
  paint,
  rectOf,
  redo,
  removeCells,
  replaceCell,
  startHistory,
  undo,
  type LayoutModel,
  type PaintType,
  type Point,
} from './layoutModel';
import type { GridEditHandlers } from './LayoutGrid';

export type Tool = PaintType | 'eraser' | 'select';

type Drag =
  | { kind: 'paint'; tool: PaintType | 'eraser'; last: Point; stroke: string }
  | { kind: 'fill'; tool: PaintType | 'eraser'; from: Point; to: Point }
  | { kind: 'marquee'; from: Point; to: Point; keep: LayoutCellDto[] }
  | { kind: 'move'; from: Point; to: Point; cells: LayoutCellDto[] };

/** Trạng thái chỉnh sửa của trình thiết kế: lịch sử undo / redo, công cụ, vùng chọn và thao tác kéo thả. */
export const useLayoutEditing = (draft: LayoutModel, prefix: string) => {
  const [history, setHistory] = useState(() => startHistory(draft));
  const [tool, setTool] = useState<Tool>('select');
  const [selection, setSelection] = useState<LayoutCellDto[]>([]);
  const [drag, setDragState] = useState<Drag | null>(null);
  const [paletteHover, setPaletteHover] = useState<Point | null>(null);
  // Sự kiện chuột đến dồn dập trước khi render lại nên thao tác đang kéo đọc từ ref.
  const dragRef = useRef<Drag | null>(null);
  const strokes = useRef(0);

  const model = history.present;
  // Ô bị thay thế / xóa (kể cả do undo) tự rời vùng chọn.
  const selected = selection.filter((c) => model.cells.includes(c));

  const setDrag = (next: Drag | null) => {
    dragRef.current = next;
    setDragState(next);
  };

  /** Mọi thay đổi đi qua đây; dạng hàm để các sự kiện liên tiếp cộng dồn đúng. */
  const change = (fn: (m: LayoutModel) => LayoutModel, stroke: string | null = null) =>
    setHistory((h) => commit(h, fn(h.present), stroke));

  const toggle = (cell: LayoutCellDto) =>
    setSelection(selected.includes(cell) ? selected.filter((c) => c !== cell) : [...selected, cell]);

  const start = (at: Point, shift: boolean) => {
    if (tool !== 'select') {
      if (shift) return setDrag({ kind: 'fill', tool, from: at, to: at });
      strokes.current += 1;
      const stroke = `paint-${strokes.current}`;
      setDrag({ kind: 'paint', tool, last: at, stroke });
      return change((m) => paint(m, tool, at.x, at.y, prefix), stroke);
    }
    const cell = cellAt(model, at.x, at.y);
    if (!cell) {
      const keep = shift ? selected : [];
      setSelection(keep);
      return setDrag({ kind: 'marquee', from: at, to: at, keep });
    }
    if (shift) return toggle(cell);
    const cells = selected.includes(cell) ? selected : [cell];
    setSelection(cells);
    setDrag({ kind: 'move', from: at, to: at, cells });
  };

  const move = (to: Point) => {
    const d = dragRef.current;
    if (!d) return;
    if (d.kind !== 'paint') return setDrag({ ...d, to });
    if (to.x === d.last.x && to.y === d.last.y) return;
    const path = lineBetween(d.last, to);
    setDrag({ ...d, last: to });
    change(
      (m) => path.reduce((next, p) => (p.x < next.columns && p.y < next.rows ? paint(next, d.tool, p.x, p.y, prefix) : next), m),
      d.stroke
    );
  };

  const moveSelection = (cells: LayoutCellDto[], dx: number, dy: number) => {
    if (!cells.length || (dx === 0 && dy === 0)) return;
    const result = moveCells(model, cells, dx, dy);
    if (!result) return;
    change(() => result.model);
    setSelection(result.moved);
  };

  const end = () => {
    const d = dragRef.current;
    if (!d) return;
    setDrag(null);
    if (d.kind === 'fill') change((m) => fillRect(m, d.tool, d.from, d.to, prefix));
    if (d.kind === 'marquee') setSelection([...new Set([...d.keep, ...cellsInRect(model, d.from, d.to)])]);
    if (d.kind === 'move') moveSelection(d.cells, d.to.x - d.from.x, d.to.y - d.from.y);
  };

  /** Enter / Space trên một ô (bàn phím): chọn ô, hoặc tô bằng công cụ đang chọn. */
  const activate = (at: Point, shift: boolean) => {
    if (tool !== 'select') return change((m) => paint(m, tool, at.x, at.y, prefix));
    const cell = cellAt(model, at.x, at.y);
    if (!cell) return setSelection([]);
    if (shift) return toggle(cell);
    setSelection([cell]);
  };

  const drop = (dropped: Tool, at: Point) => {
    setPaletteHover(null);
    if (dropped !== 'select') change((m) => paint(m, dropped, at.x, at.y, prefix));
  };

  const ghost = (() => {
    if (drag?.kind === 'move') {
      const dx = drag.to.x - drag.from.x;
      const dy = drag.to.y - drag.from.y;
      if (!dx && !dy) return null;
      return {
        cells: drag.cells.map((c) => ({ x: c.x + dx, y: c.y + dy, widthCells: c.widthCells, heightCells: c.heightCells })),
        valid: moveCells(model, drag.cells, dx, dy) !== null,
      };
    }
    if (paletteHover) return { cells: [{ ...paletteHover, widthCells: 1, heightCells: 1 }], valid: true };
    return null;
  })();

  const grid: GridEditHandlers = {
    onStart: start,
    onMove: move,
    onEnd: end,
    onCancel: () => setDrag(null),
    onActivate: activate,
    onDragTool: setPaletteHover,
    onDropTool: (dragged, at) => drop(dragged as Tool, at),
    ghost,
    marquee: drag?.kind === 'marquee' || drag?.kind === 'fill' ? rectOf(drag.from, drag.to) : null,
  };

  return {
    model,
    tool,
    setTool,
    selected,
    grid,
    canUndo: history.past.length > 0,
    canRedo: history.future.length > 0,
    undo: () => setHistory(undo),
    redo: () => setHistory(redo),
    /** Thay cả sơ đồ hoặc đổi kích thước: một bước undo. */
    apply: change,
    nudge: (dx: number, dy: number) => moveSelection(selected, dx, dy),
    deleteSelection: () => {
      if (selected.length) change((m) => removeCells(m, selected));
    },
    clearSelection: () => setSelection([]),
    /** Sửa ô đang chọn; các lần gõ liên tiếp cùng một trường gộp thành một bước undo. */
    patchSelected: (patch: Partial<LayoutCellDto>) => {
      const cell = selected[0];
      if (!cell) return;
      const next = { ...cell, ...patch };
      change((m) => replaceCell(m, cell, next), `field-${cell.x},${cell.y}-${Object.keys(patch).join()}`);
      setSelection([next]);
    },
  };
};
