import React, { useEffect, useRef } from 'react';
import { Link } from 'react-router-dom';
import { StatusIndicator } from './StatusIndicator';
import { useStaggerOnce } from '../hooks/useStaggerOnce';
import { animate, motionMs, motionVar } from '../utils/motion';

export interface DataTableColumn<T> {
  key: string;
  header: string;
  /** Độ rộng cột (px hoặc %), mặc định chia đều. */
  width?: number | string;
  align?: 'left' | 'right';
  render: (row: T, index: number) => React.ReactNode;
}

interface DataTableProps<T> {
  columns: DataTableColumn<T>[];
  rows: T[];
  rowKey: (row: T) => string | number;
  onRowClick?: (row: T) => void;
  selectedKey?: string | number | null;
  /** Nội dung thay cho body khi không có dòng (State block loading/empty/error…). */
  placeholder?: React.ReactNode;
  footer?: React.ReactNode;
  /** Bỏ viền ngoài khi bảng nằm trong Panel. */
  bare?: boolean;
  /**
   * Motion (mặc định bật): lần hiển thị đầu 8 dòng đầu hiện so le (--stagger-step); sau đó đổi dữ liệu (refetch, phân trang,
   * lọc, sort) chỉ crossfade thân bảng. false = tắt hẳn.
   */
  animateRows?: boolean;
  /** Đang tải lại: giữ dữ liệu cũ, làm mờ nhẹ + thanh tiến trình trên header thay vì nháy skeleton. */
  busy?: boolean;
  /** Dòng vừa được cập nhật sau một thao tác: nền nháy highlight một lần (~1 s). */
  highlightKey?: string | number | null;
}

const MAX_STAGGER_ROWS = 8;

/**
 * Shell/Table (Figma): header nền table/header chữ trắng, dòng 44px (52 khi two-line), xen kẽ row-alt,
 * dòng chọn surface/selected + viền trái navy-800 2px. Không có cột trống ở đầu bảng.
 */
export const DataTable = <T,>({
  columns,
  rows,
  rowKey,
  onRowClick,
  selectedKey,
  placeholder,
  footer,
  bare,
  animateRows = true,
  busy,
  highlightKey,
}: DataTableProps<T>) => {
  const bodyRef = useRef<HTMLTableSectionElement>(null);
  const signature = rows.map((row) => String(rowKey(row))).join('|');
  const stagger = useStaggerOnce(animateRows ? signature : '') && animateRows;
  const shownSignature = useRef<string | null>(null);

  // Sau lần hiển thị đầu: danh sách đổi → crossfade thân bảng (không stagger lại, không remount dòng).
  useEffect(() => {
    const body = bodyRef.current;
    const previous = shownSignature.current;
    shownSignature.current = signature;
    if (!animateRows || !body || previous === null || previous === '' || previous === signature) return;
    animate(body, [{ opacity: 0.35 }, { opacity: 1 }], { duration: motionMs('--dur-base', body), easing: motionVar('--ease-out', body) });
  }, [signature, animateRows]);

  return (
    <div
      className={`relative flex min-w-0 flex-col bg-(--admin-surface-card) ${bare ? '' : 'border border-(--admin-border-default)'}`}
      aria-busy={busy || undefined}
    >
      {busy && <span className="adm-progress z-10" aria-hidden="true" />}
      <div className={`adm-motion-dim overflow-x-auto ${busy ? 'opacity-60' : ''}`}>
        <table className="w-full border-collapse">
          <thead>
            <tr>
              {columns.map((col) => (
                <th
                  key={col.key}
                  scope="col"
                  style={{ width: col.width }}
                  className={`adm-type-table-header h-7 border-r border-(--admin-table-header-border) bg-(--admin-table-header) px-3 whitespace-nowrap uppercase text-(--admin-text-on-navy) last:border-r-0 ${
                    col.align === 'right' ? 'text-right' : 'text-left'
                  }`}
                >
                  {col.header}
                </th>
              ))}
            </tr>
          </thead>
          {rows.length > 0 && (
            <tbody ref={bodyRef}>
              {rows.map((row, index) => {
                const key = rowKey(row);
                const selected = selectedKey !== undefined && selectedKey === key;
                const rise = stagger && index < MAX_STAGGER_ROWS;
                return (
                  <tr
                    key={key}
                    onClick={onRowClick ? () => onRowClick(row) : undefined}
                    style={rise ? ({ '--adm-delay': `calc(var(--stagger-step) * ${index})` } as React.CSSProperties) : undefined}
                    className={`h-11 border-b border-(--admin-border-default) ${animateRows ? 'adm-motion-row' : ''} ${rise ? 'adm-motion-rise' : ''} ${
                      highlightKey !== undefined && highlightKey !== null && highlightKey === key ? 'adm-row-highlight' : ''
                    } ${
                      selected
                        ? 'bg-(--admin-surface-selected) shadow-[inset_2px_0_0_var(--admin-brand-navy-800)]'
                        : index % 2 === 1
                          ? 'bg-(--admin-surface-row-alt)'
                          : 'bg-(--admin-surface-card)'
                    } ${onRowClick ? 'cursor-pointer hover:bg-(--admin-surface-selected)' : ''}`}
                  >
                    {columns.map((col) => (
                      <td key={col.key} className={`px-3 py-1.5 align-middle ${col.align === 'right' ? 'text-right' : 'text-left'}`}>
                        {col.render(row, index)}
                      </td>
                    ))}
                  </tr>
                );
              })}
            </tbody>
          )}
        </table>
      </div>
      {rows.length === 0 && placeholder}
      {footer}
    </div>
  );
};

/* ===== Shell/Table/Cell variants ===== */

export const CellText: React.FC<{ children: React.ReactNode }> = ({ children }) => (
  <span className="adm-type-body-default text-(--admin-text-primary)">{children}</span>
);

export const CellStrong: React.FC<{ children: React.ReactNode }> = ({ children }) => (
  <span className="adm-type-body-strong text-(--admin-text-primary)">{children}</span>
);

export const CellTwoLine: React.FC<{ primary: React.ReactNode; secondary?: React.ReactNode }> = ({ primary, secondary }) => (
  <span className="flex flex-col">
    <span className="adm-type-body-strong text-(--admin-text-primary)">{primary}</span>
    {secondary && <span className="adm-type-body-small text-(--admin-text-secondary)">{secondary}</span>}
  </span>
);

export const CellMono: React.FC<{ children: React.ReactNode }> = ({ children }) => (
  <span className="adm-type-body-mono whitespace-nowrap text-(--admin-text-primary)">{children}</span>
);

export const CellMuted: React.FC<{ children: React.ReactNode }> = ({ children }) => (
  <span className="adm-type-body-regular-12 whitespace-nowrap text-(--admin-text-secondary)">{children}</span>
);

export const CellAmount: React.FC<{ children: React.ReactNode }> = ({ children }) => (
  <span className="adm-type-body-strong whitespace-nowrap text-(--admin-text-primary)">{children}</span>
);

export const CellLink: React.FC<{ to: string; children: React.ReactNode }> = ({ to, children }) => (
  <Link to={to} onClick={(e) => e.stopPropagation()} className="adm-type-link-default adm-tr-colors text-(--admin-brand-navy-900) hover:underline">
    {children}
  </Link>
);

export const CellStatus: React.FC<{ value?: string | null; label?: string }> = ({ value, label }) => (
  <StatusIndicator value={value} label={label} />
);

/** Cột hành động: nút Ghost "View" đi tới chi tiết. */
export const CellView: React.FC<{ to: string; label?: string }> = ({ to, label = 'View' }) => (
  <Link
    to={to}
    onClick={(e) => e.stopPropagation()}
    className="adm-type-body-strong adm-press inline-flex h-8 items-center rounded-[2px] px-3 text-(--admin-brand-navy-900) hover:bg-(--admin-surface-selected)"
  >
    {label}
  </Link>
);
