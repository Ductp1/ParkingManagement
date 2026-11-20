import * as THREE from 'three';
import type { DynamicFloorData, DynamicRoutePoint, DynamicRouteResult } from './astarGrid';

export const CELL_SIZE = 2.8;
export const FLOOR_HEIGHT = 4.5;

const CAR_COLORS = [0xc0392b, 0x2563eb, 0x334155, 0xe2e8f0, 0x0f766e, 0x475569, 0xd97706];

export function gridToWorld(
  gx: number,
  gy: number,
  cols: number,
  rows: number,
  floorIndex: number,
  yOffset = 0,
): THREE.Vector3 {
  const x = (gx - cols / 2 + 0.5) * CELL_SIZE;
  const z = (gy - rows / 2 + 0.5) * CELL_SIZE;
  const y = floorIndex * FLOOR_HEIGHT + yOffset;
  return new THREE.Vector3(x, y, z);
}

export function routePointToWorld(pt: DynamicRoutePoint, floors: DynamicFloorData[]): THREE.Vector3 {
  const floor = floors[pt.floorIndex];
  return gridToWorld(pt.gx, pt.gy, floor.layout.columns, floor.layout.rows, pt.floorIndex, 0.14);
}

/**
 * Dựng toàn bộ mô hình 3D động từ dữ liệu sơ đồ 2D của từng tầng (trả về từ ParkingService API).
 * Ẩn các tầng nằm phía trên tầng đích để không che khuất đường đi.
 */
export function buildDynamicParkingScene(
  scene: THREE.Scene,
  floors: DynamicFloorData[],
  routeResult: DynamicRouteResult,
  targetSlotCode: string,
): { dispose: () => void }[] {
  const disposables: { dispose: () => void }[] = [];
  const normalizedTarget = targetSlotCode.trim().toUpperCase();

  // Chỉ render từ tầng thấp nhất cho tới max(entryFloorIndex, targetFloorIndex)
  const maxVisibleFloor = Math.max(routeResult.entryFloorIndex, routeResult.targetFloorIndex);

  const slabMat = new THREE.MeshStandardMaterial({ color: 0x1e293b, roughness: 0.9 });
  const roadMat = new THREE.MeshStandardMaterial({ color: 0x334155, roughness: 0.85 });
  const entryMat = new THREE.MeshStandardMaterial({ color: 0x1d4ed8, roughness: 0.5 });
  const exitMat = new THREE.MeshStandardMaterial({ color: 0xb45309, roughness: 0.5 });
  const wallMat = new THREE.MeshStandardMaterial({ color: 0x64748b, roughness: 0.7 });
  const pillarMat = new THREE.MeshStandardMaterial({ color: 0x94a3b8, roughness: 0.6 });
  const slotAvailMat = new THREE.MeshStandardMaterial({ color: 0x0f172a, roughness: 0.8 });
  const slotBorderMat = new THREE.LineBasicMaterial({ color: 0x475569 });
  const slotReservedMat = new THREE.MeshStandardMaterial({ color: 0x78350f, roughness: 0.7 });
  const slotMaintMat = new THREE.MeshStandardMaterial({ color: 0x7f1d1d, roughness: 0.7 });
  const targetSlotMat = new THREE.MeshStandardMaterial({ color: 0x065f46, roughness: 0.5 });

  disposables.push(
    slabMat,
    roadMat,
    entryMat,
    exitMat,
    wallMat,
    pillarMat,
    slotAvailMat,
    slotBorderMat,
    slotReservedMat,
    slotMaintMat,
    targetSlotMat,
  );

  const carPositions: { pos: THREE.Vector3; color: number }[] = [];

  for (let fIdx = 0; fIdx <= maxVisibleFloor && fIdx < floors.length; fIdx++) {
    const floor = floors[fIdx];
    const cols = Math.max(1, floor.layout.columns);
    const rows = Math.max(1, floor.layout.rows);
    const floorY = fIdx * FLOOR_HEIGHT;

    // Nền tầng
    const slabGeo = new THREE.BoxGeometry(cols * CELL_SIZE + 0.6, 0.25, rows * CELL_SIZE + 0.6);
    const slab = new THREE.Mesh(slabGeo, slabMat);
    slab.position.set(0, floorY - 0.125, 0);
    scene.add(slab);
    disposables.push(slabGeo);

    // Hình học dùng chung cho các ô 1x1 (scale theo widthCells/heightCells)
    const tileGeo = new THREE.BoxGeometry(CELL_SIZE * 0.96, 0.04, CELL_SIZE * 0.96);
    const slotGeo = new THREE.BoxGeometry(CELL_SIZE * 0.88, 0.05, CELL_SIZE * 0.88);
    const wallGeo = new THREE.BoxGeometry(CELL_SIZE, 2.2, CELL_SIZE);
    const pillarGeo = new THREE.BoxGeometry(CELL_SIZE * 0.45, 2.8, CELL_SIZE * 0.45);
    const gatePostGeo = new THREE.BoxGeometry(0.22, 1.6, 0.22);
    const gateBarGeo = new THREE.BoxGeometry(CELL_SIZE * 0.85, 0.12, 0.12);
    const slotEdgeGeo = new THREE.EdgesGeometry(slotGeo);
    disposables.push(tileGeo, slotGeo, wallGeo, pillarGeo, gatePostGeo, gateBarGeo, slotEdgeGeo);

    for (const cell of floor.layout.cells) {
      const w = Math.max(1, cell.widthCells || 1);
      const h = Math.max(1, cell.heightCells || 1);
      const gx = cell.x + (w - 1) / 2;
      const gy = cell.y + (h - 1) / 2;
      const pos = gridToWorld(gx, gy, cols, rows, fIdx, 0.02);
      const type = (cell.type || '').toLowerCase();

      if (type === 'road' || type === 'ramp') {
        const mesh = new THREE.Mesh(tileGeo, roadMat);
        mesh.scale.set(w, 1, h);
        mesh.position.copy(pos);
        scene.add(mesh);
      } else if (type === 'entry' || type === 'exit') {
        const mesh = new THREE.Mesh(tileGeo, type === 'entry' ? entryMat : exitMat);
        mesh.scale.set(w, 1, h);
        mesh.position.copy(pos);
        scene.add(mesh);

        // Cổng barie nhỏ để dễ nhận diện cổng vào/ra
        const postL = new THREE.Mesh(gatePostGeo, pillarMat);
        postL.position.set(pos.x - CELL_SIZE * 0.4, floorY + 0.8, pos.z);
        const bar = new THREE.Mesh(gateBarGeo, type === 'entry' ? entryMat : exitMat);
        bar.position.set(pos.x, floorY + 1.1, pos.z);
        scene.add(postL, bar);
      } else if (type === 'wall') {
        const mesh = new THREE.Mesh(wallGeo, wallMat);
        mesh.scale.set(w, 1, h);
        mesh.position.set(pos.x, floorY + 1.1, pos.z);
        scene.add(mesh);
      } else if (type === 'pillar') {
        const mesh = new THREE.Mesh(pillarGeo, pillarMat);
        mesh.position.set(pos.x, floorY + 1.4, pos.z);
        scene.add(mesh);
      } else if (type === 'slot') {
        const isTarget = cell.code?.trim().toUpperCase() === normalizedTarget;
        const state = (cell.state || 'Available').toLowerCase();
        const mat = isTarget
          ? targetSlotMat
          : state === 'maintenance' || state === 'unavailable'
          ? slotMaintMat
          : state === 'reserved'
          ? slotReservedMat
          : slotAvailMat;

        const mesh = new THREE.Mesh(slotGeo, mat);
        mesh.scale.set(w, 1, h);
        mesh.position.copy(pos);
        scene.add(mesh);

        const outline = new THREE.LineSegments(slotEdgeGeo, slotBorderMat);
        outline.scale.set(w, 1, h);
        outline.position.copy(pos);
        scene.add(outline);

        // Nếu ô đang có xe đỗ (không phải ô đích), dựng xe 3D
        if (!isTarget && state === 'occupied') {
          const hash = (cell.x * 31 + cell.y * 17 + fIdx * 7) % CAR_COLORS.length;
          carPositions.push({
            pos: new THREE.Vector3(pos.x, floorY, pos.z),
            color: CAR_COLORS[Math.abs(hash)],
          });
        }
      }
    }
  }

  // Dựng xe đang đỗ bằng InstancedMesh (hiệu năng cao)
  if (carPositions.length > 0) {
    const bodyGeo = new THREE.BoxGeometry(1.9, 0.48, 1.3);
    const cabinGeo = new THREE.BoxGeometry(1.05, 0.38, 1.12);
    const wheelGeo = new THREE.CylinderGeometry(0.23, 0.23, 0.16, 12).rotateX(Math.PI / 2);
    const bodyMat = new THREE.MeshStandardMaterial({ roughness: 0.45, metalness: 0.3 });
    const cabinMat = new THREE.MeshStandardMaterial({ color: 0x1e293b, roughness: 0.15, metalness: 0.6 });
    const wheelMat = new THREE.MeshStandardMaterial({ color: 0x111111, roughness: 0.9 });

    const bodies = new THREE.InstancedMesh(bodyGeo, bodyMat, carPositions.length);
    const cabins = new THREE.InstancedMesh(cabinGeo, cabinMat, carPositions.length);
    const wheels = new THREE.InstancedMesh(wheelGeo, wheelMat, carPositions.length * 4);
    const m = new THREE.Matrix4();
    const color = new THREE.Color();

    carPositions.forEach((car, i) => {
      const p = car.pos;
      m.makeTranslation(p.x, p.y + 0.45, p.z);
      bodies.setMatrixAt(i, m);
      bodies.setColorAt(i, color.setHex(car.color));
      m.makeTranslation(p.x - 0.08, p.y + 0.86, p.z);
      cabins.setMatrixAt(i, m);
      [
        [0.65, 0.62],
        [0.65, -0.62],
        [-0.65, 0.62],
        [-0.65, -0.62],
      ].forEach(([dx, dz], w) => {
        m.makeTranslation(p.x + dx, p.y + 0.23, p.z + dz);
        wheels.setMatrixAt(i * 4 + w, m);
      });
    });

    scene.add(bodies, cabins, wheels);
    disposables.push(bodyGeo, cabinGeo, wheelGeo, bodyMat, cabinMat, wheelMat, bodies, cabins, wheels);
  }

  return disposables;
}
