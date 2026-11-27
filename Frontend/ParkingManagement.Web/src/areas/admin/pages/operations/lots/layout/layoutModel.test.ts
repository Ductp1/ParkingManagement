import { describe, expect, it } from 'vitest';
import type { FloorLayoutDto } from '@/types/ownerLayout';
import {
  canRemoveEdge, cellAt, commit, detectPrefix, diffSlots, fillRect, fromDto, lineBetween, moveCells, nextSlotCode, openSide, paint,
  redo, renumberSlots, resizeEdge, standardLayout, startHistory, toCells, undo, validateLayout,
} from './layoutModel';

const slot = (x: number, y: number, code: string, state = 'Available') => ({
  x, y, type: 'slot', code, slotType: 'Standard', maxVehicleType: 'Suv', state, widthCells: 1, heightCells: 1,
});

const dto: FloorLayoutDto = {
  floorId: 8, floorName: 'G', level: 0, versionNo: 1, isPublished: true, publishedAtUtc: null, columns: 3, rows: 2,
  cells: [
    slot(0, 0, 'A-01'),
    slot(1, 0, 'A-02', 'Reserved'),
    { x: 0, y: 1, type: 'entry', widthCells: 1, heightCells: 1 },
    { x: 1, y: 1, type: 'road', widthCells: 1, heightCells: 1 },
    { x: 2, y: 1, type: 'empty', widthCells: 1, heightCells: 1 },
  ],
};
const published = fromDto(dto);

describe('layoutModel', () => {
  it('numbers a new slot with the lowest free code of the existing prefix', () => {
    expect(detectPrefix(published)).toBe('A-');
    expect(nextSlotCode(published, 'A-')).toBe('A-03');
    const withoutFirst = paint(published, 'eraser', 0, 0, 'A-');
    expect(nextSlotCode(withoutFirst, 'a-')).toBe('A-01');
  });

  it('keeps the code when painting over an existing slot with the slot tool', () => {
    expect(paint(published, 'slot', 1, 0, 'B-')).toBe(published);
  });

  it('reports a moved slot instead of a removed and an added one', () => {
    const moved = paint(paint(published, 'eraser', 0, 0, 'A-'), 'slot', 2, 0, 'A-');
    expect(diffSlots(published, moved)).toEqual({ added: [], removed: [], moved: ['A-01'], retyped: [] });
  });

  it('accepts a layout whose slots touch a road', () => {
    expect(validateLayout(paint(paint(published, 'road', 2, 0, 'A-'), 'road', 0, 1, 'A-'), published).errors).toEqual([]);
  });

  it('blocks removing a reserved slot and duplicate codes', () => {
    const removed = paint(published, 'eraser', 1, 0, 'A-');
    expect(validateLayout(removed, published).errors.join()).toContain('A-02');
    const duplicated = { ...published, cells: [...published.cells, slot(2, 0, 'a-01')] };
    expect(validateLayout(duplicated, published).errors.join()).toContain('Duplicate slot codes: A-01');
  });

  it('blocks overlapping and out-of-grid cells, and warns about unreachable slots', () => {
    const wide = { ...published, cells: published.cells.map((c) => (c.code === 'A-01' ? { ...c, widthCells: 2 } : c)) };
    expect(validateLayout(wide, published).errors.join()).toContain('Overlapping');
    expect(validateLayout({ ...published, columns: 1 }, published).errors.join()).toContain('Outside the grid');
    const lonely = { columns: 2, rows: 2, cells: [slot(0, 0, 'Z-01')] };
    expect(validateLayout(lonely, lonely).warnings).toEqual([
      '1 slot(s) do not touch a road: Z-01.',
      'No entry or ramp on this floor.',
    ]);
  });

  it('grows from any side; the top and left sides shift every cell and keep its code', () => {
    const grown = resizeEdge(resizeEdge(published, 'top', 1), 'left', 1);
    expect([grown.columns, grown.rows]).toEqual([4, 3]);
    expect(cellAt(grown, 1, 1)?.code).toBe('A-01');
    expect(diffSlots(published, grown).moved).toEqual(['A-01', 'A-02']);
  });

  it('only removes an empty row or column', () => {
    expect(canRemoveEdge(published, 'right')).toBe(true);
    expect(canRemoveEdge(published, 'top')).toBe(false);
    expect(resizeEdge(published, 'right', -1).columns).toBe(2);
    expect(resizeEdge(published, 'bottom', -1)).toBe(published);
  });

  it('moves a group together and refuses to leave the grid or cover another cell', () => {
    const first = cellAt(published, 0, 0)!;
    const second = cellAt(published, 1, 0)!;
    expect(moveCells(published, [first, second], 1, 0)?.model.cells.filter((c) => c.type === 'slot').map((c) => [c.code, c.x]))
      .toEqual([['A-01', 1], ['A-02', 2]]);
    expect(moveCells(published, [first], 1, 0)).toBeNull();
    expect(moveCells(published, [first], -1, 0)).toBeNull();
    expect(moveCells(published, [first], 0, 1)).toBeNull();
  });

  it('fills a rectangle and paints an unbroken line', () => {
    const filled = fillRect({ columns: 3, rows: 2, cells: [] }, 'slot', { x: 2, y: 1 }, { x: 0, y: 0 }, 'B-');
    expect(filled.cells.map((c) => c.code)).toEqual(['B-01', 'B-02', 'B-03', 'B-04', 'B-05', 'B-06']);
    expect(lineBetween({ x: 0, y: 0 }, { x: 3, y: 0 }).map((p) => p.x)).toEqual([0, 1, 2, 3]);
  });

  it('renumbers in reading order but keeps the code of a reserved slot', () => {
    const messy = {
      columns: 4,
      rows: 1,
      cells: [slot(3, 0, 'A-09'), slot(0, 0, 'A-40'), slot(1, 0, 'A-02', 'Reserved'), slot(2, 0, 'A-17')],
    };
    const result = renumberSlots(messy, published, 'A-');
    expect([0, 1, 2, 3].map((x) => cellAt(result, x, 0)?.code)).toEqual(['A-01', 'A-02', 'A-03', 'A-04']);
  });

  it.each([[5, 5], [12, 8], [20, 11]])('builds a %ix%i standard layout with every slot reachable', (columns, rows) => {
    const layout = standardLayout(columns, rows, 'A-')!;
    expect(validateLayout(layout, layout)).toEqual({ errors: [], warnings: [] });
    expect(layout.cells.some((c) => c.type === 'slot')).toBe(true);
    expect(cellAt(layout, 0, 1)?.type).toBe('entry');
    expect(cellAt(layout, columns - 1, rows - 2)?.type).toBe('exit');
  });

  it('refuses a standard layout on a tiny grid', () => {
    expect(standardLayout(4, 8, 'A-')).toBeNull();
  });

  it('opens a slot towards the neighbouring road', () => {
    expect(openSide(published, cellAt(published, 1, 0)!)).toBe('bottom');
  });

  it('undoes a whole drag stroke as one step', () => {
    const a = paint(published, 'wall', 2, 0, 'A-');
    const b = paint(a, 'wall', 2, 1, 'A-');
    const history = commit(commit(startHistory(published), a, 's1'), b, 's1');
    expect(undo(history).present).toBe(published);
    expect(redo(undo(history)).present).toBe(b);
  });

  it('sends every grid position row by row, with empty cells filled in', () => {
    const cells = toCells(published);
    expect(cells.map((c) => `${c.x},${c.y},${c.type}`)).toEqual([
      '0,0,slot', '1,0,slot', '2,0,empty', '0,1,entry', '1,1,road', '2,1,empty',
    ]);
    expect(cells[0]).toMatchObject({ code: 'A-01', slotType: 'Standard' });
    expect(cells[3]).toMatchObject({ code: null, slotType: null });
  });
});
