import { cleanup, fireEvent, render, screen } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { LayoutGrid } from './LayoutGrid';
import type { LayoutModel } from './layoutModel';

const model: LayoutModel = {
  columns: 3,
  rows: 1,
  cells: [
    { x: 2, y: 0, type: 'slot', code: 'A-01', state: 'Occupied', widthCells: 1, heightCells: 1 },
    { x: 0, y: 0, type: 'entry', widthCells: 1, heightCells: 1 },
  ],
};

describe('LayoutGrid', () => {
  afterEach(cleanup);

  it('places each cell at its own column, whatever the order of the data', () => {
    render(<LayoutGrid model={model} showState />);

    expect(screen.getByRole('img', { name: 'Column 3, row 1: slot A-01 Occupied' }).style.gridColumn).toBe('3 / span 1');
    expect(screen.getByRole('img', { name: 'Column 1, row 1: entry' }).style.gridColumn).toBe('1 / span 1');
    expect(screen.getByRole('img', { name: 'Column 2, row 1: empty' })).toBeTruthy();
  });

  const handlers = () => ({
    onStart: vi.fn(), onMove: vi.fn(), onEnd: vi.fn(), onCancel: vi.fn(), onActivate: vi.fn(), onDragTool: vi.fn(),
    onDropTool: vi.fn(), ghost: null, marquee: null,
  });

  it('reports a keyboard activation with the position, including empty ones', () => {
    const edit = handlers();
    render(<LayoutGrid model={model} showState={false} edit={edit} />);

    fireEvent.click(screen.getByRole('button', { name: 'Column 2, row 1: empty' }));

    expect(edit.onActivate).toHaveBeenCalledWith({ x: 1, y: 0 }, false);
  });

  it('turns pointer positions into grid positions and clamps them to the grid', () => {
    const edit = handlers();
    render(<LayoutGrid model={model} showState={false} edit={edit} />);
    const grid = screen.getByTestId('layout-grid');

    fireEvent.pointerDown(grid, { button: 0, clientX: 45, clientY: 5 });
    fireEvent.pointerMove(grid, { clientX: 900, clientY: 900 });
    fireEvent.pointerUp(grid);
    fireEvent.pointerMove(grid, { clientX: 5, clientY: 5 });

    expect(edit.onStart).toHaveBeenCalledWith({ x: 1, y: 0 }, false);
    expect(edit.onMove).toHaveBeenCalledTimes(1);
    expect(edit.onMove).toHaveBeenCalledWith({ x: 2, y: 0 });
    expect(edit.onEnd).toHaveBeenCalledTimes(1);
  });

  it('accepts a tool dragged from the toolbar', () => {
    const edit = handlers();
    render(<LayoutGrid model={model} showState={false} edit={edit} />);
    const dataTransfer = { types: ['text/layout-tool'], getData: () => 'wall' };

    fireEvent.drop(screen.getByTestId('layout-grid'), { dataTransfer, clientX: 45, clientY: 5 });

    expect(edit.onDropTool.mock.calls[0][0]).toBe('wall');
  });
});
