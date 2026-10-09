import React from 'react';
import { Navigate, Outlet } from 'react-router-dom';
import { useAppSelector } from '@/store';
import type { UserRole } from '@/types/auth';

interface ProtectedRouteProps {
  allowedRoles?: UserRole[];
}

export const ProtectedRoute: React.FC<ProtectedRouteProps> = ({ allowedRoles }) => {
  const { user, isAuthenticated } = useAppSelector((state) => state.auth);

  if (!isAuthenticated || !user) {
    return <Navigate to="/driver" replace />;
  }

  if (allowedRoles && !allowedRoles.includes(user.role)) {
    // If not matching role, redirect to driver area or unauthorized page
    return (
      <div className="max-w-4xl mx-auto py-16 px-4 text-center">
        <div className="w-16 h-16 bg-red-100 text-red-600 rounded-full flex items-center justify-center mx-auto mb-4">
          <span className="material-symbols-outlined text-[32px]">lock</span>
        </div>
        <h2 className="text-2xl font-bold text-slate-800">Không có quyền truy cập</h2>
        <p className="text-slate-600 mt-2">
          Vai trò hiện tại của bạn là <strong className="text-blue-600">{user.role}</strong>, không được phép truy cập phân hệ này.
        </p>
        <p className="text-xs text-slate-400 mt-1">
          (Bạn có thể đổi vai trò nhanh trên thanh Navbar để kiểm thử phân hệ tương ứng)
        </p>
      </div>
    );
  }

  return <Outlet />;
};
