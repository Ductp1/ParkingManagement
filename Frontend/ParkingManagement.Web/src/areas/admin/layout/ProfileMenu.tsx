import React from 'react';
import { Link } from 'react-router-dom';
import { AdminIcon } from '../components/AdminIcon';
import { ADMIN_ICONS } from '../components/adminIcons';

interface ProfileMenuProps {
  name: string;
  email: string | null;
  roles: string;
  profilePath: string | null;
  onSignOut: () => void;
  onClose: () => void;
}

/** Shell/Profile menu (Figma 142:356): 240 rộng – tên, email · vai trò, My profile, Sign out (status/danger). */
export const ProfileMenu: React.FC<ProfileMenuProps> = ({ name, email, roles, profilePath, onSignOut, onClose }) => (
  <div className="adm-dropdown absolute top-10 right-0 z-40 flex w-[240px] flex-col rounded-[4px] border border-(--admin-border-default) bg-(--admin-surface-card) shadow-[0_4px_12px_rgb(0_0_0/0.12)]">
    <div className="flex flex-col gap-0.5 border-b border-(--admin-border-default) p-3">
      <span className="adm-type-body-strong-13 text-(--admin-text-primary)">{name}</span>
      <span className="adm-type-body-small text-(--admin-text-secondary)">{[email, roles].filter(Boolean).join(' · ')}</span>
    </div>
    {profilePath && (
      <Link to={profilePath} onClick={onClose} className="adm-type-nav-item adm-tr-colors flex h-9 items-center gap-2 px-3 text-(--admin-text-nav) hover:bg-(--admin-surface-selected)">
        <AdminIcon icon={ADMIN_ICONS.user} />
        My profile
      </Link>
    )}
    <button type="button" onClick={onSignOut} className="adm-type-nav-item adm-tr-colors flex h-9 items-center gap-2 px-3 text-left text-(--admin-status-danger) hover:bg-(--admin-surface-danger)">
      <AdminIcon icon={ADMIN_ICONS.logOut} />
      Sign out
    </button>
  </div>
);
