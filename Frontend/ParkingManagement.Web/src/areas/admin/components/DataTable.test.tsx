import { cleanup, fireEvent, render, screen } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { DataTable, type DataTableColumn } from './DataTable';

interface Row {
  id: number;
  name: string;
}

const rows: Row[] = [
  { id: 1, name: 'Lot A' },
  { id: 2, name: 'Lot B' },
];

const columns: DataTableColumn<Row>[] = [
  { key: 'name', header: 'Name', render: (r) => r.name },
  { key: 'act', header: 'Actions', render: (r) => <button type="button">Edit {r.name}</button> },
];

const rowOf = (text: string) => screen.getByText(text).closest('tr') as HTMLTableRowElement;

afterEach(cleanup);

describe('DataTable – hàng mở chi tiết dùng được bằng bàn phím', () => {
  it('hàng có onRowClick nhận focus và mở bằng Enter / Space', () => {
    const onRowClick = vi.fn();
    render(<DataTable columns={columns} rows={rows} rowKey={(r) => r.id} onRowClick={onRowClick} />);
    const row = rowOf('Lot A');
    expect(row.tabIndex).toBe(0);

    fireEvent.keyDown(row, { key: 'Enter' });
    fireEvent.keyDown(row, { key: ' ' });
    expect(onRowClick).toHaveBeenCalledTimes(2);
    expect(onRowClick).toHaveBeenCalledWith(rows[0]);
  });

  it('phím khác và phím từ nút bên trong hàng không mở hàng', () => {
    const onRowClick = vi.fn();
    render(<DataTable columns={columns} rows={rows} rowKey={(r) => r.id} onRowClick={onRowClick} />);
    fireEvent.keyDown(rowOf('Lot A'), { key: 'a' });
    fireEvent.keyDown(screen.getByRole('button', { name: 'Edit Lot A' }), { key: 'Enter' });
    expect(onRowClick).not.toHaveBeenCalled();
  });

  it('bảng không có onRowClick thì hàng không vào thứ tự Tab', () => {
    render(<DataTable columns={columns} rows={rows} rowKey={(r) => r.id} />);
    expect(rowOf('Lot B').hasAttribute('tabindex')).toBe(false);
  });
});
