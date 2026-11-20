import React, { useEffect, useId, useRef, useState } from 'react';

export interface MoreActionItem {
  key: string;
  label: string;
  /** Hành động không hoàn tác: chữ màu danger. */
  destructive?: boolean;
  disabled?: boolean;
  onSelect: () => void;
}

/**
 * Menu "More actions" ở header trang chi tiết: nút mở + danh sách role="menu". Đóng bằng Escape / bấm ngoài / chọn mục; mũi tên lên xuống
 * di chuyển giữa các mục; focus trả về nút khi đóng.
 */
export const MoreActionsMenu: React.FC<{ items: MoreActionItem[]; label?: string }> = ({ items, label = 'More actions' }) => {
  const [open, setOpen] = useState(false);
  const rootRef = useRef<HTMLDivElement>(null);
  const buttonRef = useRef<HTMLButtonElement>(null);
  const menuId = useId();

  useEffect(() => {
    if (!open) return undefined;
    const onPointer = (e: MouseEvent) => {
      if (!rootRef.current?.contains(e.target as Node)) setOpen(false);
    };
    document.addEventListener('mousedown', onPointer);
    return () => document.removeEventListener('mousedown', onPointer);
  }, [open]);

  useEffect(() => {
    if (open) rootRef.current?.querySelector<HTMLElement>('[role="menuitem"]:not(:disabled)')?.focus();
  }, [open]);

  const close = () => {
    setOpen(false);
    buttonRef.current?.focus();
  };

  const onKeyDown = (e: React.KeyboardEvent) => {
    if (e.key === 'Escape') {
      e.preventDefault();
      close();
      return;
    }
    if (e.key !== 'ArrowDown' && e.key !== 'ArrowUp') return;
    e.preventDefault();
    const nodes = Array.from(rootRef.current?.querySelectorAll<HTMLElement>('[role="menuitem"]:not(:disabled)') ?? []);
    const index = nodes.indexOf(document.activeElement as HTMLElement);
    const next = e.key === 'ArrowDown' ? (index + 1) % nodes.length : (index - 1 + nodes.length) % nodes.length;
    nodes[next]?.focus();
  };

  if (items.length === 0) return null;
  return (
    <div ref={rootRef} className="relative" onKeyDown={onKeyDown}>
      <button
        ref={buttonRef}
        type="button"
        aria-haspopup="menu"
        aria-expanded={open}
        aria-controls={open ? menuId : undefined}
        onClick={() => setOpen((v) => !v)}
        className="adm-type-body-strong inline-flex h-8 items-center gap-1.5 rounded-[2px] border border-(--admin-border-default) bg-(--admin-surface-card) px-3 text-(--admin-brand-navy-900) hover:bg-(--admin-surface-selected)"
      >
        {label}
        <span aria-hidden="true">▾</span>
      </button>
      {open && (
        <div
          id={menuId}
          role="menu"
          className="absolute right-0 z-30 mt-1 flex min-w-44 flex-col rounded-[2px] border border-(--admin-border-default) bg-(--admin-surface-card) py-1 shadow-md"
        >
          {items.map((item) => (
            <button
              key={item.key}
              type="button"
              role="menuitem"
              disabled={item.disabled}
              onClick={() => {
                setOpen(false);
                item.onSelect();
              }}
              className={`adm-type-body-default px-3 py-2 text-left hover:bg-(--admin-surface-selected) focus-visible:bg-(--admin-surface-selected) focus-visible:outline-none disabled:cursor-not-allowed disabled:opacity-40 ${
                item.destructive ? 'text-(--admin-status-danger)' : 'text-(--admin-text-primary)'
              }`}
            >
              {item.label}
            </button>
          ))}
        </div>
      )}
    </div>
  );
};
