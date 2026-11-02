import React, { useRef } from 'react';
import { Link } from 'react-router-dom';
import { useSlidingIndicator } from '../hooks/useSlidingIndicator';
import { ADMIN_SECTIONS, type AdminSection } from '../navigation';
import { AdminIcon } from '../components/AdminIcon';
import avatarUser from '../assets/icons/avatar-user.svg';

interface AdminSidebarProps {
  activeSection: AdminSection;
  userName: string;
  userEmail?: string;
}

/** Shell/Sidebar (Figma 113:618): rộng 220, cao bằng màn hình; Nav co giãn, thẻ người dùng ghim dưới cùng. */
export const AdminSidebar: React.FC<AdminSidebarProps> = ({ activeSection, userName, userEmail }) => {
  const navRef = useRef<HTMLElement>(null);
  const indicatorRef = useRef<HTMLSpanElement>(null);
  // Khối nền navy của mục active trượt giữa các section (spring nhẹ), chữ đổi màu bằng transition.
  useSlidingIndicator(navRef, indicatorRef, 'y', activeSection.key);

  return (
    <aside className="sticky top-0 flex h-screen w-[220px] shrink-0 flex-col border-r border-(--admin-border-default) bg-(--admin-surface-card)">
      <div className="flex h-12 shrink-0 items-center border-b border-(--admin-border-default) px-4">
        <span className="adm-type-nav-logo uppercase text-(--admin-brand-navy-900)">Smart Parking Admin</span>
      </div>

      <nav ref={navRef} aria-label="Admin sections" className="relative flex min-h-0 flex-1 flex-col gap-0.5 overflow-auto px-1 py-1.5">
        <span ref={indicatorRef} aria-hidden="true" className="adm-indicator rounded-[2px] bg-(--admin-brand-navy-800)" />
        {ADMIN_SECTIONS.map((section) => {
          const isActive = section.key === activeSection.key;
          return (
            <Link
              key={section.key}
              to={section.tabs[0].path}
              aria-current={isActive ? 'page' : undefined}
              className={`adm-type-nav-item adm-tr-colors relative flex items-center rounded-[2px] px-2 py-1.5 whitespace-nowrap ${
                isActive ? 'text-(--admin-text-on-navy)' : 'text-(--admin-text-nav) hover:bg-(--admin-surface-selected)'
              }`}
            >
              <AdminIcon icon={section.icon} />
              {section.label}
            </Link>
          );
        })}
      </nav>

      <div className="flex shrink-0 items-center gap-2 border-t border-(--admin-border-default) bg-(--admin-surface-selected) px-2 pt-[9px] pb-2">
        <span className="flex size-8 shrink-0 items-center justify-center rounded-full bg-(--admin-brand-navy-900) text-(--admin-text-on-navy)">
          <AdminIcon icon={{ src: avatarUser, width: 12, height: 12 }} />
        </span>
        <div className="flex min-w-0 flex-1 flex-col">
          <span className="adm-type-body-strong-13 truncate text-(--admin-text-primary)">{userName}</span>
          {userEmail && <span className="adm-type-body-regular-12 truncate text-(--admin-text-nav)">{userEmail}</span>}
        </div>
      </div>
    </aside>
  );
};
