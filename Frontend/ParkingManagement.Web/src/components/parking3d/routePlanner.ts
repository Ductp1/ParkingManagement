import type { LayoutFloor, LayoutSlot, LotLayout, Vec2 } from './lotLayouts';

/** Điểm 3D theo hệ Blender (X đông, Y bắc, Z lên). */
export type Point3 = readonly [x: number, y: number, z: number];

export interface ResolvedTarget {
  floor: LayoutFloor;
  /** null: mã ô hợp lệ nhưng chưa có trên mô hình 3D → chỉ dẫn tới tầng. */
  slot: LayoutSlot | null;
  /** Mã ô đã chuẩn hóa (ví dụ "B1-A01"). */
  code: string;
}

/** Đường đi lơ lửng trên mặt sàn để không bị sàn che. */
export const ROUTE_LIFT = 0.12;
const RAMP_STEP_DEG = 12;

/**
 * Tìm ô trong bố cục từ mã ô của booking.
 * Nhận cả mã đầy đủ "B1-A01" lẫn dạng rút gọn "A-01" / "A01" kèm floorHint (ví dụ "B1" hoặc "Hầm B1").
 */
export function resolveTarget(layout: LotLayout, slotCode: string | null | undefined, floorHint?: string | null): ResolvedTarget | null {
  const raw = (slotCode ?? '').trim().toUpperCase().replace(/\s+/g, '');
  const match = /^(?:([A-Z]{1,2}\d{1,2})-)?([A-Z])-?(\d{1,3})$/.exec(raw);
  if (!match) return null;

  const hint = floorHint?.toUpperCase().match(/[A-Z]{1,2}\d{1,2}/)?.[0];
  const floorCode = match[1] ?? hint;
  const floor = layout.floors.find((f) => f.code === floorCode);
  if (!floor) return null;

  const code = `${floor.code}-${match[2]}${String(Number(match[3])).padStart(2, '0')}`;
  return { floor, slot: floor.slots.find((s) => s.code === code) ?? null, code };
}

const at = ([x, y]: Vec2, z: number): Point3 => [x, y, z];

/** Các điểm trên ram xoắn ốc từ sàn tầng `fromZ` lên `toZ`. */
function rampPoints(layout: LotLayout, fromZ: number, toZ: number): Point3[] {
  const { center, radius, startAngleDeg, sweepDeg } = layout.ramp;
  const points: Point3[] = [];
  const steps = Math.ceil(sweepDeg / RAMP_STEP_DEG);
  for (let i = 0; i <= steps; i++) {
    const t = i / steps;
    const angle = ((startAngleDeg + sweepDeg * t) * Math.PI) / 180;
    points.push([center[0] + radius * Math.cos(angle), center[1] + radius * Math.sin(angle), fromZ + (toZ - fromZ) * t]);
  }
  return points;
}

/** Bỏ các điểm trùng liên tiếp (đường cong Catmull-Rom không chịu được đoạn dài 0). */
function dedupe(points: Point3[]): Point3[] {
  return points.filter((p, i) => {
    const prev = points[i - 1];
    return !prev || Math.hypot(p[0] - prev[0], p[1] - prev[1], p[2] - prev[2]) > 0.05;
  });
}

/**
 * Đường từ ngoài cổng vào tới ô (hoặc tới lối đi của tầng đích nếu ô chưa có trên mô hình).
 * Tầng 1: cổng → lối đi trái / phải → trước đầu ô → tâm ô.
 * Tầng trên: cổng trái → lối đi trái → ram xoắn ốc (mỗi tầng một vòng ram) → lối đi của ô → ô.
 */
export function buildRoute(layout: LotLayout, target: ResolvedTarget): Point3[] {
  const ground = layout.floors[0];
  const floorIndex = layout.floors.indexOf(target.floor);
  const { leftX, rightX, crossY } = layout.aisles;
  const lift = (z: number) => z + ROUTE_LIFT;
  const groundZ = lift(ground.elevation);
  const slot = target.slot;
  const side = slot?.aisle ?? 'left';
  const points: Point3[] = [];

  if (floorIndex <= 0) {
    const gate = layout.gates[side];
    const aisleX = side === 'left' ? leftX : rightX;
    points.push(at(gate.outside, groundZ), at(gate.barrier, groundZ), [aisleX, gate.barrier[1] + 1, groundZ]);
    if (slot) points.push([aisleX, slot.front[1], groundZ], at(slot.front, groundZ), at(slot.center, groundZ));
    else points.push([aisleX, 0, groundZ]);
    return dedupe(points);
  }

  // Lên tầng trên: luôn vào cổng trái, đi hết lối đi trái tới chân ram.
  const gate = layout.gates.left;
  const rampStart = rampPoints(layout, 0, 0)[0];
  points.push(at(gate.outside, groundZ), at(gate.barrier, groundZ), [leftX, gate.barrier[1] + 1, groundZ],
    [rampStart[0], rampStart[1] - 3, groundZ]);
  for (let i = 1; i <= floorIndex; i++) {
    points.push(...rampPoints(layout, lift(layout.floors[i - 1].elevation), lift(layout.floors[i].elevation)));
  }

  const z = lift(target.floor.elevation);
  const exit = points[points.length - 1];
  // Ra khỏi ram theo hướng đông, nhập vào lối đi trái.
  points.push([leftX, exit[1], z]);
  if (!slot) return dedupe(points);

  if (side === 'left') {
    points.push([leftX, slot.front[1], z]);
  } else {
    points.push([leftX + 1.2, crossY, z], [rightX, crossY, z], [rightX, slot.front[1], z]);
  }
  points.push(at(slot.front, z), at(slot.center, z));
  return dedupe(points);
}

export interface ParkedCar {
  slotCode: string;
  /** Tâm ô trên mặt sàn (Blender XYZ). */
  position: Point3;
  /** Màu thân xe (hex), chọn ổn định theo mã ô để mỗi lần mở trông giống nhau. */
  color: number;
}

const CAR_COLORS = [0xf8fafc, 0x1e293b, 0x94a3b8, 0xb91c1c, 0x1d4ed8, 0xd4d4d8, 0x0f766e, 0x78350f, 0x475569, 0xe2e8f0];

/**
 * Xe đang đỗ để bãi trông như thực tế: mọi ô đã vẽ trên các tầng hiển thị (tầng đích và các tầng dưới), trừ ô tài xế đã đặt.
 */
export function parkedCars(layout: LotLayout, target: ResolvedTarget): ParkedCar[] {
  const cars: ParkedCar[] = [];
  for (const floor of layout.floors) {
    if (floor.modelFloor > target.floor.modelFloor) continue;
    for (const slot of floor.slots) {
      if (slot.code === target.slot?.code) continue;
      const hash = [...slot.code].reduce((h, ch) => (h * 31 + ch.charCodeAt(0)) >>> 0, 7);
      cars.push({ slotCode: slot.code, position: at(slot.center, floor.elevation), color: CAR_COLORS[hash % CAR_COLORS.length] });
    }
  }
  return cars;
}

/** Đổi từ hệ Blender (Z lên) sang hệ three.js (Y lên) – giống trình xuất glTF của Blender. */
export const toThree = ([x, y, z]: Point3): Point3 => [x, z, -y];
