import React, { useEffect, useState } from 'react';
import { NavLink, useNavigate, useLocation } from 'react-router-dom';
import { useAppSelector, useAppDispatch } from '@/store';
import { avatarSource, logoutSession } from '@/api/authApi';
import { logout } from '@/store/slices/authSlice';
import { fetchNotificationInbox, markNotificationAsRead } from '@/store/slices/notificationSlice';
import { notificationTargetRoute } from '@/utils/notificationBooking';
import type { NotificationDto } from '@/types/notification';
import type { UserRole } from '@/types/auth';
import './navbar.css';

/** Icon + màu theo loại thông báo (templateKey do NotificationService sinh ra). */
const NOTIFICATION_ICONS: Record<string, { icon: string; color: string }> = {
  BOOKING_CONFIRMED: { icon: 'event_available', color: 'bg-emerald-100 text-emerald-600' },
  BOOKING_REMINDER: { icon: 'alarm', color: 'bg-amber-100 text-amber-600' },
  BOOKING_CANCELLED: { icon: 'event_busy', color: 'bg-red-100 text-red-600' },
  BOOKING_MODIFIED: { icon: 'edit_calendar', color: 'bg-blue-100 text-blue-600' },
  PAYMENT_SUCCEEDED: { icon: 'payments', color: 'bg-blue-100 text-blue-600' },
  PAYMENT_FAILED: { icon: 'money_off', color: 'bg-red-100 text-red-600' },
  COMPLAINT_CREATED: { icon: 'support_agent', color: 'bg-orange-100 text-orange-600' },
  COMPLAINT_RESOLVED: { icon: 'task_alt', color: 'bg-emerald-100 text-emerald-600' },
};
const DEFAULT_NOTIFICATION_ICON = { icon: 'notifications', color: 'bg-slate-100 text-slate-600' };

const formatTimeAgo = (isoUtc: string): string => {
  const minutes = Math.floor((Date.now() - new Date(isoUtc).getTime()) / 60000);
  if (minutes < 1) return 'Vừa xong';
  if (minutes < 60) return `${minutes} phút trước`;
  const hours = Math.floor(minutes / 60);
  if (hours < 24) return `${hours} giờ trước`;
  return `${Math.floor(hours / 24)} ngày trước`;
};

interface RolePortalItem {
  id: UserRole;
  label: string;
  subtitle: string;
  path: string;
  icon: string;
  badgeClass: string;
  subActiveClass: string;
}

const ROLE_PORTALS: RolePortalItem[] = [
  {
    id: 'Driver',
    label: 'Tài xế',
    subtitle: 'Cổng Dịch Vụ Khách Hàng',
    path: '/driver/find-lot',
    icon: 'directions_car',
    badgeClass: 'bg-blue-100 text-blue-700',
    subActiveClass: 'bg-blue-50 text-blue-700 font-bold',
  },
  {
    id: 'Owner',
    label: 'Chủ bãi',
    subtitle: 'Cổng Đối Tác Chủ Bãi',
    path: '/owner',
    icon: 'domain',
    badgeClass: 'bg-blue-100 text-blue-700',
    subActiveClass: 'bg-blue-50 text-blue-700 font-bold',
  },
  {
    id: 'Staff',
    label: 'Trực cổng',
    subtitle: 'Trạm Soát Vé Barie (Gate Console)',
    path: '/gate',
    icon: 'fence',
    badgeClass: 'bg-amber-100 text-amber-800',
    subActiveClass: 'bg-amber-50 text-amber-800 font-bold',
  },
  {
    id: 'Admin',
    label: 'Quản trị',
    subtitle: 'Hệ Thống Quản Trị Trung Tâm',
    path: '/admin',
    icon: 'admin_panel_settings',
    badgeClass: 'bg-purple-100 text-purple-700',
    subActiveClass: 'bg-purple-50 text-purple-700 font-bold',
  },
];

interface SubNavItem {
  label: string;
  path: string;
  icon: string;
}

const PORTAL_SUB_NAVS: Record<UserRole, SubNavItem[]> = {
  // Flow Tài xế tinh gọn: 4 tính năng cốt lõi
  Driver: [
    { label: 'Tìm bãi đỗ xe', path: '/driver/find-lot', icon: 'near_me' },
    { label: 'Xe của tôi', path: '/driver/vehicles', icon: 'directions_car' },
    { label: 'Lịch sử đặt', path: '/driver/history', icon: 'receipt_long' },
    { label: 'Hỗ trợ CSKH', path: '/driver/support', icon: 'support_agent' },
  ],
  Owner: [
    { label: 'Tổng quan & Doanh thu', path: '/owner', icon: 'analytics' },
    { label: 'Bãi đỗ & Sơ đồ ô', path: '/owner/lots', icon: 'view_quilt' },
    { label: 'Bảng giá & Thu phí', path: '/owner/pricing', icon: 'payments' },
    { label: 'Khiếu nại & Đánh giá', path: '/owner/feedback', icon: 'rate_review' },
  ],
  Staff: [
    { label: 'Bàn điều khiển Barie', path: '/gate', icon: 'tune' },
    { label: 'Làn vào (ANPR Inbound)', path: '/gate/inbound', icon: 'login' },
    { label: 'Làn ra (ANPR Outbound)', path: '/gate/outbound', icon: 'logout' },
    { label: 'Soát vé thủ công', path: '/gate/manual', icon: 'edit_square' },
    { label: 'Nhật ký trạm', path: '/gate/events', icon: 'history' },
  ],
  Admin: [
    { label: 'Tổng quan Dashboard', path: '/admin', icon: 'grid_view' },
    { label: 'Quản lý tài khoản', path: '/admin/users', icon: 'manage_accounts' },
    { label: 'Phê duyệt KYB', path: '/admin/kyb-approvals', icon: 'fact_check' },
    { label: 'Cấu hình tham số', path: '/admin/parameters', icon: 'settings' },
    { label: 'Feature Toggles', path: '/admin/toggles', icon: 'toggle_on' },
    { label: 'Đối soát tài chính', path: '/admin/settlements', icon: 'account_balance' },
  ],
};

const ROLE_BADGE_META: Record<UserRole, { label: string; color: string }> = {
  Driver: { label: 'Tài xế', color: 'text-blue-600' },
  Owner: { label: 'Chủ bãi', color: 'text-blue-600' },
  Staff: { label: 'Nhân viên cổng', color: 'text-amber-600' },
  Admin: { label: 'Quản trị viên', color: 'text-purple-600' },
};

export const MainNavbar: React.FC = () => {
  const { user } = useAppSelector((state) => state.auth);
  const dispatch = useAppDispatch();
  const navigate = useNavigate();
  const location = useLocation();

  const [logoutError, setLogoutError] = useState('');
  const [loggingOut, setLoggingOut] = useState(false);
  const [isMobileMenuOpen, setIsMobileMenuOpen] = useState(false);
  const [isUserMenuOpen, setIsUserMenuOpen] = useState(false);

  // Xác định phân hệ đang active từ URL
  const getActiveRole = (pathname: string): UserRole => {
    if (pathname.startsWith('/admin')) return 'Admin';
    if (pathname.startsWith('/owner')) return 'Owner';
    if (pathname.startsWith('/gate')) return 'Staff';
    return 'Driver';
  };

  const activeRole = getActiveRole(location.pathname);
  const activePortal = ROLE_PORTALS.find((p) => p.id === activeRole) || ROLE_PORTALS[0];
  const currentSubNavs = PORTAL_SUB_NAVS[activeRole] || PORTAL_SUB_NAVS.Driver;

  const userRoles: UserRole[] = user?.roles && user.roles.length > 0 ? user.roles : (user ? [user.role] : ['Driver', 'Owner']);
  const authorizedPortals = ROLE_PORTALS.filter(
    (portal) => !user || userRoles.includes('Admin') || userRoles.includes(portal.id)
  );

  // ===== Hộp thông báo (NotificationService) =====
  const { items: notifications, unreadCount, isLoading: isNotificationLoading } = useAppSelector(
    (state) => state.notification
  );
  const [isNotificationOpen, setIsNotificationOpen] = useState(false);
  const userId = user?.id || 5;

  useEffect(() => {
    dispatch(fetchNotificationInbox(userId));
  }, [dispatch, userId]);

  const handleToggleNotifications = () => {
    const next = !isNotificationOpen;
    setIsNotificationOpen(next);
    if (next) dispatch(fetchNotificationInbox(userId));
  };

  const handleNotificationClick = (notification: NotificationDto) => {
    if (!notification.isRead) {
      dispatch(markNotificationAsRead({ notificationId: notification.id, userId }));
    }
    setIsNotificationOpen(false);
    navigate(notificationTargetRoute(notification.dataJson));
  };

  const handleMarkAllAsRead = async () => {
    for (const n of notifications.filter((item) => !item.isRead)) {
      await dispatch(markNotificationAsRead({ notificationId: n.id, userId })).unwrap();
    }
  };

  return (
    <header className="pm-navbar bg-white border-b border-slate-200 sticky top-0 z-50 w-full shadow-2xs">
      {/* ===== BRAND LOGO + TRUY CẬP NHANH CÁC ROLE QUA URL + USER DROPDOWN ===== */}
      <div className="pm-navbar-shell pm-navbar-top flex items-center justify-between">
        {/* Brand Logo & Desktop Portal Switcher */}
        <div className="flex items-center gap-2 sm:gap-4 min-w-0">
          <NavLink to={activePortal.path} className="pm-navbar-brand flex items-center gap-2.5 group">
            <div className="w-8 h-8 sm:w-9 sm:h-9 shrink-0 rounded-xl bg-blue-600 text-white flex items-center justify-center text-lg font-black shadow-xs group-hover:scale-105 transition-transform">
              P
            </div>
            <div className="flex flex-col">
              <span className="font-extrabold text-slate-900 text-base sm:text-lg leading-tight tracking-tight group-hover:text-blue-600 transition-colors">
                ParkMaster
              </span>
              <span className="hidden sm:inline text-[10px] text-slate-400 font-semibold tracking-wider uppercase">
                {activePortal.subtitle}
              </span>
            </div>
          </NavLink>

          {/* Desktop Portal Switcher: Chuyển nhanh giữa Tài xế & Chủ bãi (chỉ hiển thị icon) */}
          {authorizedPortals.length > 1 && (
            <div className="hidden lg:flex items-center gap-1 p-1 bg-slate-100 rounded-xl border border-slate-200/80 shadow-2xs ml-2">
              {authorizedPortals.map((portal) => {
                const isCurrent = portal.id === activeRole;
                return (
                  <NavLink
                    key={portal.id}
                    to={portal.path}
                    title={portal.label}
                    aria-label={portal.label}
                    className={`w-8 h-8 flex items-center justify-center rounded-lg transition-all ${
                      isCurrent
                        ? 'bg-white text-blue-700 shadow-xs'
                        : 'text-slate-500 hover:text-slate-900 hover:bg-white/60'
                    }`}
                  >
                    <span className="material-symbols-outlined text-[18px]">{portal.icon}</span>
                  </NavLink>
                );
              })}
            </div>
          )}
        </div>

        {/* Menu Điều hướng Desktop: 4 mục cốt lõi, không thanh cuộn xám */}
        <div className="pm-navbar-desktop-menu">
          <nav aria-label={`Chức năng ${activePortal.label}`} className="pm-navbar-menu">
            {currentSubNavs.map((sub) => {
              const isSubActive =
                location.pathname === sub.path ||
                (sub.path !== activePortal.path && location.pathname.startsWith(sub.path));

              return (
                <NavLink
                  key={sub.path}
                  to={sub.path}
                  end={sub.path === activePortal.path}
                  className={`pm-navbar-menu-link flex items-center gap-2 px-3.5 py-1.5 rounded-xl text-xs transition-all ${
                    isSubActive
                      ? activePortal.subActiveClass
                      : 'text-slate-600 hover:text-slate-900 hover:bg-slate-100 font-medium'
                  }`}
                >
                  <span className="material-symbols-outlined text-[17px]">{sub.icon}</span>
                  <span>{sub.label}</span>
                </NavLink>
              );
            })}
          </nav>
        </div>

        {/* Cụm chức năng bên phải: Thông báo + User Profile Dropdown */}
        <div className="pm-navbar-actions flex items-center gap-2 sm:gap-3 shrink-0">
          {/* Hộp Thông báo Realtime */}
          <div className="relative">
            <button
              onClick={handleToggleNotifications}
              className="relative p-2 rounded-xl text-slate-500 hover:bg-slate-100 hover:text-slate-700 transition-colors cursor-pointer"
              title="Thông báo"
            >
              <span className="material-symbols-outlined text-[20px]">notifications</span>
              {unreadCount > 0 && (
                <span className="absolute top-1 right-1 min-w-[16px] h-[16px] px-1 rounded-full bg-red-600 text-white text-[9px] font-bold flex items-center justify-center ring-2 ring-white">
                  {unreadCount > 9 ? '9+' : unreadCount}
                </span>
              )}
            </button>

            {isNotificationOpen && (
              <div className="fixed inset-0 z-40" onClick={() => setIsNotificationOpen(false)} />
            )}

            {isNotificationOpen && (
              <div className="pm-navbar-notifications absolute right-0 top-full mt-2 w-[380px] max-w-[calc(100vw-2rem)] bg-white rounded-2xl shadow-2xl border border-slate-200 z-50 overflow-hidden">
                <div className="px-4 py-3 border-b border-slate-100 flex items-center justify-between">
                  <div className="flex items-center gap-2">
                    <h3 className="text-sm font-bold text-slate-900">Thông báo</h3>
                    {unreadCount > 0 && (
                      <span className="px-2 py-0.5 rounded-full bg-blue-100 text-blue-700 text-[10px] font-bold">
                        {unreadCount} chưa đọc
                      </span>
                    )}
                  </div>
                  <div className="flex items-center gap-1">
                    <button
                      type="button"
                      title="Làm mới"
                      onClick={() => dispatch(fetchNotificationInbox(userId))}
                      className="p-1.5 rounded-lg text-slate-400 hover:text-blue-600 hover:bg-blue-50 cursor-pointer"
                    >
                      <span className="material-symbols-outlined text-[18px]">refresh</span>
                    </button>
                    {unreadCount > 0 && (
                      <button
                        type="button"
                        onClick={handleMarkAllAsRead}
                        className="text-[11px] font-bold text-blue-600 hover:text-blue-700 px-2 py-1 rounded-lg hover:bg-blue-50 cursor-pointer"
                      >
                        Đánh dấu tất cả
                      </button>
                    )}
                  </div>
                </div>

                <div className="max-h-96 overflow-y-auto">
                  {isNotificationLoading && notifications.length === 0 ? (
                    <div className="py-10 flex flex-col items-center text-slate-400">
                      <span className="material-symbols-outlined text-[32px] animate-spin">progress_activity</span>
                      <span className="text-xs font-medium mt-2">Đang tải thông báo...</span>
                    </div>
                  ) : notifications.length === 0 ? (
                    <div className="py-10 flex flex-col items-center text-slate-400">
                      <span className="material-symbols-outlined text-[40px]">notifications_off</span>
                      <span className="text-xs font-medium mt-2">Chưa có thông báo nào</span>
                    </div>
                  ) : (
                    notifications.map((n) => {
                      const style = NOTIFICATION_ICONS[n.templateKey] || DEFAULT_NOTIFICATION_ICON;
                      return (
                        <button
                          key={n.id}
                          type="button"
                          onClick={() => handleNotificationClick(n)}
                          className={`w-full text-left px-4 py-3 flex gap-3 border-b border-slate-100 last:border-0 transition-colors cursor-pointer ${
                            n.isRead ? 'hover:bg-slate-50' : 'bg-blue-50/60 hover:bg-blue-50'
                          }`}
                        >
                          <div className={`w-9 h-9 rounded-xl flex items-center justify-center shrink-0 ${style.color}`}>
                            <span className="material-symbols-outlined text-[19px]">{style.icon}</span>
                          </div>
                          <div className="flex-1 min-w-0">
                            <div className="flex items-center justify-between gap-2">
                              <span className={`text-[13px] font-bold truncate ${n.isRead ? 'text-slate-700' : 'text-slate-900'}`}>
                                {n.title}
                              </span>
                              {!n.isRead && <span className="w-2 h-2 rounded-full bg-blue-600 shrink-0"></span>}
                            </div>
                            <p className="text-xs text-slate-500 line-clamp-2 mt-0.5">{n.body}</p>
                            <span className="text-[10px] text-slate-400 font-medium mt-1 block">
                              {formatTimeAgo(n.createdAtUtc)} • {n.channel}
                            </span>
                          </div>
                        </button>
                      );
                    })
                  )}
                </div>

                <div className="px-4 py-2 bg-slate-50 border-t border-slate-100 text-center">
                  <span className="text-[10px] text-slate-400 font-medium">
                    Hệ thống thông báo thời gian thực ParkMaster
                  </span>
                </div>
              </div>
            )}
          </div>

          {/* User Profile Dropdown: Gọn gàng, sạch bóng, có liên kết KYC & Logout */}
          <div className="relative">
            <button
              type="button"
              onClick={() => setIsUserMenuOpen(!isUserMenuOpen)}
              className="flex items-center gap-2 p-1 rounded-xl hover:bg-slate-100 transition-colors cursor-pointer border border-transparent hover:border-slate-200"
              aria-expanded={isUserMenuOpen}
              aria-label="Menu tài khoản"
            >
              <div className="flex flex-col items-end hidden sm:flex leading-tight pr-0.5">
                <span className="text-xs font-bold text-slate-900 truncate max-w-[130px]">
                  {user?.fullName || 'Tài khoản'}
                </span>
                <span className={`text-[10px] font-semibold ${ROLE_BADGE_META[activeRole]?.color || 'text-slate-500'}`}>
                  {ROLE_BADGE_META[activeRole]?.label || activeRole}
                </span>
              </div>

              <div className="relative w-8 h-8 rounded-lg bg-blue-600 text-white flex items-center justify-center font-bold text-xs shadow-xs">
                {user?.avatarUrl ? (
                  <img
                    src={avatarSource(user.avatarUrl)}
                    alt=""
                    className="absolute inset-0 w-8 h-8 rounded-lg object-cover"
                    referrerPolicy="no-referrer"
                    onError={(e) => {
                      e.currentTarget.style.display = 'none';
                    }}
                  />
                ) : (
                  <span className="material-symbols-outlined text-[18px]">person</span>
                )}
              </div>

              <span className="material-symbols-outlined text-[16px] text-slate-400 hidden sm:inline">
                {isUserMenuOpen ? 'expand_less' : 'expand_more'}
              </span>
            </button>

            {/* Click outside to close user dropdown */}
            {isUserMenuOpen && (
              <div className="fixed inset-0 z-40" onClick={() => setIsUserMenuOpen(false)} />
            )}

            {/* Menu thả xuống Profile */}
            {isUserMenuOpen && (
              <div className="absolute right-0 top-full mt-2 w-64 bg-white rounded-2xl shadow-xl border border-slate-200 z-50 overflow-hidden">
                <div className="p-3.5 bg-slate-50 border-b border-slate-100 flex items-center gap-3">
                  <div className="w-10 h-10 rounded-xl bg-blue-600 text-white flex items-center justify-center font-bold text-sm shrink-0">
                    {user?.fullName ? user.fullName[0].toUpperCase() : 'P'}
                  </div>
                  <div className="min-w-0">
                    <p className="text-xs font-bold text-slate-900 truncate">
                      {user?.fullName || (activeRole === 'Owner' ? 'Chủ bãi ParkMaster' : 'Tài xế ParkMaster')}
                    </p>
                    <p className="text-[11px] text-slate-500 truncate">
                      {user?.phone || user?.email || (activeRole === 'Owner' ? 'Chủ bãi' : 'Tài xế')}
                    </p>
                  </div>
                </div>

                <div className="p-1.5 space-y-0.5 text-xs font-medium text-slate-700">
                  <NavLink
                    to="/account/profile"
                    onClick={() => setIsUserMenuOpen(false)}
                    className="flex items-center gap-2.5 px-3 py-2 rounded-xl hover:bg-slate-100 hover:text-slate-900 transition-colors"
                  >
                    <span className="material-symbols-outlined text-[18px] text-slate-500">manage_accounts</span>
                    <span>Hồ sơ cá nhân</span>
                  </NavLink>

                  {/* Xác minh KYC: Hiển thị khi đang ở giao diện Tài xế */}
                  {activeRole === 'Driver' && (
                    <NavLink
                      to="/driver/kyc"
                      onClick={() => setIsUserMenuOpen(false)}
                      className="flex items-center justify-between px-3 py-2 rounded-xl hover:bg-slate-100 hover:text-slate-900 transition-colors"
                    >
                      <div className="flex items-center gap-2.5">
                        <span className="material-symbols-outlined text-[18px] text-blue-600">verified_user</span>
                        <span>Xác minh KYC</span>
                      </div>
                      <span className="text-[10px] font-bold text-blue-700 bg-blue-50 px-2 py-0.5 rounded-full border border-blue-200">
                        Hồ sơ
                      </span>
                    </NavLink>
                  )}

                  {/* Hồ sơ pháp lý KYB: Chỉ hiển thị nếu người dùng đang ở giao diện Chủ bãi */}
                  {activeRole === 'Owner' && (
                    <NavLink
                      to="/owner/kyb"
                      onClick={() => setIsUserMenuOpen(false)}
                      className="flex items-center justify-between px-3 py-2 rounded-xl hover:bg-slate-100 hover:text-slate-900 transition-colors"
                    >
                      <div className="flex items-center gap-2.5">
                        <span className="material-symbols-outlined text-[18px] text-blue-600">verified</span>
                        <span>Hồ sơ pháp lý KYB</span>
                      </div>
                      <span className="text-[10px] font-bold text-blue-700 bg-blue-50 px-2 py-0.5 rounded-full border border-blue-200">
                        Hồ sơ
                      </span>
                    </NavLink>
                  )}
                </div>

                <div className="p-1.5 border-t border-slate-100">
                  <button
                    type="button"
                    disabled={loggingOut}
                    onClick={async () => {
                      setIsUserMenuOpen(false);
                      setLogoutError('');
                      setLoggingOut(true);
                      try {
                        await logoutSession();
                        dispatch(logout());
                        navigate('/login', { replace: true });
                      } catch (error) {
                        setLogoutError(error instanceof Error ? error.message : 'Đăng xuất thất bại.');
                      } finally {
                        setLoggingOut(false);
                      }
                    }}
                    className="w-full flex items-center gap-2.5 px-3 py-2 rounded-xl text-rose-600 hover:bg-rose-50 transition-colors cursor-pointer text-xs font-semibold"
                  >
                    <span className="material-symbols-outlined text-[18px]">logout</span>
                    <span>{loggingOut ? 'Đang đăng xuất...' : 'Đăng xuất'}</span>
                  </button>
                </div>
              </div>
            )}
          </div>

          {logoutError && <span role="alert" className="text-xs text-red-600">{logoutError}</span>}

          {/* Mobile Menu Hamburger */}
          <button
            type="button"
            onClick={() => setIsMobileMenuOpen(!isMobileMenuOpen)}
            className="lg:hidden p-2 rounded-lg text-slate-600 hover:bg-slate-100 cursor-pointer"
            aria-label="Mở menu"
            aria-expanded={isMobileMenuOpen}
            aria-controls="navbar-mobile-menu"
          >
            <span className="material-symbols-outlined text-[24px]">
              {isMobileMenuOpen ? 'close' : 'menu'}
            </span>
          </button>
        </div>
      </div>

      {/* ===== MENU MOBILE ===== */}
      {isMobileMenuOpen && (
        <div id="navbar-mobile-menu" className="lg:hidden pm-navbar-shell bg-white border-t border-slate-200 py-4 space-y-4 shadow-xl">
          {authorizedPortals.length > 1 && (
            <>
              <div className="text-xs font-bold text-slate-400 uppercase tracking-wider px-2">
                Chuyển phân hệ:
              </div>
              <div className="grid grid-cols-2 gap-2">
                {authorizedPortals.map((portal) => {
                  const isCurrent = portal.id === activeRole;
                  return (
                    <NavLink
                      key={portal.id}
                      to={portal.path}
                      onClick={() => setIsMobileMenuOpen(false)}
                      className={`flex items-center gap-2 p-2 rounded-xl text-xs font-bold border transition-all ${
                        isCurrent
                          ? `${portal.badgeClass} border-transparent shadow-xs`
                          : 'border-slate-200 text-slate-700 hover:bg-slate-50'
                      }`}
                    >
                      <span className="material-symbols-outlined text-[18px]">{portal.icon}</span>
                      <span>{portal.label}</span>
                    </NavLink>
                  );
                })}
              </div>
            </>
          )}

          <div className="space-y-1 pt-2 border-t border-slate-100">
            {currentSubNavs.map((sub) => {
              const isSubActive =
                location.pathname === sub.path ||
                (sub.path !== activePortal.path && location.pathname.startsWith(sub.path));
              return (
                <NavLink
                  key={sub.path}
                  to={sub.path}
                  onClick={() => setIsMobileMenuOpen(false)}
                  className={`flex items-center gap-2.5 px-3 py-2 rounded-xl text-xs font-bold transition-colors ${
                    isSubActive
                      ? activePortal.subActiveClass
                      : 'text-slate-700 hover:bg-slate-100'
                  }`}
                >
                  <span className="material-symbols-outlined text-[18px]">{sub.icon}</span>
                  <span>{sub.label}</span>
                </NavLink>
              );
            })}
          </div>
        </div>
      )}
    </header>
  );
};
