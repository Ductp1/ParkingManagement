import type { FloorLayoutDto, LayoutCellDto } from '@/types/parking';

export interface GridPoint {
  x: number;
  y: number;
}

export interface DynamicFloorData {
  floorId: number;
  floorName: string;
  level: number;
  layout: FloorLayoutDto;
}

export interface DynamicRoutePoint {
  /** Tọa độ lưới (có thể là số lẻ ở tâm ô nhiều cell). */
  gx: number;
  gy: number;
  /** Chỉ số tầng hiển thị (0 = tầng thấp nhất trong danh sách). */
  floorIndex: number;
}

export interface DynamicRouteResult {
  points: DynamicRoutePoint[];
  targetFloorIndex: number;
  targetCell: LayoutCellDto | null;
  entryFloorIndex: number;
  entryCell: LayoutCellDto | null;
  /** Số bước A* đã duyệt (để kiểm chứng/hiển thị nếu cần). */
  exploredNodes: number;
  usedFallback: boolean;
}

const WALKABLE_TYPES = new Set(['road', 'entry', 'exit', 'ramp']);
const DIRS: readonly GridPoint[] = [
  { x: 1, y: 0 },
  { x: -1, y: 0 },
  { x: 0, y: 1 },
  { x: 0, y: -1 },
];

interface HeapNode {
  key: number;
  x: number;
  y: number;
  dir: number; // -1 = start, 0..3 = DIRS index
  g: number;
  f: number;
}

/** Min-heap nhị phân cho A* để đạt độ phức tạp O(E log V). */
class MinHeap {
  private data: HeapNode[] = [];

  get size() {
    return this.data.length;
  }

  push(node: HeapNode) {
    this.data.push(node);
    this.bubbleUp(this.data.length - 1);
  }

  pop(): HeapNode | undefined {
    const top = this.data[0];
    const last = this.data.pop();
    if (this.data.length > 0 && last) {
      this.data[0] = last;
      this.sinkDown(0);
    }
    return top;
  }

  private bubbleUp(i: number) {
    while (i > 0) {
      const p = (i - 1) >> 1;
      if (this.data[p].f <= this.data[i].f) break;
      [this.data[p], this.data[i]] = [this.data[i], this.data[p]];
      i = p;
    }
  }

  private sinkDown(i: number) {
    const n = this.data.length;
    while (true) {
      let smallest = i;
      const l = 2 * i + 1;
      const r = 2 * i + 2;
      if (l < n && this.data[l].f < this.data[smallest].f) smallest = l;
      if (r < n && this.data[r].f < this.data[smallest].f) smallest = r;
      if (smallest === i) break;
      [this.data[i], this.data[smallest]] = [this.data[smallest], this.data[i]];
      i = smallest;
    }
  }
}

/** Xây bảng tra cứu loại ô trên lưới 2D (hỗ trợ ô có widthCells/heightCells > 1). */
export function buildWalkableGrid(layout: FloorLayoutDto): {
  cols: number;
  rows: number;
  walkable: Uint8Array;
} {
  const cols = Math.max(1, layout.columns);
  const rows = Math.max(1, layout.rows);
  const walkable = new Uint8Array(cols * rows);

  for (const cell of layout.cells) {
    const type = (cell.type || '').toLowerCase();
    if (!WALKABLE_TYPES.has(type)) continue;
    const w = Math.max(1, cell.widthCells || 1);
    const h = Math.max(1, cell.heightCells || 1);
    for (let dy = 0; dy < h; dy++) {
      for (let dx = 0; dx < w; dx++) {
        const cx = cell.x + dx;
        const cy = cell.y + dy;
        if (cx >= 0 && cx < cols && cy >= 0 && cy < rows) {
          walkable[cy * cols + cx] = 1;
        }
      }
    }
  }

  return { cols, rows, walkable };
}

/**
 * Tìm đường đi ngắn nhất bằng thuật toán A* trên lưới 2D.
 * Heuristic: khoảng cách Manhattan tới tập ô đích gần nhất + phạt nhẹ khi rẽ để đường đi mượt.
 */
export function findPathAStar(
  cols: number,
  rows: number,
  walkable: Uint8Array,
  starts: GridPoint[],
  goals: GridPoint[],
): { path: GridPoint[]; exploredNodes: number } | null {
  if (starts.length === 0 || goals.length === 0) return null;

  const goalSet = new Set<number>(goals.map((g) => g.y * cols + g.x));
  const total = cols * rows;
  const gScore = new Float64Array(total).fill(Infinity);
  const parent = new Int32Array(total).fill(-1);
  const closed = new Uint8Array(total);
  const heap = new MinHeap();

  const heuristic = (x: number, y: number) => {
    let best = Infinity;
    for (const g of goals) {
      const d = Math.abs(x - g.x) + Math.abs(y - g.y);
      if (d < best) best = d;
    }
    return best;
  };

  for (const s of starts) {
    if (s.x < 0 || s.x >= cols || s.y < 0 || s.y >= rows) continue;
    const key = s.y * cols + s.x;
    gScore[key] = 0;
    heap.push({ key, x: s.x, y: s.y, dir: -1, g: 0, f: heuristic(s.x, s.y) });
  }

  let exploredNodes = 0;

  while (heap.size > 0) {
    const cur = heap.pop()!;
    if (closed[cur.key]) continue;
    closed[cur.key] = 1;
    exploredNodes++;

    if (goalSet.has(cur.key)) {
      const path: GridPoint[] = [];
      let k = cur.key;
      while (k !== -1) {
        path.push({ x: k % cols, y: Math.floor(k / cols) });
        k = parent[k];
      }
      path.reverse();
      return { path, exploredNodes };
    }

    for (let d = 0; d < DIRS.length; d++) {
      const nx = cur.x + DIRS[d].x;
      const ny = cur.y + DIRS[d].y;
      if (nx < 0 || nx >= cols || ny < 0 || ny >= rows) continue;
      const nKey = ny * cols + nx;
      if (closed[nKey]) continue;
      if (!walkable[nKey] && !goalSet.has(nKey)) continue;

      const turnPenalty = cur.dir !== -1 && cur.dir !== d ? 0.15 : 0;
      const tentativeG = cur.g + 1 + turnPenalty;
      if (tentativeG < gScore[nKey]) {
        gScore[nKey] = tentativeG;
        parent[nKey] = cur.key;
        heap.push({
          key: nKey,
          x: nx,
          y: ny,
          dir: d,
          g: tentativeG,
          f: tentativeG + heuristic(nx, ny),
        });
      }
    }
  }

  return null;
}

/** Rút gọn các điểm thẳng hàng liên tiếp để đường cong 3D mượt hơn. */
export function simplifyGridPath(path: GridPoint[]): GridPoint[] {
  if (path.length <= 2) return path;
  const out: GridPoint[] = [path[0]];
  for (let i = 1; i < path.length - 1; i++) {
    const prev = out[out.length - 1];
    const curr = path[i];
    const next = path[i + 1];
    const dx1 = Math.sign(curr.x - prev.x);
    const dy1 = Math.sign(curr.y - prev.y);
    const dx2 = Math.sign(next.x - curr.x);
    const dy2 = Math.sign(next.y - curr.y);
    if (dx1 !== dx2 || dy1 !== dy2) {
      out.push(curr);
    }
  }
  out.push(path[path.length - 1]);
  return out;
}

/** Tìm các ô đường đi ('road' / 'entry' / 'exit' / 'ramp') tiếp giáp với 1 cell. */
function adjacentWalkable(cell: LayoutCellDto, cols: number, rows: number, walkable: Uint8Array): GridPoint[] {
  const w = Math.max(1, cell.widthCells || 1);
  const h = Math.max(1, cell.heightCells || 1);
  const result: GridPoint[] = [];
  const seen = new Set<number>();

  for (let dy = 0; dy < h; dy++) {
    for (let dx = 0; dx < w; dx++) {
      const cx = cell.x + dx;
      const cy = cell.y + dy;
      for (const dir of DIRS) {
        const nx = cx + dir.x;
        const ny = cy + dir.y;
        if (nx < 0 || nx >= cols || ny < 0 || ny >= rows) continue;
        const k = ny * cols + nx;
        if (walkable[k] && !seen.has(k)) {
          seen.add(k);
          result.push({ x: nx, y: ny });
        }
      }
    }
  }
  return result;
}

function cellCenter(cell: LayoutCellDto): { gx: number; gy: number } {
  const w = Math.max(1, cell.widthCells || 1);
  const h = Math.max(1, cell.heightCells || 1);
  return { gx: cell.x + (w - 1) / 2, gy: cell.y + (h - 1) / 2 };
}

function findEntryCells(layout: FloorLayoutDto): LayoutCellDto[] {
  const entries = layout.cells.filter((c) => c.type?.toLowerCase() === 'entry');
  if (entries.length > 0) return entries;
  const roads = layout.cells.filter((c) => c.type?.toLowerCase() === 'road');
  return roads.length > 0 ? [roads[0]] : [];
}

function findRampOrConnector(layout: FloorLayoutDto): GridPoint | null {
  const ramp = layout.cells.find((c) => c.type?.toLowerCase() === 'ramp');
  if (ramp) return { x: ramp.x, y: ramp.y };
  const roads = layout.cells.filter((c) => WALKABLE_TYPES.has((c.type || '').toLowerCase()));
  return roads.length > 0 ? { x: roads[roads.length - 1].x, y: roads[roads.length - 1].y } : null;
}

/**
 * Tính đường đi 3D hoàn chỉnh bằng A* trên dữ liệu lưới 2D từ API.
 * Hỗ trợ cả 1 tầng lẫn nhiều tầng (đi qua ram/điểm chuyển tầng).
 */
export function buildDynamicRoute(
  floors: DynamicFloorData[],
  slotCode: string,
  floorHint?: string | null,
): DynamicRouteResult | null {
  if (floors.length === 0) return null;

  const normalizedSlot = slotCode.trim().toUpperCase();
  const normalizedHint = floorHint?.trim().toUpperCase();

  let targetFloorIndex = -1;
  let targetCell: LayoutCellDto | null = null;

  for (let i = 0; i < floors.length; i++) {
    const found = floors[i].layout.cells.find(
      (c) => c.type?.toLowerCase() === 'slot' && c.code?.trim().toUpperCase() === normalizedSlot,
    );
    if (found) {
      targetFloorIndex = i;
      targetCell = found;
      break;
    }
  }

  if (targetFloorIndex === -1 && normalizedHint) {
    targetFloorIndex = floors.findIndex((f) => f.floorName.trim().toUpperCase() === normalizedHint);
  }
  if (targetFloorIndex === -1) return null;

  // Chọn tầng cổng vào: ưu tiên tầng có ô 'entry' và gần mặt đất (level = 0 hoặc |level| nhỏ nhất).
  let entryFloorIndex = floors.findIndex((f) => f.layout.cells.some((c) => c.type?.toLowerCase() === 'entry'));
  if (entryFloorIndex === -1) {
    entryFloorIndex = 0;
  }

  const entryFloor = floors[entryFloorIndex];
  const entryCells = findEntryCells(entryFloor.layout);
  const entryCell = entryCells[0] ?? null;

  const targetFloor = floors[targetFloorIndex];
  const targetGrid = buildWalkableGrid(targetFloor.layout);
  const targetCenter = targetCell
    ? cellCenter(targetCell)
    : { gx: Math.floor(targetGrid.cols / 2), gy: Math.floor(targetGrid.rows / 2) };

  // Trường hợp cùng tầng (cổng vào và ô đỗ nằm trên 1 tầng)
  if (entryFloorIndex === targetFloorIndex) {
    const starts = entryCells.map((c) => ({ x: c.x, y: c.y }));
    const goals = targetCell
      ? adjacentWalkable(targetCell, targetGrid.cols, targetGrid.rows, targetGrid.walkable)
      : [{ x: Math.round(targetCenter.gx), y: Math.round(targetCenter.gy) }];

    const astar = findPathAStar(targetGrid.cols, targetGrid.rows, targetGrid.walkable, starts, goals);
    if (astar && astar.path.length > 0) {
      const simplified = simplifyGridPath(astar.path);
      const points: DynamicRoutePoint[] = simplified.map((p) => ({
        gx: p.x,
        gy: p.y,
        floorIndex: targetFloorIndex,
      }));
      if (targetCell) {
        points.push({ gx: targetCenter.gx, gy: targetCenter.gy, floorIndex: targetFloorIndex });
      }
      return {
        points,
        targetFloorIndex,
        targetCell,
        entryFloorIndex,
        entryCell,
        exploredNodes: astar.exploredNodes,
        usedFallback: false,
      };
    }

    // Fallback nếu chủ bãi chưa vẽ nối thông ô 'road' tới ô đỗ
    const startPt = starts[0] ?? { x: 0, y: 0 };
    return {
      points: [
        { gx: startPt.x, gy: startPt.y, floorIndex: targetFloorIndex },
        { gx: targetCenter.gx, gy: targetCenter.gy, floorIndex: targetFloorIndex },
      ],
      targetFloorIndex,
      targetCell,
      entryFloorIndex,
      entryCell,
      exploredNodes: 0,
      usedFallback: true,
    };
  }

  // Trường hợp khác tầng: A* từ cổng vào tới điểm chuyển tầng -> lên/xuống tầng -> A* tới ô đỗ
  const entryGrid = buildWalkableGrid(entryFloor.layout);
  const connectorEntry = findRampOrConnector(entryFloor.layout);
  const connectorTarget = findRampOrConnector(targetFloor.layout);
  const starts = entryCells.map((c) => ({ x: c.x, y: c.y }));

  let totalExplored = 0;
  let usedFallback = false;
  const points: DynamicRoutePoint[] = [];

  if (connectorEntry) {
    const leg1 = findPathAStar(entryGrid.cols, entryGrid.rows, entryGrid.walkable, starts, [connectorEntry]);
    if (leg1) {
      totalExplored += leg1.exploredNodes;
      for (const p of simplifyGridPath(leg1.path)) {
        points.push({ gx: p.x, gy: p.y, floorIndex: entryFloorIndex });
      }
    } else {
      usedFallback = true;
      const s = starts[0] ?? connectorEntry;
      points.push({ gx: s.x, gy: s.y, floorIndex: entryFloorIndex });
      points.push({ gx: connectorEntry.x, gy: connectorEntry.y, floorIndex: entryFloorIndex });
    }
  }

  const goals = targetCell
    ? adjacentWalkable(targetCell, targetGrid.cols, targetGrid.rows, targetGrid.walkable)
    : [{ x: Math.round(targetCenter.gx), y: Math.round(targetCenter.gy) }];

  if (connectorTarget) {
    const leg2 = findPathAStar(targetGrid.cols, targetGrid.rows, targetGrid.walkable, [connectorTarget], goals);
    if (leg2) {
      totalExplored += leg2.exploredNodes;
      for (const p of simplifyGridPath(leg2.path)) {
        points.push({ gx: p.x, gy: p.y, floorIndex: targetFloorIndex });
      }
    } else {
      usedFallback = true;
      points.push({ gx: connectorTarget.x, gy: connectorTarget.y, floorIndex: targetFloorIndex });
    }
  }

  if (targetCell) {
    points.push({ gx: targetCenter.gx, gy: targetCenter.gy, floorIndex: targetFloorIndex });
  }

  return {
    points,
    targetFloorIndex,
    targetCell,
    entryFloorIndex,
    entryCell,
    exploredNodes: totalExplored,
    usedFallback,
  };
}
