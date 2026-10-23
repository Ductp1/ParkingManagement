import React from 'react';
import { AdminIcon } from './AdminIcon';
import { ADMIN_ICONS, type AdminIconName } from './adminIcons';

export type AdminButtonVariant = 'primary' | 'secondary' | 'destructive' | 'ghost' | 'onNavy';

interface AdminButtonProps extends React.ButtonHTMLAttributes<HTMLButtonElement> {
  variant?: AdminButtonVariant;
  icon?: AdminIconName;
}

const VARIANT_CLASS: Record<AdminButtonVariant, string> = {
  primary: 'bg-(--admin-brand-navy-900) text-(--admin-text-on-navy) enabled:hover:bg-(--admin-brand-navy-800)',
  secondary:
    'border border-(--admin-border-default) bg-(--admin-surface-card) text-(--admin-brand-navy-900) enabled:hover:bg-(--admin-surface-selected)',
  destructive: 'bg-(--admin-status-danger) text-(--admin-text-on-navy) enabled:hover:brightness-95',
  ghost: 'text-(--admin-brand-navy-900) enabled:hover:bg-(--admin-surface-selected)',
  onNavy: 'border border-(--admin-text-on-navy) text-(--admin-text-on-navy) enabled:hover:bg-(--admin-brand-navy-900)',
};

/** Shell/Button (Figma 140:259): cao 32, radius 2, padding 0 12, Disabled = opacity 40%. */
export const AdminButton: React.FC<AdminButtonProps> = ({
  variant = 'primary',
  icon,
  className,
  type = 'button',
  children,
  ...rest
}) => (
  <button
    type={type}
    className={`adm-type-body-strong inline-flex h-8 shrink-0 items-center justify-center gap-1.5 rounded-[2px] px-3 whitespace-nowrap disabled:cursor-not-allowed disabled:opacity-40 ${VARIANT_CLASS[variant]} ${className ?? ''}`}
    {...rest}
  >
    {icon && <AdminIcon icon={ADMIN_ICONS[icon]} />}
    {children}
  </button>
);
