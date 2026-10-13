import React, { useEffect, useState } from 'react';
import { NavLink, useNavigate, useLocation } from 'react-router-dom';
import { useAppSelector, useAppDispatch } from '@/store';
import { switchRole } from '@/store/slices/authSlice';
import { fetchNotificationInbox, markNotificationAsRead } from '@/store/slices/notificationSlice';
import type { NotificationDto } from '@/types/notification';
import type { UserRole } from '@/types/auth';

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

export const MainNavbar: React.FC = () => {
  const { user } = useAppSelector((state) => state.auth);
  const dispatch = useAppDispatch();
  const navigate = useNavigate();
  const location = useLocation();

  // ===== Hộp thông báo (NotificationService – TV6) =====
  const { items: notifications, unreadCount, isLoading: isNotificationLoading } = useAppSelector(
    (state) => state.notification
  );
  const [isNotificationOpen, setIsNotificationOpen] = useState(false);
  const userId = user?.id || 5;

  // Tải hộp thông báo khi vào trang. Làm mới thủ công khi mở chuông hoặc bấm refresh.
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
  };

  const handleMarkAllAsRead = async () => {
    for (const n of notifications.filter((item) => !item.isRead)) {
      await dispatch(markNotificationAsRead({ notificationId: n.id, userId })).unwrap();
    }
  };

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
              <option value="Driver">🚗 /driver (Tài xế)</option>
              <option value="Owner">🏢 /owner (Chủ bãi)</option>
              <option value="Staff">🚧 /gate (Soát vé)</option>
              <option value="Admin">⚙️ /admin (Quản trị)</option>
            </select>
          </div>

          {/* Notifications – hộp thư thật từ NotificationService (GET /api/v1/notifications) */}
          <div className="relative">
            <button
              onClick={handleToggleNotifications}
              className="relative p-2 rounded-lg text-slate-500 hover:bg-slate-100 hover:text-slate-700 transition-colors cursor-pointer"
              type="button"
              aria-label="Thông báo"
            >
              <span className="material-symbols-outlined text-[24px]">notifications</span>
              {unreadCount > 0 && (
                <span className="absolute top-0.5 right-0.5 min-w-[18px] h-[18px] px-1 rounded-full bg-red-600 text-white text-[10px] font-bold flex items-center justify-center ring-2 ring-white">
                  {unreadCount > 9 ? '9+' : unreadCount}
                </span>
              )}
            </button>

            {isNotificationOpen && (
              <div className="fixed inset-0 z-40" onClick={() => setIsNotificationOpen(false)} />
            )}

            {isNotificationOpen && (
              <div className="absolute right-0 top-full mt-2 w-[380px] max-w-[calc(100vw-2rem)] bg-white rounded-2xl shadow-2xl border border-slate-200 z-50 overflow-hidden">
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

                <div className="px-4 py-2.5 bg-slate-50 border-t border-slate-100 text-center">
                  <span className="text-[10px] text-slate-400 font-medium">
                    Đồng bộ với NotificationService (TV6) qua Gateway :5000
                  </span>
                </div>
              </div>
            )}
          </div>

          {/* User Profile Info */}
          <div className="flex items-center gap-2.5 pl-2 border-l border-slate-200">
            <div className="flex flex-col items-end hidden sm:flex">
              <span className="text-sm font-semibold text-slate-900">{user?.fullName || 'Nguyễn Văn An'}</span>
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
