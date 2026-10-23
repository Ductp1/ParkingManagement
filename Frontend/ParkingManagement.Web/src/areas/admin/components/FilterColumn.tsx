import React from 'react';
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
export const FilterPanel: React.FC<FilterPanelProps> = ({ title, options, value, onChange, disabled, disabledReason }) => (
  <section className="flex flex-col border border-(--admin-border-default) bg-(--admin-surface-card)" title={disabled ? disabledReason : undefined}>
    <header className="flex h-[33px] items-center border-b border-(--admin-border-default) px-3">
      <h2 className="adm-type-panel-title m-0 uppercase text-(--admin-text-secondary)">{title}</h2>
    </header>
    <ul className="m-0 list-none p-0">
      {options.map((opt) => {
        const active = opt.value === value;
        return (
          <li key={opt.value}>
            <button
              type="button"
              disabled={disabled}
              aria-pressed={active}
              onClick={() => onChange?.(opt.value)}
              className={`flex h-8 w-full items-center justify-between px-3 text-left disabled:cursor-not-allowed disabled:opacity-60 ${
                active
                  ? 'adm-type-body-bold bg-(--admin-surface-selected) text-(--admin-brand-navy-900) shadow-[inset_2px_0_0_var(--admin-brand-navy-800)]'
                  : 'adm-type-body-regular-12 text-(--admin-text-nav) enabled:hover:bg-(--admin-surface-row-alt)'
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

/** Cột lọc rộng 200px bên trái danh sách. */
export const FilterColumn: React.FC<{ children: React.ReactNode }> = ({ children }) => (
  <aside className="flex w-[200px] shrink-0 flex-col gap-4">{children}</aside>
);
