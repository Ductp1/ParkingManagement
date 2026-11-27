import { cleanup, fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { AdminApiError } from '@/api/adminClientApi';
import type { FloorLayoutDto } from '@/types/ownerLayout';
import { AdminToastProvider } from '../../../../components/AdminToastProvider';
import { LayoutTab } from './LotLayoutTab';

const h = vi.hoisted(() => ({
  hierarchy: vi.fn(), getLayout: vi.fn(), getDraft: vi.fn(), saveDraft: vi.fn(), publish: vi.fn(),
}));

vi.mock('@/api/adminOperationsApi', () => ({ adminOperationsApi: { getParkingLotHierarchy: h.hierarchy } }));
vi.mock('@/api/adminLayoutApi', () => ({
  adminLayoutApi: { getLayout: h.getLayout, getDraft: h.getDraft, saveDraft: h.saveDraft, publish: h.publish },
}));

const layout: FloorLayoutDto = {
  floorId: 8, floorName: 'Ground', level: 0, versionNo: 0, isPublished: false, publishedAtUtc: null, columns: 2, rows: 2,
  cells: [
    { x: 0, y: 0, type: 'slot', code: 'A-01', slotType: 'Standard', maxVehicleType: 'Suv', state: 'Available', widthCells: 1, heightCells: 1 },
  ],
};

const renderTab = () =>
  render(
    <AdminToastProvider>
      <LayoutTab lotId={9} />
    </AdminToastProvider>,
  );

describe('Lot layout tab', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    h.hierarchy.mockResolvedValue({ zones: [{ id: 1, code: 'NT', name: 'Outdoor', isClosed: false, floors: [{ id: 8, name: 'Ground', isClosed: false }] }] });
    h.getLayout.mockResolvedValue(layout);
    h.getDraft.mockResolvedValue(layout);
  });
  afterEach(cleanup);

  it('draws the published layout with the slot state', async () => {
    renderTab();

    expect(await screen.findByRole('img', { name: 'Column 1, row 1: slot A-01 Available' })).toBeTruthy();
    expect(screen.getByText(/Not published yet/)).toBeTruthy();
  });

  it('paints a road and a new slot, then publishes the whole grid with a note', async () => {
    h.publish.mockResolvedValue(layout);
    renderTab();
    fireEvent.click(await screen.findByRole('button', { name: 'Edit layout' }));
    fireEvent.click(await screen.findByRole('button', { name: 'Road' }));
    fireEvent.click(screen.getByRole('button', { name: 'Column 1, row 2: empty' }));
    fireEvent.click(screen.getByRole('button', { name: 'Slot' }));
    fireEvent.click(screen.getByRole('button', { name: 'Column 2, row 1: empty' }));

    fireEvent.click(screen.getByRole('button', { name: 'Publish' }));
    const dialog = screen.getByRole('dialog');
    expect(within(dialog).getByText('A-02')).toBeTruthy();
    fireEvent.change(within(dialog).getByRole('textbox'), { target: { value: 'Add road' } });
    fireEvent.click(within(dialog).getByRole('button', { name: 'Publish' }));

    await waitFor(() => expect(h.publish).toHaveBeenCalledTimes(1));
    const [floorId, body] = h.publish.mock.calls[0];
    expect(floorId).toBe(8);
    expect(body).toMatchObject({ columns: 2, rows: 2, changeNote: 'Add road' });
    expect(body.cells.map((c: { type: string; code?: string }) => c.code ?? c.type)).toEqual(['A-01', 'A-02', 'road', 'empty']);
  });

  const openEditor = async () => {
    renderTab();
    fireEvent.click(await screen.findByRole('button', { name: 'Edit layout' }));
    return screen.findByTestId('layout-grid');
  };
  /** Kéo từ ô (cột, hàng) sang ô khác; mỗi ô lưới 40×28 cách nhau 2px. */
  const drag = (grid: HTMLElement, from: [number, number], to: [number, number], shiftKey = false) => {
    const at = ([x, y]: [number, number]) => ({ clientX: x * 42 + 5, clientY: y * 30 + 5 });
    fireEvent.pointerDown(grid, { button: 0, shiftKey, ...at(from) });
    fireEvent.pointerMove(grid, at(to));
    fireEvent.pointerUp(grid);
  };
  const cell = (name: string) => screen.queryByRole('button', { name });

  it('drags a slot to another cell keeping its code, then undoes and redoes the move', async () => {
    const grid = await openEditor();

    drag(grid, [0, 0], [1, 1]);

    expect(cell('Column 2, row 2: slot A-01 Standard')).toBeTruthy();
    fireEvent.click(screen.getByRole('button', { name: 'Undo' }));
    expect(cell('Column 1, row 1: slot A-01 Standard')).toBeTruthy();
    fireEvent.keyDown(document.body, { key: 'y', ctrlKey: true });
    expect(cell('Column 2, row 2: slot A-01 Standard')).toBeTruthy();
  });

  it('selects an area, moves it with the arrow keys and deletes it', async () => {
    const grid = await openEditor();

    drag(grid, [1, 1], [0, 0]);
    expect(screen.getByDisplayValue('A-01')).toBeTruthy();
    fireEvent.keyDown(document.body, { key: 'ArrowRight' });
    expect(cell('Column 2, row 1: slot A-01 Standard')).toBeTruthy();
    fireEvent.keyDown(document.body, { key: 'Delete' });
    expect(cell('Column 2, row 1: empty')).toBeTruthy();
    fireEvent.keyDown(document.body, { key: 'z', metaKey: true });
    expect(cell('Column 2, row 1: slot A-01 Standard')).toBeTruthy();
  });

  it('adds a column on the left for a wall and paints it by dragging as one undo step', async () => {
    const grid = await openEditor();

    fireEvent.click(screen.getByRole('button', { name: 'Add column at left' }));
    fireEvent.click(screen.getByRole('button', { name: 'Wall' }));
    drag(grid, [0, 0], [0, 1]);

    expect(cell('Column 1, row 1: wall')).toBeTruthy();
    expect(cell('Column 1, row 2: wall')).toBeTruthy();
    expect(cell('Column 2, row 1: slot A-01 Standard')).toBeTruthy();
    fireEvent.click(screen.getByRole('button', { name: 'Undo' }));
    expect(cell('Column 1, row 2: empty')).toBeTruthy();
    expect(cell('Column 1, row 1: empty')).toBeTruthy();
  });

  it('does not offer a standard layout on a grid that is too small', async () => {
    await openEditor();

    fireEvent.click(screen.getByRole('button', { name: 'Generate standard layout' }));

    expect(screen.getByText('The grid needs at least 5 × 5 cells.')).toBeTruthy();
    expect(screen.getByRole('button', { name: 'Replace drawing' })).toHaveProperty('disabled', true);
  });

  it('shows the server message when saving the draft is refused', async () => {
    h.saveDraft.mockRejectedValue(new AdminApiError(409, 'Slot A-01 is reserved.'));
    renderTab();
    fireEvent.click(await screen.findByRole('button', { name: 'Edit layout' }));
    fireEvent.click(await screen.findByRole('button', { name: 'Save draft' }));

    expect(await screen.findByText('Slot A-01 is reserved.')).toBeTruthy();
  });
});
