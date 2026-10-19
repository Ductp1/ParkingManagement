import React, { useEffect, useRef, useState } from 'react';
import { Link } from 'react-router-dom';
import type { AdminSection, AdminTab } from '../navigation';
import type { NotificationDto, NotificationInboxDto } from '@/types/notification';
import type { AdminQueryResult } from '../hooks/useAdminQuery';
import { AdminIcon } from '../components/AdminIcon';
import { NotificationPanel } from './NotificationPanel';
import { ProfileMenu } from './ProfileMenu';
import iconBell from '../assets/icons/icon-bell.svg';
import avatarUser from '../assets/icons/avatar-user.svg';

interface AdminHeaderProps {
  section: AdminSection;
  activeTab: AdminTab | null;
  /** Nhãn màn chi tiết (breadcrumb cấp 3), ví dụ mã hồ sơ KYB. */
  detailLabel: string | null;
  inbox: AdminQueryResult<NotificationInboxDto>;
  onReadNotification: (n: NotificationDto) => void;
  onReadAll: () => void;
  profile: { name: string; email: string | null; roles: string; profilePath: string | null };
  onSignOut: () => void;
}

/** Shell/Header (Figma 113:624): hàng breadcrumb 52px + tab bar 40px; mọi section đều hiện tab bar. */
export const AdminHeader: React.FC<AdminHeaderProps> = ({ section, activeTab, detailLabel, inbox, onReadNotification, onReadAll, profile, onSignOut }) => {
  const [open, setOpen] = useState<'bell' | 'profile' | null>(null);
  const actionsRef = useRef<HTMLDivElement>(null);
  const unreadCount = inbox.data?.unreadCount ?? 0;
  const separator = <span className="text-(--admin-border-default)">/</span>;

  useEffect(() => {
    if (!open) return;
    const onDown = (e: MouseEvent) => {
      if (actionsRef.current && !actionsRef.current.contains(e.target as Node)) setOpen(null);
    };
    document.addEventListener('mousedown', onDown);
    return () => document.removeEventListener('mousedown', onDown);
  }, [open]);

  return (
    <header className="flex shrink-0 flex-col bg-(--admin-surface-card)">
      <div className="flex h-[52px] items-center justify-between border-b border-(--admin-border-default) px-4">
        <nav aria-label="Breadcrumb" className="adm-type-breadcrumb-default flex items-center gap-1.5 whitespace-nowrap text-(--admin-text-nav)">
          <span>{section.label}</span>
          {separator}
          {activeTab && detailLabel ? (
            <>
              <Link to={activeTab.path} className="hover:underline">
                {activeTab.label}
              </Link>
              {separator}
              <span className="adm-type-breadcrumb-current" aria-current="page">
                {detailLabel}
              </span>
            </>
          ) : (
            <span className="adm-type-breadcrumb-current" aria-current="page">
              {activeTab?.label ?? section.label}
            </span>
          )}
        </nav>

        <div ref={actionsRef} className="relative flex items-center gap-2">
          <button
            type="button"
            aria-label={unreadCount > 0 ? `Notifications, ${unreadCount} unread` : 'Notifications'}
            aria-expanded={open === 'bell'}
            onClick={() => setOpen((o) => (o === 'bell' ? null : 'bell'))}
            className="relative flex size-8 items-center justify-center rounded-[2px] border border-(--admin-border-default) text-(--admin-text-nav) hover:bg-(--admin-surface-selected)"
          >
            <AdminIcon icon={{ src: iconBell, width: 16, height: 16 }} />
            {unreadCount > 0 && <span className="absolute top-[4px] left-[19px] size-2 rounded-full bg-(--admin-status-danger)" />}
          </button>
          <button
            type="button"
            aria-label="Profile menu"
            aria-expanded={open === 'profile'}
            onClick={() => setOpen((o) => (o === 'profile' ? null : 'profile'))}
            className="flex size-8 items-center justify-center rounded-full bg-(--admin-brand-navy-900) text-(--admin-text-on-navy)"
          >
            <AdminIcon icon={{ src: avatarUser, width: 12, height: 12 }} />
          </button>
          {open === 'bell' && <NotificationPanel inbox={inbox} onRead={onReadNotification} onReadAll={onReadAll} />}
          {open === 'profile' && (
            <ProfileMenu
              {...profile}
              onClose={() => setOpen(null)}
              onSignOut={() => {
                setOpen(null);
                onSignOut();
              }}
            />
          )}
        </div>
      </div>

      <nav aria-label={`${section.label} tabs`} className="flex h-10 items-start gap-4 overflow-x-auto border-b border-(--admin-border-default) px-4">
        {section.tabs.map((tab) => {
          const isActive = tab.key === activeTab?.key;
          return (
            <Link
              key={tab.key}
              to={tab.path}
              aria-current={isActive ? 'page' : undefined}
              className={`flex h-[39px] shrink-0 items-center whitespace-nowrap ${
                isActive
                  ? 'adm-type-tab-active border-b-2 border-(--admin-brand-navy-900) text-(--admin-brand-navy-900)'
                  : 'adm-type-tab-default text-(--admin-text-nav) hover:text-(--admin-brand-navy-900)'
              }`}
            >
              {tab.label}
            </Link>
          );
        })}
      </nav>
    </header>
  );
};
