import React from 'react';
import { Navigate, Outlet, useLocation } from 'react-router-dom';
import { useAppSelector } from '@/store';
import type { UserRole } from '@/types/auth';

interface ProtectedRouteProps {
  allowedRoles?: UserRole[];
}

export const ProtectedRoute: React.FC<ProtectedRouteProps> = ({ allowedRoles }) => {
  const { user, isAuthenticated, isLoading } = useAppSelector((state) => state.auth);
  const location = useLocation();

  if (isLoading) {
    return (
      <div className="flex items-center justify-center p-16 text-slate-500 font-medium">
        <span className="material-symbols-outlined text-[24px] animate-spin mr-2">progress_activity</span>
        Đang kiểm tra phiên đăng nhập…
      </div>
    );
  }

  // 1. Chưa đăng nhập -> Chuyển hướng về trang Login kèm vị trí trang muốn vào (from)
  if (!isAuthenticated || !user) {
    return <Navigate to="/login" state={{ from: location }} replace />;
  }

  // 2. Kiểm tra phân quyền: lấy danh sách roles của user
  const userRoles: UserRole[] = user.roles && user.roles.length > 0 ? user.roles : [user.role];

  if (allowedRoles && allowedRoles.length > 0) {
    // Admin có toàn quyền hoặc user có ít nhất một role thuộc allowedRoles
    const hasPermission = userRoles.includes('Admin') || userRoles.some((r) => allowedRoles.includes(r));

    if (!hasPermission) {
      const defaultUserPath =
        userRoles.includes('Admin')
          ? '/admin'
          : userRoles.includes('Owner')
            ? '/owner'
            : userRoles.includes('Staff')
              ? '/gate'
              : '/driver';

      return (
        <div className="max-w-4xl mx-auto py-20 px-4 text-center">
          <div className="w-16 h-16 bg-red-100 text-red-600 rounded-full flex items-center justify-center mx-auto mb-4 shadow-xs">
            <span className="material-symbols-outlined text-[32px]">lock</span>
          </div>
          <h2 className="text-2xl font-bold text-slate-800">Không có quyền truy cập (403 Forbidden)</h2>
          <p className="text-slate-600 mt-2">
            Tài khoản <strong className="text-slate-900">{user.fullName}</strong> với vai trò{' '}
            <span className="inline-flex items-center px-2 py-0.5 rounded-full text-xs font-bold bg-blue-100 text-blue-800">
              {user.role}
            </span>{' '}
            không được cấp quyền truy cập phân hệ này.
          </p>
          <p className="text-xs text-slate-400 mt-1">
            Vui lòng chọn phân hệ đúng với vai trò của tài khoản hoặc đăng nhập bằng tài khoản khác.
          </p>
          <div className="mt-6 flex items-center justify-center gap-3">
            <button
              type="button"
              onClick={() => window.history.back()}
              className="px-4 py-2 text-sm font-semibold text-slate-600 bg-slate-100 rounded-xl hover:bg-slate-200 transition-colors cursor-pointer"
            >
              Quay lại
            </button>
            <a
              href={defaultUserPath}
              className="px-4 py-2 text-sm font-semibold text-white bg-blue-600 rounded-xl hover:bg-blue-700 transition-colors shadow-xs"
            >
              Về phân hệ của bạn
            </a>
          </div>
        </div>
      );
    }
  }

  return <Outlet />;
};
