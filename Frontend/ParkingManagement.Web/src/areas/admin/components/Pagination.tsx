import React from 'react';
import { formatNumber } from '../utils/format';

interface PaginationProps {
  page: number;
  pageSize: number;
  totalCount: number;
  onPageChange: (page: number) => void;
}

/** Danh sách số trang rút gọn: 1 2 3 … N (giữ trang hiện tại và hai bên). */
const pageItems = (page: number, totalPages: number): Array<number | 'gap'> => {
  const set = new Set<number>([1, totalPages, page - 1, page, page + 1]);
  if (page <= 3) [2, 3].forEach((p) => set.add(p));
  const pages = [...set].filter((p) => p >= 1 && p <= totalPages).sort((a, b) => a - b);
  const items: Array<number | 'gap'> = [];
  pages.forEach((p, i) => {
    if (i > 0 && p - pages[i - 1] > 1) items.push('gap');
    items.push(p);
  });
  return items;
};

const btn = 'adm-type-body-strong adm-press inline-flex h-8 min-w-8 items-center justify-center rounded-[2px] px-3';

/** Shell/Pagination: cao 48, viền trên, "Showing x–y of N" + Previous / số trang / Next. */
export const Pagination: React.FC<PaginationProps> = ({ page, pageSize, totalCount, onPageChange }) => {
  const totalPages = Math.max(1, Math.ceil(totalCount / pageSize));
  const from = totalCount === 0 ? 0 : (page - 1) * pageSize + 1;
  const to = Math.min(totalCount, page * pageSize);

  return (
    <nav aria-label="Pagination" className="flex h-12 items-center justify-between border-t border-(--admin-border-default) bg-(--admin-surface-card) px-3">
      <span className="adm-type-body-regular-12 text-(--admin-text-secondary)">
        Showing {formatNumber(from)}–{formatNumber(to)} of {formatNumber(totalCount)}
      </span>
      <div className="flex items-center gap-1">
        <button
          type="button"
          disabled={page <= 1}
          onClick={() => onPageChange(page - 1)}
          className={`${btn} border border-(--admin-border-default) bg-(--admin-surface-card) text-(--admin-brand-navy-900) disabled:opacity-40`}
        >
          Previous
        </button>
        {pageItems(page, totalPages).map((item, i) =>
          item === 'gap' ? (
            <span key={`gap-${i}`} className={`${btn} text-(--admin-brand-navy-900)`}>
              …
            </span>
          ) : (
            <button
              key={item}
              type="button"
              aria-current={item === page ? 'page' : undefined}
              onClick={() => onPageChange(item)}
              className={`${btn} ${
                item === page
                  ? 'bg-(--admin-brand-navy-900) text-(--admin-text-on-navy)'
                  : 'text-(--admin-brand-navy-900) hover:bg-(--admin-surface-selected)'
              }`}
            >
              {item}
            </button>
          )
        )}
        <button
          type="button"
          disabled={page >= totalPages}
          onClick={() => onPageChange(page + 1)}
          className={`${btn} border border-(--admin-border-default) bg-(--admin-surface-card) text-(--admin-brand-navy-900) disabled:opacity-40`}
        >
          Next
        </button>
      </div>
    </nav>
  );
};
