import React from 'react';
import { AdminIcon } from './AdminIcon';
import { ADMIN_ICONS } from './adminIcons';

export type BannerTone = 'info' | 'warning' | 'danger' | 'success';

const TONE: Record<BannerTone, { box: string; text: string }> = {
  info: { box: 'border-(--admin-brand-navy-800) bg-(--admin-surface-selected)', text: 'text-(--admin-text-primary)' },
  warning: { box: 'border-(--admin-status-warning) bg-(--admin-surface-selected)', text: 'text-(--admin-status-warning)' },
  danger: { box: 'border-(--admin-status-danger) bg-(--admin-surface-danger)', text: 'text-(--admin-status-danger)' },
  success: { box: 'border-(--admin-status-success) bg-(--admin-surface-selected)', text: 'text-(--admin-status-success)' },
};

interface BannerProps {
  tone?: BannerTone;
  children: React.ReactNode;
  action?: React.ReactNode;
}

/** Banner/info|warning|danger|success (Figma): viền trái 3px, padding 10/12, gap 8, chữ body/regular-12. */
export const Banner: React.FC<BannerProps> = ({ tone = 'info', children, action }) => {
  const style = TONE[tone];
  return (
    <div className={`flex min-h-9 items-center gap-2 border-l-[3px] px-3 py-2.5 ${style.box}`} role={tone === 'danger' ? 'alert' : 'status'}>
      <span className={style.text}>
        <AdminIcon icon={tone === 'info' || tone === 'success' ? ADMIN_ICONS.info : ADMIN_ICONS.alert} />
      </span>
      <span className={`adm-type-body-regular-12 min-w-0 flex-1 ${style.text}`}>{children}</span>
      {action}
    </div>
  );
};
