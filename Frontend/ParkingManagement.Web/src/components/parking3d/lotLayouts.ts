/**
 * Bố cục 3D của bãi đỗ để vẽ đường từ cổng vào đến ô đã đặt (tính năng "Xem đường đi 3D" của tài xế).
 *
 * Tọa độ đo theo hệ của Blender (mét, X sang đông, Y sang bắc, Z hướng lên) trên mô hình pk.blend
 * (D:/My_Project/Blender-Parking/pk.blend, xuất bằng tools/blender/export_pk.py). Viewer tự đổi sang hệ Y-up của three.js.
 * Ô đỗ chỉ là vạch kẻ trong ảnh bản vẽ CAD dán lên sàn nên tâm từng ô được đo trên ảnh chụp từ trên xuống
 * (ortho 23 m / 900 px, tâm camera (0.2, 0)): x = 0.2 + (px − 450)·23/900, y = (450 − py)·23/900.
 *
 * Mã ô theo quy ước của ParkingService ({tầng}-{hàng}{số}, ví dụ B1-A01): hàng A = dãy sát tường trái, B và C = hai dãy giữa,
 * D = dãy sát tường phải; số đếm từ phía cổng vào (nam) lên phía bắc.
 */

export type Vec2 = readonly [x: number, y: number];

export interface LayoutSlot {
  /** Mã ô đầy đủ, khớp Slot.Code / booking.slotCode (ví dụ "B1-A01"). */
  code: string;
  /** Tâm ô (Blender XY). */
  center: Vec2;
  /** Điểm trên lối đi trước đầu ô – xe rẽ vào ô từ đây. */
  front: Vec2;
  /** Lối đi dẫn tới ô. */
  aisle: 'left' | 'right';
  accessible?: boolean;
}

export interface LayoutFloor {
  /** Mã tầng trong dữ liệu bãi (Floor.Name, ví dụ "B1"). */
  code: string;
  label: string;
  /** Số tầng trong mô hình (custom property "floor" do export_pk.py gắn) – để ẩn các tầng phía trên. */
  modelFloor: number;
  /** Cao độ mặt sàn (Blender Z). */
  elevation: number;
  slots: LayoutSlot[];
}

export interface LotLayout {
  lotId: number;
  name: string;
  modelUrl: string;
  /** Cổng vào: điểm xuất phát ngoài bãi và điểm barie, cho từng phía (trái dùng cho dãy A/B và ram, phải cho C/D). */
  gates: Record<'left' | 'right', { outside: Vec2; barrier: Vec2 }>;
  /** Trục X của hai lối đi dọc và Y của lối ngang phía bắc (nối hai lối đi ở tầng trên). */
  aisles: { leftX: number; rightX: number; crossY: number };
  /** Ram xoắn ốc lên tầng trên. */
  ramp: {
    center: Vec2;
    radius: number;
    /** Góc bắt đầu (độ, 0 = hướng đông) và tổng góc quay ngược chiều kim đồng hồ cho mỗi tầng. */
    startAngleDeg: number;
    sweepDeg: number;
  };
  floors: LayoutFloor[];
}

// ---------- Đo trên bản vẽ (mét, Blender XY) ----------
const COL = {
  A: { center: -8.34, front: -6.9, aisle: 'left' },
  B: { center: -1.9, front: -3.33, aisle: 'left' },
  C: { center: 1.78, front: 3.27, aisle: 'right' },
  D: { center: 8.3, front: 6.84, aisle: 'right' },
} as const;

/** Tâm Y của các ô, theo thứ tự từ cổng vào lên phía bắc. */
const ROWS_Y = {
  /** Dãy trái: sàn tầng 1 có chốt bảo vệ chiếm ô sát cổng. */
  A: [-8.95, -6.95, -4.93, -2.89, -0.87, 1.15],
  /** Hai dãy giữa: tầng 1 có đảo giao thông chiếm ô sát cổng. */
  BC: [-8.96, -7.0, -5.0, -2.98, -0.95, 1.06, 3.07, 5.11],
  /** Dãy phải: hai ô trên cùng dành cho người khuyết tật. */
  D: [-8.96, -7.0, -5.0, -2.98, -0.95, 1.06, 3.07, 5.07, 8.09],
};

function column(floorCode: string, row: keyof typeof COL, ys: number[], accessibleFrom?: number): LayoutSlot[] {
  const c = COL[row];
  return ys.map((y, i) => ({
    code: `${floorCode}-${row}${String(i + 1).padStart(2, '0')}`,
    center: [c.center, y] as const,
    front: [c.front, y] as const,
    aisle: c.aisle,
    ...(accessibleFrom !== undefined && i >= accessibleFrom ? { accessible: true } : {}),
  }));
}

/** Tầng 1 (cổng vào): bỏ ô sát cổng ở dãy A, B, C, D (chốt bảo vệ + đảo giao thông). */
function groundFloor(code: string): LayoutSlot[] {
  return [
    ...column(code, 'A', ROWS_Y.A.slice(1)),
    ...column(code, 'B', ROWS_Y.BC.slice(1)),
    ...column(code, 'C', ROWS_Y.BC.slice(1)),
    ...column(code, 'D', ROWS_Y.D.slice(1), 6),
  ];
}

function upperFloor(code: string): LayoutSlot[] {
  return [
    ...column(code, 'A', ROWS_Y.A),
    ...column(code, 'B', ROWS_Y.BC),
    ...column(code, 'C', ROWS_Y.BC),
    ...column(code, 'D', ROWS_Y.D, 7),
  ];
}

/** Bãi xe Vincom Đồng Khởi (lot 1, hầm B1/B2) – dùng mô hình pk.blend: tầng 1 của mô hình = B1, tầng 2 = B2. */
export const VINCOM_LAYOUT: LotLayout = {
  lotId: 1,
  name: 'Bãi xe Vincom Đồng Khởi',
  modelUrl: '/models/lots/1/parking.glb',
  gates: {
    left: { outside: [-5.4, -13], barrier: [-5.4, -9.0] },
    right: { outside: [5.5, -13], barrier: [5.5, -9.0] },
  },
  aisles: { leftX: -5.12, rightX: 5.06, crossY: 7.4 },
  ramp: { center: [-8.76, 8.16], radius: 3.5, startAngleDeg: 0, sweepDeg: 627 },
  floors: [
    { code: 'B1', label: 'Hầm B1', modelFloor: 1, elevation: 0.21, slots: groundFloor('B1') },
    { code: 'B2', label: 'Hầm B2', modelFloor: 2, elevation: 6.0, slots: upperFloor('B2') },
  ],
};

const LAYOUTS: Record<number, LotLayout> = { [VINCOM_LAYOUT.lotId]: VINCOM_LAYOUT };

/** Bố cục 3D của bãi (undefined nếu bãi chưa có mô hình). */
export function getLotLayout(lotId: number | null | undefined): LotLayout | undefined {
  return lotId == null ? undefined : LAYOUTS[lotId];
}
