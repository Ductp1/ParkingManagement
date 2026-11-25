import { describe, expect, it } from 'vitest';
import { getLotLayout, VINCOM_LAYOUT } from './lotLayouts';
import { buildRoute, parkedCars, resolveTarget, ROUTE_LIFT, toThree } from './routePlanner';

const last = <T,>(items: T[]) => items[items.length - 1];

describe('resolveTarget', () => {
  it('nhận mã ô đầy đủ của ParkingService', () => {
    const t = resolveTarget(VINCOM_LAYOUT, 'B1-A01');
    expect(t?.floor.code).toBe('B1');
    expect(t?.slot?.code).toBe('B1-A01');
  });

  it('nhận mã rút gọn kèm tên tầng', () => {
    expect(resolveTarget(VINCOM_LAYOUT, 'c-3', 'Hầm B2')?.slot?.code).toBe('B2-C03');
    expect(resolveTarget(VINCOM_LAYOUT, 'D07', 'B1')?.slot?.accessible).toBe(true);
  });

  it('ô hợp lệ nhưng chưa có trên mô hình → slot null, vẫn biết tầng', () => {
    const t = resolveTarget(VINCOM_LAYOUT, 'B1-A10');
    expect(t).toMatchObject({ code: 'B1-A10', slot: null });
    expect(t?.floor.code).toBe('B1');
  });

  it('tầng không có trong bố cục hoặc mã sai → null', () => {
    expect(resolveTarget(VINCOM_LAYOUT, 'B3-A01')).toBeNull();
    expect(resolveTarget(VINCOM_LAYOUT, 'A-01')).toBeNull();
    expect(resolveTarget(VINCOM_LAYOUT, '')).toBeNull();
    expect(resolveTarget(VINCOM_LAYOUT, '???')).toBeNull();
  });
});

describe('buildRoute', () => {
  it('tầng 1: bắt đầu ngoài cổng, kết thúc tại tâm ô, luôn trên mặt sàn', () => {
    const target = resolveTarget(VINCOM_LAYOUT, 'B1-A02')!;
    const route = buildRoute(VINCOM_LAYOUT, target);
    expect(route[0].slice(0, 2)).toEqual([...VINCOM_LAYOUT.gates.left.outside]);
    expect(last(route).slice(0, 2)).toEqual([...target.slot!.center]);
    expect(route.every((p) => p[2] === 0.21 + ROUTE_LIFT)).toBe(true);
  });

  it('ô dãy phải đi cổng phải và lối đi phải', () => {
    const route = buildRoute(VINCOM_LAYOUT, resolveTarget(VINCOM_LAYOUT, 'B1-D03')!);
    expect(route[0][0]).toBe(VINCOM_LAYOUT.gates.right.outside[0]);
    expect(route.some((p) => p[0] === VINCOM_LAYOUT.aisles.rightX)).toBe(true);
  });

  it('tầng trên: đi qua ram, cao độ không giảm, kết thúc ở sàn tầng đích', () => {
    const target = resolveTarget(VINCOM_LAYOUT, 'B2-C05')!;
    const route = buildRoute(VINCOM_LAYOUT, target);
    for (let i = 1; i < route.length; i++) expect(route[i][2]).toBeGreaterThanOrEqual(route[i - 1][2]);
    expect(last(route)).toEqual([...target.slot!.center, 6.0 + ROUTE_LIFT]);
    const { center, radius } = VINCOM_LAYOUT.ramp;
    const onRamp = route.filter((p) => Math.abs(Math.hypot(p[0] - center[0], p[1] - center[1]) - radius) < 1e-6);
    expect(onRamp.length).toBeGreaterThan(40);
  });

  it('không có đoạn dài 0 (Catmull-Rom cần điểm phân biệt)', () => {
    for (const code of ['B1-A01', 'B1-B07', 'B2-A01', 'B2-D09', 'B2-B01']) {
      const route = buildRoute(VINCOM_LAYOUT, resolveTarget(VINCOM_LAYOUT, code)!);
      for (let i = 1; i < route.length; i++) {
        const [a, b] = [route[i - 1], route[i]];
        expect(Math.hypot(a[0] - b[0], a[1] - b[1], a[2] - b[2])).toBeGreaterThan(0.05);
      }
    }
  });

  it('ô chưa có trên mô hình: vẫn dẫn tới tầng đích', () => {
    const route = buildRoute(VINCOM_LAYOUT, resolveTarget(VINCOM_LAYOUT, 'B2-A10')!);
    expect(last(route)[2]).toBe(6.0 + ROUTE_LIFT);
  });
});

describe('parkedCars', () => {
  it('tầng 1: xe đỗ kín mọi ô trừ ô đã đặt', () => {
    const target = resolveTarget(VINCOM_LAYOUT, 'B1-A02')!;
    const cars = parkedCars(VINCOM_LAYOUT, target);
    expect(cars).toHaveLength(VINCOM_LAYOUT.floors[0].slots.length - 1);
    expect(cars.some((c) => c.slotCode === 'B1-A02')).toBe(false);
    expect(cars.every((c) => c.position[2] === 0.21)).toBe(true);
  });

  it('tầng 2: có xe ở cả tầng 1 và tầng 2; màu ổn định theo mã ô', () => {
    const target = resolveTarget(VINCOM_LAYOUT, 'B2-C05')!;
    const cars = parkedCars(VINCOM_LAYOUT, target);
    const total = VINCOM_LAYOUT.floors[0].slots.length + VINCOM_LAYOUT.floors[1].slots.length;
    expect(cars).toHaveLength(total - 1);
    expect(parkedCars(VINCOM_LAYOUT, target)).toEqual(cars);
  });

  it('ô chưa có trên mô hình: mọi ô đã vẽ đều có xe', () => {
    const cars = parkedCars(VINCOM_LAYOUT, resolveTarget(VINCOM_LAYOUT, 'B1-A10')!);
    expect(cars).toHaveLength(VINCOM_LAYOUT.floors[0].slots.length);
  });
});

describe('getLotLayout / toThree', () => {
  it('chỉ bãi có mô hình mới có bố cục', () => {
    expect(getLotLayout(1)).toBe(VINCOM_LAYOUT);
    expect(getLotLayout(2)).toBeUndefined();
    expect(getLotLayout(undefined)).toBeUndefined();
  });

  it('đổi Z-up sang Y-up như trình xuất glTF', () => {
    expect(toThree([1, 2, 3])).toEqual([1, 3, -2]);
  });
});
