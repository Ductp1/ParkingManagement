import type { LayoutCellDto } from '@/types/ownerLayout';
import { DRIVABLE, LAYOUT_COLORS, colorKeyOf, isSlot, openSide, slotCode, type LayoutModel, type Side } from './layoutModel';

/** Mặt sàn vẽ bằng canvas: nhựa đường, vạch ô đỗ, mã ô, vạch lối đi, mũi tên chiều chạy, cổng. */
const MAX_TEXTURE = 4096;
const MAX_CELL_PIXELS = 128;

const PAINT = '#f1f5f9';
const LANE_PAINT = '#fbbf24';

const key = (x: number, y: number) => `${x},${y}`;
const STEPS: Array<[number, number]> = [[1, 0], [-1, 0], [0, 1], [0, -1]];

/** Số bước từ cổng vào đến từng ô xe chạy được – mũi tên chỉ theo hướng xa cổng dần. */
export const distanceFromEntry = (model: LayoutModel): Map<string, number> => {
  const drivable = new Set<string>();
  const queue: Array<[number, number]> = [];
  const distance = new Map<string, number>();
  for (const c of model.cells.filter((c) => DRIVABLE.has(c.type))) {
    for (let x = c.x; x < c.x + c.widthCells; x += 1) {
      for (let y = c.y; y < c.y + c.heightCells; y += 1) {
        drivable.add(key(x, y));
        if (c.type === 'entry') {
          distance.set(key(x, y), 0);
          queue.push([x, y]);
        }
      }
    }
  }
  for (let i = 0; i < queue.length; i += 1) {
    const [x, y] = queue[i];
    for (const [dx, dy] of STEPS) {
      const next = key(x + dx, y + dy);
      if (!drivable.has(next) || distance.has(next)) continue;
      distance.set(next, distance.get(key(x, y))! + 1);
      queue.push([x + dx, y + dy]);
    }
  }
  return distance;
};

const TINT: Partial<Record<string, string>> = {
  Occupied: 'rgba(220,38,38,0.30)',
  Reserved: 'rgba(245,158,11,0.45)',
  Maintenance: 'rgba(100,116,139,0.45)',
  Unavailable: 'rgba(148,163,184,0.45)',
  EvCharging: 'rgba(8,145,178,0.45)',
};

/** Hạt nhựa đường: vị trí cố định theo chỉ số nên mỗi lần dựng lại trông như nhau. */
const grain = (ctx: CanvasRenderingContext2D, width: number, height: number) => {
  const count = Math.min(12000, Math.round((width * height) / 220));
  for (let i = 0; i < count; i += 1) {
    ctx.fillStyle = i % 2 ? 'rgba(255,255,255,0.05)' : 'rgba(0,0,0,0.14)';
    ctx.fillRect((i * 7919) % width, (i * 104729 + (i % 17) * 31) % height, 2, 2);
  }
};

const slotPaint = (ctx: CanvasRenderingContext2D, c: LayoutCellDto, side: Side, px: number, showState: boolean) => {
  const x = c.x * px;
  const y = c.y * px;
  const w = c.widthCells * px;
  const h = c.heightCells * px;
  const colour = colorKeyOf(c, showState);
  const tint = TINT[colour] ?? (c.slotType === 'Disabled' ? 'rgba(37,99,235,0.5)' : null);
  if (tint) {
    ctx.fillStyle = tint;
    ctx.fillRect(x, y, w, h);
  }
  if (colour === 'Maintenance') {
    ctx.save();
    ctx.beginPath();
    ctx.rect(x, y, w, h);
    ctx.clip();
    ctx.strokeStyle = 'rgba(250,204,21,0.8)';
    ctx.lineWidth = px * 0.08;
    for (let o = -h; o < w; o += px * 0.28) {
      ctx.beginPath();
      ctx.moveTo(x + o, y + h);
      ctx.lineTo(x + o + h, y);
      ctx.stroke();
    }
    ctx.restore();
  }

  // Vạch trắng ba cạnh, để hở cạnh quay ra lối đi.
  const t = Math.max(2, px * 0.045);
  ctx.fillStyle = PAINT;
  if (side !== 'top') ctx.fillRect(x, y, w, t);
  if (side !== 'bottom') ctx.fillRect(x, y + h - t, w, t);
  if (side !== 'left') ctx.fillRect(x, y, t, h);
  if (side !== 'right') ctx.fillRect(x + w - t, y, t, h);

  // Mã ô sơn gần đầu ô phía lối đi để không bị xe che.
  const inset = px * 0.16;
  const at = {
    bottom: [x + w / 2, y + h - inset],
    top: [x + w / 2, y + inset],
    left: [x + inset, y + h / 2],
    right: [x + w - inset, y + h / 2],
  }[side];
  ctx.save();
  ctx.translate(at[0], at[1]);
  if (side === 'left') ctx.rotate(Math.PI / 2);
  if (side === 'right') ctx.rotate(-Math.PI / 2);
  ctx.fillStyle = PAINT;
  ctx.font = `bold ${Math.round(px * 0.2)}px sans-serif`;
  ctx.fillText(slotCode(c), 0, 0, px * 0.9);
  ctx.restore();

  if (c.slotType === 'Disabled' || c.slotType === 'EvCharging') {
    ctx.fillStyle = PAINT;
    ctx.font = `${Math.round(px * 0.36)}px sans-serif`;
    ctx.fillText(c.slotType === 'Disabled' ? '♿' : '⚡', x + w / 2, y + h / 2);
  }
};

const lanePaint = (ctx: CanvasRenderingContext2D, model: LayoutModel, px: number) => {
  const drivable = new Set(
    model.cells.filter((c) => DRIVABLE.has(c.type)).map((c) => key(c.x, c.y))
  );
  const distance = distanceFromEntry(model);
  for (const c of model.cells.filter((cell) => cell.type === 'road')) {
    const [left, right, up, down] = [
      drivable.has(key(c.x - 1, c.y)),
      drivable.has(key(c.x + 1, c.y)),
      drivable.has(key(c.x, c.y - 1)),
      drivable.has(key(c.x, c.y + 1)),
    ];
    const horizontal = (left || right) && !up && !down;
    const vertical = (up || down) && !left && !right;
    if (!horizontal && !vertical) continue;
    const cx = (c.x + 0.5) * px;
    const cy = (c.y + 0.5) * px;
    ctx.save();
    ctx.translate(cx, cy);
    if (vertical) ctx.rotate(Math.PI / 2);

    // Cứ ba ô có một mũi tên, quay theo hướng đi xa cổng vào; các ô còn lại là vạch đứt.
    const here = distance.get(key(c.x, c.y));
    const ahead = distance.get(vertical ? key(c.x, c.y + 1) : key(c.x + 1, c.y));
    const behind = distance.get(vertical ? key(c.x, c.y - 1) : key(c.x - 1, c.y));
    const forward = here !== undefined && ((ahead ?? -1) > here || (behind ?? Infinity) < here);
    const backward = here !== undefined && ((behind ?? -1) > here || (ahead ?? Infinity) < here);
    if ((c.x + c.y) % 3 === 0 && forward !== backward) {
      if (backward) ctx.rotate(Math.PI);
      ctx.fillStyle = PAINT;
      ctx.beginPath();
      ctx.moveTo(px * 0.28, 0);
      ctx.lineTo(px * 0.04, -px * 0.17);
      ctx.lineTo(px * 0.04, -px * 0.06);
      ctx.lineTo(-px * 0.28, -px * 0.06);
      ctx.lineTo(-px * 0.28, px * 0.06);
      ctx.lineTo(px * 0.04, px * 0.06);
      ctx.lineTo(px * 0.04, px * 0.17);
      ctx.closePath();
      ctx.fill();
    } else {
      ctx.fillStyle = LANE_PAINT;
      ctx.fillRect(-px * 0.25, -px * 0.025, px * 0.5, px * 0.05);
    }
    ctx.restore();
  }
};

/** null khi trình duyệt không vẽ được canvas 2D. */
export const paintFloor = (model: LayoutModel, showState: boolean): HTMLCanvasElement | null => {
  const px = Math.min(MAX_CELL_PIXELS, Math.floor(MAX_TEXTURE / Math.max(model.columns, model.rows)));
  const canvas = document.createElement('canvas');
  canvas.width = model.columns * px;
  canvas.height = model.rows * px;
  const ctx = canvas.getContext('2d');
  if (!ctx) return null;
  ctx.textAlign = 'center';
  ctx.textBaseline = 'middle';
  ctx.fillStyle = '#454b55';
  ctx.fillRect(0, 0, canvas.width, canvas.height);
  for (const c of model.cells) {
    if (DRIVABLE.has(c.type)) {
      ctx.fillStyle = '#535a66';
      ctx.fillRect(c.x * px, c.y * px, c.widthCells * px, c.heightCells * px);
    }
  }
  grain(ctx, canvas.width, canvas.height);
  lanePaint(ctx, model, px);
  for (const c of model.cells) {
    if (isSlot(c)) slotPaint(ctx, c, openSide(model, c), px, showState);
    else if (c.type === 'entry' || c.type === 'exit') {
      ctx.fillStyle = LAYOUT_COLORS[c.type];
      ctx.fillRect(c.x * px + px * 0.08, c.y * px + px * 0.08, c.widthCells * px - px * 0.16, c.heightCells * px - px * 0.16);
      ctx.fillStyle = PAINT;
      ctx.font = `bold ${Math.round(px * 0.34)}px sans-serif`;
      ctx.fillText(c.type === 'entry' ? 'IN' : 'OUT', (c.x + c.widthCells / 2) * px, (c.y + c.heightCells / 2) * px);
    }
  }
  return canvas;
};
