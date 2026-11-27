import * as THREE from 'three';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { distanceFromEntry } from './floorTexture';
import { standardLayout, type FloorModel } from './layoutModel';
import { FLOOR_HEIGHT, buildLayoutGroup, disposeGroup } from './layoutScene';

const floor = (floorId: number, level: number, slots: number): FloorModel => ({
  floorId,
  floorName: `F${floorId}`,
  level,
  model: {
    columns: 4,
    rows: 2,
    cells: Array.from({ length: slots }, (_, x) => ({
      x, y: 0, type: 'slot', code: `A-${x}`, state: x === 0 ? 'Occupied' : 'Available', widthCells: 1, heightCells: 1,
    })),
  },
});

const count = (group: THREE.Object3D, name: string) => (group.getObjectByName(name) as THREE.InstancedMesh | undefined)?.count ?? 0;

describe('buildLayoutGroup', () => {
  afterEach(() => vi.restoreAllMocks());
  // jsdom không có canvas 2D: sàn dùng màu trơn, phần khối vẫn dựng đủ.
  const withoutCanvas = () => vi.spyOn(HTMLCanvasElement.prototype, 'getContext').mockReturnValue(null);

  it('stacks floors by level and hides the floors above the selected one', () => {
    withoutCanvas();

    const { group } = buildLayoutGroup([floor(1, -1, 3), floor(2, 0, 2), floor(3, 1, 4)], 2, true);

    expect(group.children.map((c) => c.name)).toEqual(['floor-1', 'floor-2']);
    expect(group.children.map((c) => count(c, 'slots'))).toEqual([3, 2]);
    const pads = group.children[1].getObjectByName('slots') as THREE.InstancedMesh;
    const matrix = new THREE.Matrix4();
    pads.getMatrixAt(0, matrix);
    expect(new THREE.Vector3().setFromMatrixPosition(matrix).y).toBeCloseTo(FLOOR_HEIGHT + 0.04);
    expect(pads.userData.cells.map((c: { code: string }) => c.code)).toEqual(['A-0', 'A-1']);
    disposeGroup(group);
  });

  it('parks a car only on occupied slots, and none while designing', () => {
    withoutCanvas();

    const live = buildLayoutGroup([floor(1, 0, 3)], 1, true).group;
    const design = buildLayoutGroup([floor(1, 0, 3)], 1, false).group;

    expect([count(live, 'car-body'), count(live, 'car-wheels')]).toEqual([1, 4]);
    expect(count(design, 'car-body')).toBe(0);
    disposeGroup(live);
    disposeGroup(design);
  });

  it('builds walls, pillars and both gates of a standard layout', () => {
    withoutCanvas();

    const { group } = buildLayoutGroup([{ floorId: 1, floorName: 'G', level: 0, model: standardLayout(14, 8, 'A-')! }], 1, true);

    for (const name of ['walls', 'pillars', 'gate-arms', 'entry-lights', 'exit-lights']) expect(count(group, name), name).toBeGreaterThan(0);
    disposeGroup(group);
  });
});

describe('distanceFromEntry', () => {
  it('counts road steps away from the entry', () => {
    const distance = distanceFromEntry(standardLayout(8, 5, 'A-')!);

    expect(distance.get('0,1')).toBe(0);
    expect(distance.get('3,1')).toBe(3);
    expect(distance.get('0,0')).toBeUndefined();
  });
});
