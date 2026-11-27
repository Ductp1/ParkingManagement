import * as THREE from 'three';
import { RoundedBoxGeometry } from 'three/examples/jsm/geometries/RoundedBoxGeometry.js';
import type { LayoutCellDto } from '@/types/ownerLayout';
import { paintFloor } from './floorTexture';
import { DRIVABLE, cellAt, isSlot, openSide, slotCode, type FloorModel, type LayoutModel, type Side } from './layoutModel';

/**
 * Dựng bãi đỗ 3D từ sơ đồ 2D (không cần file mô hình): mỗi tầng một sàn theo level với mặt sàn sơn vạch, xe ở ô đang đỗ,
 * tường, cột, barie, dốc, trụ sạc. Trùng ý tưởng với bộ dựng 3D của màn tài xế đang ở nhánh khác – gộp chung sau khi
 * nhánh đó vào develop.
 */
export const CELL_SIZE = 2.5;
export const FLOOR_HEIGHT = 3;
const ZONE_GAP = CELL_SIZE * 2;
const CAR_WIDTH = 1.35;
const CAR_LENGTH = 2.1;
const CAR_COLORS = ['#e5e7eb', '#334155', '#9f1239', '#1d4ed8', '#94a3b8', '#d97706', '#047857'];

export interface SceneBounds {
  center: THREE.Vector3;
  size: number;
}

interface Placement {
  floor: FloorModel;
  offsetX: number;
  elevation: number;
}

/** Tầng cao hơn tầng đang chọn bị ẩn để nhìn được vào trong; nhiều khu cùng level đứng cạnh nhau. */
const place = (floors: FloorModel[], activeFloorId: number): Placement[] => {
  const active = floors.find((f) => f.floorId === activeFloorId) ?? floors[0];
  if (!active) return [];
  const lowest = Math.min(...floors.map((f) => f.level));
  const usedWidth = new Map<number, number>();
  return floors
    .filter((f) => f.level <= active.level)
    .map((floor) => {
      const offsetX = usedWidth.get(floor.level) ?? 0;
      usedWidth.set(floor.level, offsetX + floor.model.columns * CELL_SIZE + ZONE_GAP);
      return { floor, offsetX, elevation: (floor.level - lowest) * FLOOR_HEIGHT };
    });
};

/** Hướng quay quanh trục Y để trục +Z của vật thể trỏ ra cạnh `side`. */
const HEADING: Record<Side, number> = { bottom: 0, top: Math.PI, left: -Math.PI / 2, right: Math.PI / 2 };

interface Item {
  position: THREE.Vector3;
  scale?: THREE.Vector3;
  /** Quay quanh trục Y, rồi nghiêng quanh trục X của vật thể. */
  heading?: number;
  tilt?: number;
  color?: string;
}

const matrixOf = (item: Item) =>
  new THREE.Matrix4().compose(
    item.position,
    new THREE.Quaternion().setFromEuler(new THREE.Euler(item.tilt ?? 0, item.heading ?? 0, 0, 'YXZ')),
    item.scale ?? new THREE.Vector3(1, 1, 1)
  );

/** Gom vật thể cùng hình dạng thành InstancedMesh (một lệnh vẽ cho mỗi loại trên mỗi tầng). */
const createBatches = (target: THREE.Group, opacity: number) => {
  const add = (name: string, geometry: THREE.BufferGeometry, items: Item[], look: THREE.MeshStandardMaterialParameters = {}) => {
    if (!items.length) {
      geometry.dispose();
      return null;
    }
    const material = new THREE.MeshStandardMaterial({ roughness: 0.85, ...look });
    if (opacity < 1) {
      material.transparent = true;
      material.opacity = opacity * (look.opacity ?? 1);
    }
    const mesh = new THREE.InstancedMesh(geometry, material, items.length);
    mesh.name = name;
    mesh.castShadow = opacity === 1;
    mesh.receiveShadow = true;
    items.forEach((item, i) => {
      mesh.setMatrixAt(i, matrixOf(item));
      if (item.color) mesh.setColorAt(i, new THREE.Color(item.color));
    });
    target.add(mesh);
    return mesh;
  };
  return add;
};

const hash = (text: string) => [...text].reduce((sum, ch) => (sum * 31 + ch.charCodeAt(0)) >>> 0, 7);

const box = (x: number, y: number, z: number, width: number, height: number, depth: number, color?: string): Item => ({
  position: new THREE.Vector3(x, y, z),
  scale: new THREE.Vector3(width, height, depth),
  color,
});
const unitBox = () => new THREE.BoxGeometry(1, 1, 1);

const buildFloor = (placement: Placement, isActive: boolean, showState: boolean): THREE.Group => {
  const { floor, offsetX, elevation } = placement;
  const model: LayoutModel = floor.model;
  const width = model.columns * CELL_SIZE;
  const depth = model.rows * CELL_SIZE;
  const level = new THREE.Group();
  level.name = `floor-${floor.floorId}`;
  const opacity = isActive ? 1 : 0.4;
  const add = createBatches(level, opacity);

  add('slab', unitBox(), [box(offsetX + width / 2, elevation - 0.16, depth / 2, width + 0.5, 0.3, depth + 0.5)], { color: '#aab2bd' });

  const canvas = paintFloor(model, showState);
  const texture = canvas ? new THREE.CanvasTexture(canvas) : null;
  if (texture) {
    texture.colorSpace = THREE.SRGBColorSpace;
    texture.anisotropy = 8;
  }
  const surface = new THREE.Mesh(
    new THREE.PlaneGeometry(width, depth),
    new THREE.MeshStandardMaterial({ map: texture, color: texture ? '#ffffff' : '#454b55', roughness: 0.95, transparent: opacity < 1, opacity })
  );
  surface.name = 'surface';
  surface.rotation.x = -Math.PI / 2;
  surface.position.set(offsetX + width / 2, elevation + 0.002, depth / 2);
  surface.receiveShadow = true;
  level.add(surface);

  const centre = (c: LayoutCellDto) => ({
    x: offsetX + (c.x + c.widthCells / 2) * CELL_SIZE,
    z: (c.y + c.heightCells / 2) * CELL_SIZE,
  });
  const typeAt = (x: number, y: number) => cellAt(model, x, y)?.type ?? '';

  const pads: Item[] = [];
  const padCells: LayoutCellDto[] = [];
  const cars: Array<{ x: number; z: number; heading: number; sx: number; sz: number; color: string }> = [];
  const frames: Item[] = [];
  const chargers: Item[] = [];
  const screens: Item[] = [];
  const walls: Item[] = [];
  const copings: Item[] = [];
  const pillars: Item[] = [];
  const bands: Item[] = [];
  const posts: Item[] = [];
  const arms: Item[] = [];
  const entryLights: Item[] = [];
  const exitLights: Item[] = [];
  const ramps: Item[] = [];
  const rampEdges: Item[] = [];

  for (const c of model.cells) {
    const { x, z } = centre(c);
    if (isSlot(c)) {
      const side = openSide(model, c);
      const sideways = side === 'left' || side === 'right';
      const along = (sideways ? c.widthCells : c.heightCells) * CELL_SIZE;
      const across = (sideways ? c.heightCells : c.widthCells) * CELL_SIZE;
      // Lớp trong suốt để bắt chuột (hover) đúng từng ô.
      pads.push(box(x, elevation + 0.04, z, c.widthCells * CELL_SIZE * 0.94, 0.05, c.heightCells * CELL_SIZE * 0.94));
      padCells.push(c);
      if (showState && c.state === 'Occupied') {
        cars.push({
          x,
          z,
          heading: HEADING[side],
          sx: Math.min(across * 0.56, 1.9) / CAR_WIDTH,
          sz: Math.min(along * 0.84, 4.4) / CAR_LENGTH,
          color: CAR_COLORS[hash(slotCode(c)) % CAR_COLORS.length],
        });
      }
      if (showState && c.state === 'Reserved') {
        const w = c.widthCells * CELL_SIZE * 0.86;
        const d = c.heightCells * CELL_SIZE * 0.86;
        for (const s of [-1, 1]) {
          frames.push(box(x, elevation + 0.08, z + (s * d) / 2, w, 0.1, 0.09));
          frames.push(box(x + (s * w) / 2, elevation + 0.08, z, 0.09, 0.1, d));
        }
      }
      if (c.slotType === 'EvCharging') {
        // Trụ sạc đặt ở đầu ô đối diện lối đi.
        const back = along / 2 - 0.2;
        const heading = HEADING[side];
        const px = x - Math.sin(heading) * back;
        const pz = z - Math.cos(heading) * back;
        chargers.push({ ...box(px, elevation + 0.55, pz, 0.32, 1.1, 0.2), heading });
        screens.push({ ...box(px + Math.sin(heading) * 0.11, elevation + 0.85, pz + Math.cos(heading) * 0.11, 0.2, 0.24, 0.03), heading });
      }
    } else if (c.type === 'wall') {
      // Tường mảnh nối theo các ô tường kề bên: trụ giữa ô + cánh tay nửa ô về mỗi phía có tường.
      for (let wx = c.x; wx < c.x + c.widthCells; wx += 1) {
        for (let wy = c.y; wy < c.y + c.heightCells; wy += 1) {
          const mx = offsetX + (wx + 0.5) * CELL_SIZE;
          const mz = (wy + 0.5) * CELL_SIZE;
          const piece = (dx: number, dz: number, w: number, d: number) => {
            walls.push(box(mx + dx, elevation + 0.5, mz + dz, w, 1, d));
            copings.push(box(mx + dx, elevation + 1.04, mz + dz, w + 0.08, 0.08, d + 0.08));
          };
          const t = 0.36;
          const arm = (CELL_SIZE - t) / 2;
          piece(0, 0, t, t);
          if (typeAt(wx - 1, wy) === 'wall') piece(-(t + arm) / 2, 0, arm, t);
          if (typeAt(wx + 1, wy) === 'wall') piece((t + arm) / 2, 0, arm, t);
          if (typeAt(wx, wy - 1) === 'wall') piece(0, -(t + arm) / 2, t, arm);
          if (typeAt(wx, wy + 1) === 'wall') piece(0, (t + arm) / 2, t, arm);
        }
      }
    } else if (c.type === 'pillar') {
      const height = FLOOR_HEIGHT - 0.35;
      pillars.push(box(x, elevation + height / 2, z, 0.6, height, 0.6));
      for (let i = 0; i < 5; i += 1) bands.push(box(x, elevation + 0.08 + i * 0.14, z, 0.64, 0.14, 0.64, i % 2 ? '#111827' : '#facc15'));
    } else if (c.type === 'entry' || c.type === 'exit') {
      // Barie chắn ngang chiều xe chạy: lối đi nằm trái / phải thì cần chắn dọc theo Z.
      const alongX = DRIVABLE.has(typeAt(c.x - 1, c.y)) || DRIVABLE.has(typeAt(c.x + c.widthCells, c.y));
      const length = (alongX ? c.heightCells : c.widthCells) * CELL_SIZE * 0.82;
      const at = (offset: number) => (alongX ? { x, z: z + offset } : { x: x + offset, z });
      const start = at(-length / 2);
      posts.push(box(start.x, elevation + 0.55, start.z, 0.34, 1.1, 0.34));
      const parts = 6;
      for (let i = 0; i < parts; i += 1) {
        const p = at(-length / 2 + (length / parts) * (i + 0.5));
        const size = length / parts;
        arms.push(box(p.x, elevation + 0.98, p.z, alongX ? 0.12 : size, 0.14, alongX ? size : 0.12, i % 2 ? '#f8fafc' : '#dc2626'));
      }
      (c.type === 'entry' ? entryLights : exitLights).push({ position: new THREE.Vector3(start.x, elevation + 1.22, start.z) });
    } else if (c.type === 'ramp') {
      // Dốc cao dần về phía đối diện lối đi.
      const side = openSide(model, c);
      const sideways = side === 'left' || side === 'right';
      const run = (sideways ? c.widthCells : c.heightCells) * CELL_SIZE;
      const span = (sideways ? c.heightCells : c.widthCells) * CELL_SIZE * 0.92;
      const rise = Math.min(FLOOR_HEIGHT * 0.5, run * 0.6);
      const heading = HEADING[side];
      const tilt = Math.atan2(rise, run);
      const length = Math.hypot(run, rise);
      ramps.push({ position: new THREE.Vector3(x, elevation + rise / 2, z), scale: new THREE.Vector3(span, 0.16, length), heading, tilt });
      for (const s of [-1, 1]) {
        const dx = Math.cos(heading) * (s * span) * 0.5;
        const dz = -Math.sin(heading) * (s * span) * 0.5;
        rampEdges.push({ position: new THREE.Vector3(x + dx, elevation + rise / 2 + 0.12, z + dz), scale: new THREE.Vector3(0.12, 0.1, length), heading, tilt });
      }
    }
  }

  const slots = add('slots', unitBox(), pads, { transparent: true, opacity: 0, depthWrite: false });
  if (slots) {
    slots.castShadow = false;
    slots.userData.cells = padCells;
  }
  add('reserved', unitBox(), frames, { color: '#f59e0b', emissive: '#b45309', emissiveIntensity: 0.9 });
  add('chargers', unitBox(), chargers, { color: '#e2e8f0', roughness: 0.4 });
  add('charger-screens', unitBox(), screens, { color: '#06b6d4', emissive: '#0891b2', emissiveIntensity: 1.2 });
  add('walls', unitBox(), walls, { color: '#d6d9de' });
  add('copings', unitBox(), copings, { color: '#6b7280' });
  add('pillars', unitBox(), pillars, { color: '#c3c8cf' });
  add('pillar-bands', unitBox(), bands);
  add('gate-posts', unitBox(), posts, { color: '#ca8a04', roughness: 0.5 });
  add('gate-arms', unitBox(), arms, { roughness: 0.5 });
  add('entry-lights', new THREE.SphereGeometry(0.13, 12, 8), entryLights, { color: '#22c55e', emissive: '#16a34a', emissiveIntensity: 1.5 });
  add('exit-lights', new THREE.SphereGeometry(0.13, 12, 8), exitLights, { color: '#ef4444', emissive: '#dc2626', emissiveIntensity: 1.5 });
  add('ramps', unitBox(), ramps, { color: '#7b828d' });
  add('ramp-edges', unitBox(), rampEdges, { color: '#facc15' });

  // Xe: mỗi bộ phận một InstancedMesh; toạ độ bộ phận tính theo xe (mũi xe hướng +Z).
  const part = (px: number, py: number, pz: number, color?: (car: (typeof cars)[number]) => string): Item[] =>
    cars.map((car) => {
      const lx = px * car.sx;
      const lz = pz * car.sz;
      const cos = Math.cos(car.heading);
      const sin = Math.sin(car.heading);
      return {
        position: new THREE.Vector3(car.x + lx * cos + lz * sin, elevation + py, car.z - lx * sin + lz * cos),
        scale: new THREE.Vector3(car.sx, 1, car.sz),
        heading: car.heading,
        color: color?.(car),
      };
    });
  const paintWork = (car: (typeof cars)[number]) => car.color;
  const gloss = { roughness: 0.3, metalness: 0.45 };
  add('car-body', new RoundedBoxGeometry(CAR_WIDTH, 0.46, CAR_LENGTH, 3, 0.18), part(0, 0.45, 0, paintWork), gloss);
  add('car-glass', new RoundedBoxGeometry(CAR_WIDTH * 0.86, 0.38, CAR_LENGTH * 0.52, 3, 0.16), part(0, 0.82, -0.14), {
    color: '#16202f',
    roughness: 0.08,
    metalness: 0.7,
  });
  add('car-roof', new RoundedBoxGeometry(CAR_WIDTH * 0.76, 0.09, CAR_LENGTH * 0.32, 2, 0.04), part(0, 1.02, -0.18, paintWork), gloss);
  const wheel = () => new THREE.CylinderGeometry(0.24, 0.24, 0.2, 16).rotateZ(Math.PI / 2);
  const wheelX = CAR_WIDTH / 2 - 0.06;
  const wheelZ = CAR_LENGTH * 0.31;
  add(
    'car-wheels',
    wheel(),
    [-1, 1].flatMap((sx) => [-1, 1].flatMap((sz) => part(sx * wheelX, 0.24, sz * wheelZ))),
    { color: '#0b0f17', roughness: 0.9 }
  );
  const lamps = (end: 1 | -1) => [-1, 1].flatMap((s) => part(s * CAR_WIDTH * 0.3, 0.5, end * (CAR_LENGTH / 2 - 0.01)));
  add('car-headlights', new THREE.BoxGeometry(0.24, 0.1, 0.05), lamps(1), { color: '#fef9c3', emissive: '#fde68a', emissiveIntensity: 0.9 });
  add('car-taillights', new THREE.BoxGeometry(0.24, 0.1, 0.05), lamps(-1), { color: '#ef4444', emissive: '#b91c1c', emissiveIntensity: 0.9 });

  return level;
};

export const buildLayoutGroup = (
  floors: FloorModel[],
  activeFloorId: number,
  showState: boolean
): { group: THREE.Group; bounds: SceneBounds } => {
  const group = new THREE.Group();
  const area = new THREE.Box3();
  const active = floors.find((f) => f.floorId === activeFloorId) ?? floors[0];

  for (const placement of place(floors, activeFloorId)) {
    const { floor, offsetX, elevation } = placement;
    group.add(buildFloor(placement, floor.floorId === active?.floorId, showState));
    area.expandByPoint(new THREE.Vector3(offsetX, elevation, 0));
    area.expandByPoint(
      new THREE.Vector3(offsetX + floor.model.columns * CELL_SIZE, elevation + 1.2, floor.model.rows * CELL_SIZE)
    );
  }

  const center = area.isEmpty() ? new THREE.Vector3() : area.getCenter(new THREE.Vector3());
  const extent = area.isEmpty() ? new THREE.Vector3(10, 3, 10) : area.getSize(new THREE.Vector3());
  return { group, bounds: { center, size: Math.max(extent.x, extent.z, extent.y, 10) } };
};

export const disposeGroup = (group: THREE.Object3D) => {
  group.traverse((object) => {
    if (!(object instanceof THREE.Mesh)) return;
    object.geometry.dispose();
    const material = object.material as THREE.MeshStandardMaterial;
    material.map?.dispose();
    material.dispose();
    if (object instanceof THREE.InstancedMesh) object.dispose();
  });
};
