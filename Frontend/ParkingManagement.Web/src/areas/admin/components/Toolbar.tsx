import React, { useEffect, useState } from 'react';
import { AdminIcon } from './AdminIcon';
import { ADMIN_ICONS } from './adminIcons';

interface SearchBoxProps {
  placeholder: string;
  value: string;
  onSearch: (value: string) => void;
  width?: number;
  disabled?: boolean;
  disabledReason?: string;
}

/** Ô tìm kiếm (Figma "Search"): 32px, viền border/default, icon search, gõ xong 400ms mới gọi API. */
export const SearchBox: React.FC<SearchBoxProps> = ({ placeholder, value, onSearch, width = 320, disabled, disabledReason }) => {
  const [text, setText] = useState(value);

  useEffect(() => {
    if (text === value) return;
    const handle = window.setTimeout(() => onSearch(text.trim()), 400);
    return () => window.clearTimeout(handle);
  }, [text, value, onSearch]);

  return (
    <label
      className={`adm-tr-colors flex h-8 items-center gap-2 rounded-[2px] border border-(--admin-border-default) bg-(--admin-surface-card) px-2.5 text-(--admin-text-secondary) focus-within:border-(--admin-brand-navy-900) ${disabled ? 'opacity-60' : ''}`}
      style={{ width }}
      title={disabled ? disabledReason : undefined}
    >
      <AdminIcon icon={ADMIN_ICONS.search} />
      <input
        type="search"
        value={text}
        disabled={disabled}
        onChange={(e) => setText(e.target.value)}
        placeholder={placeholder}
        aria-label={placeholder}
        className="adm-type-body-default min-w-0 flex-1 bg-transparent text-(--admin-text-primary) outline-none placeholder:text-(--admin-text-placeholder) disabled:cursor-not-allowed"
      />
    </label>
  );
};

export interface ToolbarSelectOption {
  value: string;
  label: string;
}

interface ToolbarSelectProps {
  label: string;
  value: string;
  options: ToolbarSelectOption[];
  onChange: (value: string) => void;
  width?: number;
  disabled?: boolean;
  disabledReason?: string;
}

/** Bộ lọc dạng nút "City: All ▾" (Figma "Filter/…"): native select phủ trong suốt để giữ đúng giao diện. */
export const ToolbarSelect: React.FC<ToolbarSelectProps> = ({ label, value, options, onChange, width = 150, disabled, disabledReason }) => {
  const current = options.find((o) => o.value === value)?.label ?? options[0]?.label ?? '';
  return (
    <label
      className={`adm-tr-colors relative flex h-8 items-center justify-between gap-2 rounded-[2px] border border-(--admin-border-default) bg-(--admin-surface-card) px-2.5 text-(--admin-text-secondary) focus-within:border-(--admin-brand-navy-900) ${disabled ? 'opacity-60' : ''}`}
      style={{ width }}
      title={disabled ? disabledReason : undefined}
    >
      <span className="adm-type-body-regular-12 truncate text-(--admin-text-primary)">
        {label}: {current}
      </span>
      <AdminIcon icon={ADMIN_ICONS.chevronDown} />
      <select
        aria-label={label}
        value={value}
        disabled={disabled}
        onChange={(e) => onChange(e.target.value)}
        className="absolute inset-0 cursor-pointer opacity-0 disabled:cursor-not-allowed"
      >
        {options.map((o) => (
          <option key={o.value} value={o.value}>
            {o.label}
          </option>
        ))}
      </select>
    </label>
  );
};

/** Hàng công cụ phía trên bảng (search + bộ lọc), cao 32px, gap 8. */
export const ToolbarRow: React.FC<{ children: React.ReactNode }> = ({ children }) => (
  <div className="flex flex-wrap items-center gap-2">{children}</div>
);
