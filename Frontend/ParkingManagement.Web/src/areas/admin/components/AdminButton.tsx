import React from 'react';
import { AdminIcon } from './AdminIcon';
import { ADMIN_ICONS, type AdminIconName } from './adminIcons';

export type AdminButtonVariant = 'primary' | 'secondary' | 'destructive' | 'ghost' | 'onNavy';

interface AdminButtonProps extends React.ButtonHTMLAttributes<HTMLButtonElement> {
  variant?: AdminButtonVariant;
  icon?: AdminIconName;
  /** Đang xử lý: hiện spinner + loadingLabel thay nhãn, nút giữ nguyên độ rộng (hai nhãn chồng nhau trong cùng ô lưới). */
  loading?: boolean;
  loadingLabel?: React.ReactNode;
}

const VARIANT_CLASS: Record<AdminButtonVariant, string> = {
  primary: 'bg-(--admin-brand-navy-900) text-(--admin-text-on-navy) enabled:hover:bg-(--admin-brand-navy-800)',
  secondary:
    'border border-(--admin-border-default) bg-(--admin-surface-card) text-(--admin-brand-navy-900) enabled:hover:bg-(--admin-surface-selected)',
  destructive: 'bg-(--admin-status-danger) text-(--admin-text-on-navy) enabled:hover:brightness-95',
  ghost: 'text-(--admin-brand-navy-900) enabled:hover:bg-(--admin-surface-selected)',
  onNavy: 'border border-(--admin-text-on-navy) text-(--admin-text-on-navy) enabled:hover:bg-(--admin-brand-navy-900)',
};

/** Shell/Button (Figma 140:259): cao 32, radius 2, padding 0 12, Disabled = opacity 40%. Nhấn: scale --motion-press-scale. */
export const AdminButton: React.FC<AdminButtonProps> = ({
  variant = 'primary',
  icon,
  className,
  type = 'button',
  loading,
  loadingLabel,
  children,
  ...rest
}) => (
  <button
    type={type}
    aria-busy={loading || undefined}
    className={`adm-type-body-strong adm-press inline-flex h-8 shrink-0 items-center justify-center gap-1.5 rounded-[2px] px-3 whitespace-nowrap disabled:cursor-not-allowed disabled:opacity-40 ${VARIANT_CLASS[variant]} ${className ?? ''}`}
    {...rest}
  >
    {loadingLabel === undefined ? (
      <>
        {icon && <AdminIcon icon={ADMIN_ICONS[icon]} />}
        {children}
      </>
    ) : (
      <span className="grid items-center justify-items-center">
        <span className={`adm-tr-colors col-start-1 row-start-1 inline-flex items-center gap-1.5 ${loading ? 'opacity-0' : ''}`} aria-hidden={loading || undefined}>
          {icon && <AdminIcon icon={ADMIN_ICONS[icon]} />}
          {children}
        </span>
        <span className={`adm-tr-colors col-start-1 row-start-1 inline-flex items-center gap-1.5 ${loading ? '' : 'opacity-0'}`} aria-hidden={!loading || undefined}>
          {/* Spinner chỉ quay khi đang xử lý; lúc ẩn giữ ô trống cùng cỡ để độ rộng không đổi. */}
          <span className={loading ? 'adm-spinner' : 'inline-block size-3'} aria-hidden="true" />
          {loadingLabel}
        </span>
      </span>
    )}
  </button>
);
