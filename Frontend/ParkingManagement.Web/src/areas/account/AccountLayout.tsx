import { useState } from 'react';
import { NavLink, Outlet } from 'react-router-dom';
import { UserRound } from 'lucide-react';
import { avatarSource } from '@/api/authApi';
import { useAppSelector } from '@/store';
import { kycLabels } from './accountFormat';
import './account.css';

const links = [
  { to: '/account/profile', label: 'Hồ sơ cá nhân', icon: UserRound },
];

/** Khung chung cho mọi trang tài khoản: một cột điều hướng, nội dung bên phải. */
export function AccountLayout() {
  const user = useAppSelector(s => s.auth.user);
  const [broken, setBroken] = useState(false);
  const kyc = kycLabels[user?.kycStatus ?? ''];
  const avatar = avatarSource(user?.avatarUrl);

  return <div className="acct">
    <div className="acct-shell">
      <aside className="acct-rail">
        {user && <div className="acct-holder">
          {avatar && !broken
            ? <img className="acct-holder-avatar" src={avatar} alt="" referrerPolicy="no-referrer" onError={() => setBroken(true)} />
            : <span className="acct-holder-avatar" aria-hidden="true">{user.fullName.trim().charAt(0).toUpperCase()}</span>}
          <div className="min-w-0">
            <div className="acct-holder-name">{user.fullName}</div>
            {kyc && <div className="acct-holder-meta">{kyc.text}</div>}
          </div>
        </div>}
        <nav className="acct-nav" aria-label="Tài khoản của tôi">
          {links.map(({ to, label, icon: Icon }) => <NavLink key={to} to={to}>
            <Icon size={18} aria-hidden="true" />{label}
          </NavLink>)}
        </nav>
      </aside>
      <main className="acct-main"><Outlet /></main>
    </div>
  </div>;
}
