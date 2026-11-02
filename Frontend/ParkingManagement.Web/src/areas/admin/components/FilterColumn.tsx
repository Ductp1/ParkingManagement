import React, { useRef } from 'react';
import { useSlidingIndicator } from '../hooks/useSlidingIndicator';
import { formatNumber } from '../utils/format';

export interface FilterOption {
  value: string;
  label: string;
  count?: number | null;
}

interface FilterPanelProps {
  title: string;
  options: FilterOption[];
  value: string;
  onChange?: (value: string) => void;
  /** API chưa hỗ trợ lọc theo nhóm này → hiển thị nhưng không bấm được. */
  disabled?: boolean;
  disabledReason?: string;
}

/**
 * Panel lọc cột trái (Figma "Panel/…" + Shell/Filter item): mỗi dòng = một giá trị enum + số lượng (nếu API trả).
 * Active: nền surface/selected, viền trái navy-800 2px, chữ body/bold navy-900.
 */
export const FilterPanel: React.FC<FilterPanelProps> = ({ title, options, value, onChange, disabled, disabledReason }) => {
  const listRef = useRef<HTMLUListElement>(null);
  const indicatorRef = useRef<HTMLLIElement>(null);
  // Nền + vạch trái của mục đang chọn trượt tới mục mới (spring nhẹ) thay vì nhảy.
  useSlidingIndicator(listRef, indicatorRef, 'y', value, '[aria-pressed="true"]');

  return (
    <section className="flex flex-col border border-(--admin-border-default) bg-(--admin-surface-card)" title={disabled ? disabledReason : undefined}>
      <header className="flex h-[33px] items-center border-b border-(--admin-border-default) px-3">
        <h2 className="adm-type-panel-title m-0 uppercase text-(--admin-text-secondary)">{title}</h2>
      </header>
      <ul ref={listRef} className="relative m-0 list-none p-0">
        {/* li (không phải span) để danh sách vẫn hợp lệ; aria-hidden nên không tính là một mục. */}
        <li
          ref={indicatorRef}
          aria-hidden="true"
          className="adm-indicator bg-(--admin-surface-selected) shadow-[inset_2px_0_0_var(--admin-brand-navy-800)]"
        />
        {options.map((opt) => {
          const active = opt.value === value;
          return (
            <li key={opt.value}>
              <button
                type="button"
                disabled={disabled}
                aria-pressed={active}
                onClick={() => onChange?.(opt.value)}
                className={`adm-tr-colors relative flex h-8 w-full items-center justify-between px-3 text-left disabled:cursor-not-allowed disabled:opacity-60 ${
                  active ? 'adm-type-body-bold text-(--admin-brand-navy-900)' : 'adm-type-body-regular-12 text-(--admin-text-nav) enabled:hover:bg-(--admin-surface-row-alt)'
                }`}
              >
                <span>{opt.label}</span>
                {opt.count !== undefined && opt.count !== null && (
                  <span className="adm-type-body-small text-(--admin-text-secondary)">{formatNumber(opt.count)}</span>
                )}
              </button>
            </li>
          );
        })}
      </ul>
    </section>
  );
};

/** Cột lọc rộng 200px bên trái danh sách. */
export const FilterColumn: React.FC<{ children: React.ReactNode }> = ({ children }) => (
  <aside className="flex w-[200px] shrink-0 flex-col gap-4">{children}</aside>
);
