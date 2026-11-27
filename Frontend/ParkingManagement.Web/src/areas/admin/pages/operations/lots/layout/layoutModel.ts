import type { FloorLayoutDto, LayoutCellDto, LayoutCellInputDto } from '@/types/ownerLayout';

/** Hàm thuần cho trình thiết kế sơ đồ tầng: chỉ giữ ô có nội dung, ô trống được suy ra từ kích thước lưới. */
export interface LayoutModel {
  columns: number;
  rows: number;
  cells: LayoutCellDto[];
}

/** Một tầng kèm sơ đồ, dùng cho màn xem, trình thiết kế và mô hình 3D. */
export interface FloorModel {
  floorId: number;
  floorName: string;
  level: number;
  model: LayoutModel;
}

export const MAX_GRID = 60;
export const MAX_CELL_SPAN = 4;

export const PAINT_TYPES = ['slot', 'road', 'entry', 'exit', 'ramp', 'pillar', 'wall'] as const;
export type PaintType = (typeof PAINT_TYPES)[number];

export const SLOT_TYPES = ['Standard', 'Oversized', 'EvCharging', 'Disabled', 'Motorbike', 'Vip'] as const;
export const VEHICLE_TYPES = ['Motorbike', 'CompactCar', 'Sedan', 'Suv', 'Van', 'Truck'] as const;

/** Ô xe đi được: ô đỗ phải kề ít nhất một ô loại này. */
export const DRIVABLE = new Set(['road', 'entry', 'exit', 'ramp']);

/** Màu dùng chung cho sơ đồ 2D và mô hình 3D. */
export const LAYOUT_COLORS = {
  Available: '#16a149',
  Occupied: '#dc2626',
  Reserved: '#d27306',
  Maintenance: '#64748b',
  Unavailable: '#94a3b8',
  EvCharging: '#0891b2',
  road: '#d5dae3',
  entry: '#15803d',
  exit: '#b91c1c',
  ramp: '#7c3aed',
  pillar: '#475569',
  wall: '#1e293b',
} as const;
export type LayoutColorKey = keyof typeof LAYOUT_COLORS;

export const isSlot = (c: LayoutCellDto) => c.type === 'slot';
export const slotCode = (c: LayoutCellDto) => (c.code ?? '').trim().toUpperCase();

/** Màu của một ô. showState = false (đang thiết kế) thì chỉ phân biệt loại ô, không theo trạng thái đang đỗ. */
export const colorKeyOf = (c: LayoutCellDto, showState: boolean): LayoutColorKey => {
  if (isSlot(c)) {
    if (showState && c.state && c.state !== 'Available' && c.state in LAYOUT_COLORS) return c.state as LayoutColorKey;
    return c.slotType === 'EvCharging' ? 'EvCharging' : 'Available';
  }
  return c.type in LAYOUT_COLORS ? (c.type as LayoutColorKey) : 'wall';
};

export const fromDto = (dto: FloorLayoutDto): LayoutModel => ({
  columns: Math.max(1, dto.columns),
  rows: Math.max(1, dto.rows),
  cells: dto.cells
    .filter((c) => c.type !== 'empty')
    .map((c) => ({ ...c, widthCells: c.widthCells || 1, heightCells: c.heightCells || 1 })),
});

const covers = (c: LayoutCellDto, x: number, y: number) =>
  x >= c.x && x < c.x + c.widthCells && y >= c.y && y < c.y + c.heightCells;

export const cellAt = (model: LayoutModel, x: number, y: number) => model.cells.find((c) => covers(c, x, y));

/** Tiền tố mã ô hay dùng nhất ("A-01" → "A-"). */
export const detectPrefix = (model: LayoutModel): string => {
  const counts = new Map<string, number>();
  for (const c of model.cells.filter(isSlot)) {
    const match = /^(.*?)\d+$/.exec(slotCode(c));
    if (match) counts.set(match[1], (counts.get(match[1]) ?? 0) + 1);
  }
  return [...counts].sort((a, b) => b[1] - a[1])[0]?.[0] ?? 'S-';
};

/**
 * Mã cho ô mới: số nhỏ nhất chưa dùng. Nhờ vậy xóa A-05 rồi vẽ lại chỗ khác vẫn là A-05 –
 * ParkingService coi là cùng một ô (giữ trạng thái) thay vì tắt ô cũ và tạo ô mới.
 */
export const nextSlotCode = (model: LayoutModel, prefix: string): string => {
  const p = prefix.trim().toUpperCase();
  const used = new Set(model.cells.filter(isSlot).map(slotCode));
  const widths = [...used]
    .filter((code) => code.startsWith(p) && /^\d+$/.test(code.slice(p.length)))
    .map((code) => code.length - p.length);
  const pad = widths.length ? Math.max(...widths) : 2;
  let n = 1;
  while (used.has(`${p}${String(n).padStart(pad, '0')}`)) n += 1;
  return `${p}${String(n).padStart(pad, '0')}`;
};

/** Tô một ô bằng công cụ đang chọn. Tô đúng loại đang có thì giữ nguyên (không đổi mã ô). */
export const paint = (model: LayoutModel, tool: PaintType | 'eraser', x: number, y: number, prefix: string): LayoutModel => {
  const current = cellAt(model, x, y);
  if (tool === 'eraser') return current ? { ...model, cells: model.cells.filter((c) => !covers(c, x, y)) } : model;
  if (current?.type === tool) return model;
  const rest: LayoutModel = { ...model, cells: model.cells.filter((c) => !covers(c, x, y)) };
  const cell: LayoutCellDto = { x, y, type: tool, widthCells: 1, heightCells: 1 };
  if (tool === 'slot') Object.assign(cell, { code: nextSlotCode(rest, prefix), slotType: 'Standard', maxVehicleType: 'Suv' });
  return { ...rest, cells: [...rest.cells, cell] };
};

export const replaceCell = (model: LayoutModel, old: LayoutCellDto, next: LayoutCellDto): LayoutModel => ({
  ...model,
  cells: model.cells.map((c) => (c === old ? next : c)),
});

export const removeCells = (model: LayoutModel, cells: LayoutCellDto[]): LayoutModel =>
  cells.length ? { ...model, cells: model.cells.filter((c) => !cells.includes(c)) } : model;

export interface Point {
  x: number;
  y: number;
}

/** Mọi vị trí trên đoạn thẳng a → b: kéo chuột nhanh vẫn tô liền nét. */
export const lineBetween = (a: Point, b: Point): Point[] => {
  const steps = Math.max(Math.abs(b.x - a.x), Math.abs(b.y - a.y));
  return Array.from({ length: steps + 1 }, (_, i) => ({
    x: Math.round(a.x + ((b.x - a.x) * i) / (steps || 1)),
    y: Math.round(a.y + ((b.y - a.y) * i) / (steps || 1)),
  }));
};

/** Hình chữ nhật (tính cả hai góc) giữa hai vị trí. */
export const rectOf = (a: Point, b: Point) => ({
  x: Math.min(a.x, b.x),
  y: Math.min(a.y, b.y),
  width: Math.abs(a.x - b.x) + 1,
  height: Math.abs(a.y - b.y) + 1,
});

export const cellsInRect = (model: LayoutModel, a: Point, b: Point) => {
  const r = rectOf(a, b);
  return model.cells.filter(
    (c) => c.x < r.x + r.width && c.x + c.widthCells > r.x && c.y < r.y + r.height && c.y + c.heightCells > r.y
  );
};

/** Tô kín hình chữ nhật theo thứ tự đọc (mã ô mới tăng dần). */
export const fillRect = (model: LayoutModel, tool: PaintType | 'eraser', a: Point, b: Point, prefix: string): LayoutModel => {
  const r = rectOf(a, b);
  let next = model;
  for (let y = r.y; y < r.y + r.height; y += 1)
    for (let x = r.x; x < r.x + r.width; x += 1) next = paint(next, tool, x, y, prefix);
  return next;
};

/** Dời cả nhóm ô, giữ nguyên mã. null = ra ngoài lưới hoặc đè lên ô không nằm trong nhóm. */
export const moveCells = (model: LayoutModel, cells: LayoutCellDto[], dx: number, dy: number) => {
  const moved = cells.map((c) => ({ ...c, x: c.x + dx, y: c.y + dy }));
  const others = model.cells.filter((c) => !cells.includes(c));
  const blocked = moved.some(
    (m) =>
      m.x < 0 ||
      m.y < 0 ||
      m.x + m.widthCells > model.columns ||
      m.y + m.heightCells > model.rows ||
      cellsInRect({ ...model, cells: others }, m, { x: m.x + m.widthCells - 1, y: m.y + m.heightCells - 1 }).length > 0
  );
  return blocked ? null : { model: { ...model, cells: [...others, ...moved] }, moved };
};

export type Side = 'top' | 'bottom' | 'left' | 'right';
const axisOf = (side: Side) => (side === 'top' || side === 'bottom' ? 'rows' : 'columns');

/** Bỏ được hàng / cột ở một cạnh khi không còn ô nào chạm vào đó (không tự xóa dữ liệu). */
export const canRemoveEdge = (model: LayoutModel, side: Side) =>
  model[axisOf(side)] > 1 &&
  !model.cells.some((c) => {
    if (side === 'top') return c.y === 0;
    if (side === 'left') return c.x === 0;
    return side === 'bottom' ? c.y + c.heightCells >= model.rows : c.x + c.widthCells >= model.columns;
  });

/** Thêm (+1) hoặc bỏ (-1) một hàng / cột ở cạnh bất kỳ; cạnh trên / trái dời mọi ô theo. */
export const resizeEdge = (model: LayoutModel, side: Side, delta: 1 | -1): LayoutModel => {
  const axis = axisOf(side);
  if (delta === -1 ? !canRemoveEdge(model, side) : model[axis] >= MAX_GRID) return model;
  const dx = side === 'left' ? delta : 0;
  const dy = side === 'top' ? delta : 0;
  return {
    ...model,
    [axis]: model[axis] + delta,
    cells: dx || dy ? model.cells.map((c) => ({ ...c, x: c.x + dx, y: c.y + dy })) : model.cells,
  };
};

const inUseCodes = (model: LayoutModel) =>
  new Set(model.cells.filter((c) => isSlot(c) && (c.state === 'Occupied' || c.state === 'Reserved')).map(slotCode));

/**
 * Đánh lại mã ô theo thứ tự đọc (trên → dưới, trái → phải). Ô đang có xe / giữ chỗ ở bản đang áp dụng giữ mã cũ,
 * các mã đó được bỏ qua khi đánh số.
 */
export const renumberSlots = (model: LayoutModel, published: LayoutModel, prefix: string): LayoutModel => {
  const kept = inUseCodes(published);
  const p = prefix.trim().toUpperCase();
  const targets = model.cells.filter((c) => isSlot(c) && !kept.has(slotCode(c))).sort((a, b) => a.y - b.y || a.x - b.x);
  const pad = Math.max(2, String(model.cells.filter(isSlot).length).length);
  const codes = new Map<LayoutCellDto, string>();
  let n = 0;
  for (const c of targets) {
    let code: string;
    do {
      n += 1;
      code = `${p}${String(n).padStart(pad, '0')}`;
    } while (kept.has(code));
    codes.set(c, code);
  }
  return { ...model, cells: model.cells.map((c) => (codes.has(c) ? { ...c, code: codes.get(c) } : c)) };
};

export const MIN_STANDARD_SIZE = 5;
const PILLAR_EVERY = 6;

/**
 * Bố cục chuẩn trong lưới hiện tại: tường bao, cổng vào bên trái, cổng ra bên phải, hai lối dọc nối các lối ngang,
 * giữa hai lối ngang là hai dãy ô quay lưng vào nhau, cứ vài ô có một cột. null = lưới quá nhỏ.
 */
export const standardLayout = (columns: number, rows: number, prefix: string): LayoutModel | null => {
  if (columns < MIN_STANDARD_SIZE || rows < MIN_STANDARD_SIZE) return null;
  const aisles = new Set([1]);
  for (let y = 1; y < rows - 2; ) {
    y = Math.min(y + 3, rows - 2);
    aisles.add(y);
  }
  const lastAisle = rows - 2;
  let model: LayoutModel = { columns, rows, cells: [] };
  const put = (type: PaintType, x: number, y: number) => {
    model = paint(model, type, x, y, prefix);
  };
  for (let y = 0; y < rows; y += 1) {
    for (let x = 0; x < columns; x += 1) {
      if (x === 0 && y === 1) put('entry', x, y);
      else if (x === columns - 1 && y === lastAisle) put('exit', x, y);
      else if (x === 0 || y === 0 || x === columns - 1 || y === rows - 1) put('wall', x, y);
      else if (aisles.has(y) || x === 1 || x === columns - 2) put('road', x, y);
      else put((x - 1) % PILLAR_EVERY === 0 && x < columns - 3 ? 'pillar' : 'slot', x, y);
    }
  }
  // Đánh số lại từ 1 theo thứ tự đọc với độ dài mã thống nhất.
  return renumberSlots(model, { columns, rows, cells: [] }, prefix);
};

/** Cạnh của ô quay ra lối đi (ưu tiên dưới, trên, trái, phải); không kề đường thì mặc định cạnh dưới. */
export const openSide = (model: LayoutModel, c: LayoutCellDto): Side => {
  const drivable = (x: number, y: number) => DRIVABLE.has(cellAt(model, x, y)?.type ?? '');
  const across = Array.from({ length: c.widthCells }, (_, i) => c.x + i);
  const along = Array.from({ length: c.heightCells }, (_, i) => c.y + i);
  if (across.some((x) => drivable(x, c.y + c.heightCells))) return 'bottom';
  if (across.some((x) => drivable(x, c.y - 1))) return 'top';
  if (along.some((y) => drivable(c.x - 1, y))) return 'left';
  if (along.some((y) => drivable(c.x + c.widthCells, y))) return 'right';
  return 'bottom';
};

/** Lịch sử undo / redo. Các thay đổi cùng một "nét" (kéo chuột, gõ một ô nhập) gộp thành một bước. */
export interface History {
  past: LayoutModel[];
  present: LayoutModel;
  future: LayoutModel[];
  stroke: string | null;
}

const HISTORY_LIMIT = 100;

export const startHistory = (model: LayoutModel): History => ({ past: [], present: model, future: [], stroke: null });

export const commit = (h: History, next: LayoutModel, stroke: string | null = null): History => {
  if (next === h.present) return h;
  if (stroke !== null && stroke === h.stroke) return { ...h, present: next };
  return { past: [...h.past, h.present].slice(-HISTORY_LIMIT), present: next, future: [], stroke };
};

export const undo = (h: History): History =>
  h.past.length
    ? { past: h.past.slice(0, -1), present: h.past[h.past.length - 1], future: [h.present, ...h.future], stroke: null }
    : h;

export const redo = (h: History): History =>
  h.future.length ? { past: [...h.past, h.present], present: h.future[0], future: h.future.slice(1), stroke: null } : h;

export interface LayoutDiff {
  added: string[];
  removed: string[];
  moved: string[];
  retyped: string[];
}

/** So ô đỗ theo mã với bản đang áp dụng. */
export const diffSlots = (published: LayoutModel, draft: LayoutModel): LayoutDiff => {
  const byCode = (m: LayoutModel) => new Map(m.cells.filter((c) => isSlot(c) && slotCode(c)).map((c) => [slotCode(c), c]));
  const before = byCode(published);
  const after = byCode(draft);
  const diff: LayoutDiff = { added: [], removed: [], moved: [], retyped: [] };
  for (const [code, c] of after) {
    const old = before.get(code);
    if (!old) diff.added.push(code);
    else {
      if (old.x !== c.x || old.y !== c.y || old.widthCells !== c.widthCells || old.heightCells !== c.heightCells) diff.moved.push(code);
      if ((old.slotType ?? null) !== (c.slotType ?? null) || (old.maxVehicleType ?? null) !== (c.maxVehicleType ?? null))
        diff.retyped.push(code);
    }
  }
  diff.removed = [...before.keys()].filter((code) => !after.has(code));
  return diff;
};

const listed = (items: string[]) => (items.length > 5 ? `${items.slice(0, 5).join(', ')} and ${items.length - 5} more` : items.join(', '));

/** errors chặn lưu / xuất bản (cùng quy tắc ParkingService); warnings chỉ nhắc. */
export const validateLayout = (model: LayoutModel, published: LayoutModel): { errors: string[]; warnings: string[] } => {
  const errors: string[] = [];
  const warnings: string[] = [];
  const name = (c: LayoutCellDto) => slotCode(c) || `${c.type} at column ${c.x + 1}, row ${c.y + 1}`;

  const outside = model.cells.filter(
    (c) => c.x < 0 || c.y < 0 || c.x + c.widthCells > model.columns || c.y + c.heightCells > model.rows
  );
  if (outside.length) errors.push(`Outside the grid: ${listed(outside.map(name))}.`);

  const taken = new Map<string, LayoutCellDto>();
  const overlaps = new Set<string>();
  for (const c of model.cells) {
    for (let dx = 0; dx < c.widthCells; dx += 1) {
      for (let dy = 0; dy < c.heightCells; dy += 1) {
        const key = `${c.x + dx},${c.y + dy}`;
        const other = taken.get(key);
        if (other) overlaps.add(`${name(other)} and ${name(c)}`);
        else taken.set(key, c);
      }
    }
  }
  if (overlaps.size) errors.push(`Overlapping cells: ${listed([...overlaps])}.`);

  const slots = model.cells.filter(isSlot);
  const noCode = slots.filter((c) => !slotCode(c));
  if (noCode.length) errors.push(`${noCode.length} slot(s) have no code.`);
  const seen = new Set<string>();
  const duplicates = new Set<string>();
  for (const code of slots.map(slotCode).filter(Boolean)) {
    if (seen.has(code)) duplicates.add(code);
    seen.add(code);
  }
  if (duplicates.size) errors.push(`Duplicate slot codes: ${listed([...duplicates])}.`);

  const inUse = published.cells.filter(
    (c) => isSlot(c) && (c.state === 'Occupied' || c.state === 'Reserved') && !seen.has(slotCode(c))
  );
  if (inUse.length) errors.push(`Occupied or reserved slots cannot be removed or renamed: ${listed(inUse.map(slotCode))}.`);

  const drivable = (x: number, y: number) => DRIVABLE.has(taken.get(`${x},${y}`)?.type ?? '');
  const unreachable = slots.filter((c) => {
    for (let dx = 0; dx < c.widthCells; dx += 1)
      if (drivable(c.x + dx, c.y - 1) || drivable(c.x + dx, c.y + c.heightCells)) return false;
    for (let dy = 0; dy < c.heightCells; dy += 1)
      if (drivable(c.x - 1, c.y + dy) || drivable(c.x + c.widthCells, c.y + dy)) return false;
    return true;
  });
  if (unreachable.length) warnings.push(`${unreachable.length} slot(s) do not touch a road: ${listed(unreachable.map(name))}.`);
  if (!model.cells.some((c) => c.type === 'entry' || c.type === 'ramp')) warnings.push('No entry or ramp on this floor.');
  return { errors, warnings };
};

/**
 * Payload gửi ParkingService: đủ mọi ô lưới theo thứ tự từng hàng, ô trống ghi "empty".
 * shortcut: có màn đang vẽ sơ đồ theo thứ tự mảng thay vì x/y; bỏ phần lấp ô trống khi mọi màn đọc x/y.
 */
export const toCells = (model: LayoutModel): LayoutCellInputDto[] => {
  const cells: LayoutCellInputDto[] = [];
  for (let y = 0; y < model.rows; y += 1) {
    for (let x = 0; x < model.columns; x += 1) {
      const c = cellAt(model, x, y);
      if (!c) cells.push({ x, y, type: 'empty', widthCells: 1, heightCells: 1 });
      else if (c.x === x && c.y === y) {
        cells.push({
          x,
          y,
          type: c.type,
          code: isSlot(c) ? slotCode(c) : null,
          slotType: isSlot(c) ? (c.slotType ?? null) : null,
          maxVehicleType: isSlot(c) ? (c.maxVehicleType ?? null) : null,
          widthCells: c.widthCells,
          heightCells: c.heightCells,
        });
      }
    }
  }
  return cells;
};
