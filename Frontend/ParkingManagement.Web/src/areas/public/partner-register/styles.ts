/** Class dùng chung của trang Đăng ký đối tác (token --admin-*, xem admin.css). */

export const inputClass = (error?: string | null) =>
  `h-11 w-full min-w-0 rounded-lg border px-3 text-sm text-(--admin-text-primary) placeholder:text-(--admin-text-placeholder) outline-none transition-colors focus-visible:border-(--admin-brand-navy-900) focus-visible:ring-2 focus-visible:ring-(--admin-brand-navy-900)/25 read-only:bg-(--admin-surface-row-alt) disabled:cursor-not-allowed disabled:bg-(--admin-surface-row-alt) disabled:text-(--admin-text-secondary) ${
    error ? 'border-(--admin-status-danger) bg-(--admin-surface-danger)' : 'border-(--admin-border-default) bg-(--admin-surface-card)'
  }`;

export const buttonClass = (variant: 'primary' | 'secondary' | 'ghost' = 'primary') =>
  `inline-flex h-10 shrink-0 items-center justify-center gap-2 rounded-lg px-4 text-sm font-semibold whitespace-nowrap transition-colors outline-none focus-visible:ring-2 focus-visible:ring-(--admin-brand-navy-900) focus-visible:ring-offset-2 disabled:cursor-not-allowed ${
    variant === 'primary'
      ? 'bg-(--admin-brand-navy-900) text-(--admin-text-on-navy) enabled:hover:bg-(--admin-brand-navy-800) disabled:bg-(--admin-chart-neutral)'
      : variant === 'secondary'
        ? 'border border-(--admin-border-default) bg-(--admin-surface-card) text-(--admin-brand-navy-900) enabled:hover:bg-(--admin-surface-selected) disabled:opacity-50'
        : 'text-(--admin-brand-navy-900) enabled:hover:bg-(--admin-surface-selected) disabled:opacity-50'
  }`;

export const messageId = (id: string) => `${id}-msg`;
