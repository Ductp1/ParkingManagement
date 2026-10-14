import { logoutSession } from '@/api/authApi';
import React from 'react';
import { NavLink, useNavigate, useLocation } from 'react-router-dom';
import { useAppSelector, useAppDispatch } from '@/store';
import { switchRole, logout } from '@/store/slices/authSlice';
import type { UserRole } from '@/types/auth';

export const MainNavbar: React.FC = () => {
  const { user } = useAppSelector((state) => state.auth);
  const dispatch = useAppDispatch();
  const navigate = useNavigate();
  const location = useLocation();
  const [logoutError, setLogoutError] = React.useState('');
  const [loggingOut, setLoggingOut] = React.useState(false);

  const handleRoleChange = (e: React.ChangeEvent<HTMLSelectElement>) => {
    const role = e.target.value as UserRole;
    dispatch(switchRole(role));
    if (role === 'Driver') navigate('/driver/vehicles');
    else if (role === 'Owner') navigate('/owner');
    else if (role === 'Staff') navigate('/gate');
    else if (role === 'Admin') navigate('/admin');
  };

  const navItems = [
    { label: 'Trang chủ', path: '/driver' },
    { label: 'Bãi đỗ & Sơ đồ', path: '/driver/parking-lots' },
    { label: 'Xe của tôi', path: '/driver/vehicles' },
    { label: 'Lịch sử đặt', path: '/driver/history' },
    { label: 'Hỗ trợ', path: '/driver/support' },
  ];

  return (
    <header className="bg-white border-b border-gray-100 sticky top-0 z-50 w-full shadow-xs">
      <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 h-16 flex items-center justify-between">
        {/* Brand Logo */}
        <div className="flex items-center gap-6">
          <NavLink to="/driver" className="flex items-center gap-2">
            <div className="w-10 h-10 rounded-lg bg-blue-600 text-white flex items-center justify-center text-xl font-bold shadow-xs">
              P
            </div>
            <div className="flex flex-col">
              <span className="font-bold text-slate-900 text-lg leading-tight tracking-tight">ParkMaster</span>
              <span className="text-[10px] text-slate-500 font-medium tracking-wider uppercase">SMART PARKING SYSTEM</span>
            </div>
          </NavLink>

          {/* Navigation Links for Driver Portal */}
          <nav className="hidden lg:flex items-center gap-1 ml-4">
            {navItems.map((item) => {
              const isActive = location.pathname === item.path;
              return (
                <NavLink
                  key={item.path}
                  to={item.path}
                  className={`font-medium px-3.5 py-1.5 transition-colors rounded-lg text-sm ${
                    isActive
                      ? 'bg-blue-50 text-blue-700 font-semibold shadow-xs'
                      : 'text-slate-600 hover:text-blue-600 hover:bg-slate-50'
                  }`}
                >
                  {item.label}
                </NavLink>
              );
            })}
          </nav>
        </div>

        {/* Right Action: Portal Switcher + Notification + Profile */}
        <div className="flex items-center gap-4">
          {/* Quick Portal Switcher (Sprint Demo Feature for 4 areas) */}
          <div className="hidden md:flex items-center gap-1.5 bg-slate-100 px-2.5 py-1 rounded-lg border border-slate-200">
            <span className="text-xs text-slate-500 font-medium">Phân hệ:</span>
            <select
              value={user?.role || 'Driver'}
              onChange={handleRoleChange}
              className="text-xs font-semibold text-slate-800 bg-transparent border-none focus:outline-hidden cursor-pointer"
            >
              {(user?.roles ?? []).map(role => <option key={role} value={role}>{role}</option>)}
            </select>
          </div>

          <button type="button" disabled={loggingOut} className="text-sm text-blue-600" onClick={async () => {
            setLogoutError(''); setLoggingOut(true);
            try { await logoutSession(); dispatch(logout()); navigate('/login', { replace: true }); }
            catch (error) { setLogoutError(error instanceof Error ? error.message : 'Đăng xuất thất bại.'); }
            finally { setLoggingOut(false); }
          }}>{loggingOut ? 'Đang đăng xuất…' : 'Đăng xuất'}</button>
          {logoutError && <span role="alert" className="text-sm text-red-600">{logoutError}</span>}
          {/* Notifications */}
          <button
            className="relative p-2 rounded-lg text-slate-500 hover:bg-slate-100 hover:text-slate-700 transition-colors"
            type="button"
            aria-label="Thông báo"
          >
            <span className="material-symbols-outlined text-[24px]">notifications</span>
            <span className="absolute top-1.5 right-1.5 w-2.5 h-2.5 rounded-full bg-red-600 ring-2 ring-white"></span>
          </button>

          {/* User Profile Info */}
          <div className="flex items-center gap-2.5 pl-2 border-l border-slate-200">
            <div className="flex flex-col items-end hidden sm:flex">
              <span className="text-sm font-semibold text-slate-900">{user?.fullName || ''}</span>
              <div className="flex items-center gap-1">
                <span className="text-[11px] font-bold text-emerald-600 tracking-wider">• THÀNH VIÊN VIP</span>
              </div>
            </div>
            <div className="w-9 h-9 rounded-full bg-blue-700 flex items-center justify-center text-white font-semibold text-sm shadow-xs">
              <span className="material-symbols-outlined text-[20px]">person</span>
            </div>
          </div>
        </div>
      </div>
    </header>
  );
};
